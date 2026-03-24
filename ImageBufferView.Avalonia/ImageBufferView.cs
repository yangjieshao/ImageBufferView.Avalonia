using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using System;
using System.Buffers;
using System.IO;
using System.Threading;

namespace ImageBufferView.Avalonia;

public partial class ImageBufferView : Control
{
    /// <summary>
    /// 并发解码数 = CPU 核心数（充分利用多核）
    /// </summary>
    private static readonly SemaphoreSlim s_decodeSemaphore =
        new(Environment.ProcessorCount, Environment.ProcessorCount);

    static ImageBufferView()
    {
        AffectsRender<ImageBufferView>(BitmapProperty, StretchProperty, StretchDirectionProperty, DefaultBackgroundProperty);
        AffectsMeasure<ImageBufferView>(BitmapProperty, StretchProperty, StretchDirectionProperty, DefaultBackgroundProperty);
        AffectsArrange<ImageBufferView>(BitmapProperty, StretchProperty, StretchDirectionProperty, DefaultBackgroundProperty);

        BitmapProperty.Changed.AddClassHandler<ImageBufferView>(BitmapChanged);
        ImageBufferProperty.Changed.AddClassHandler<ImageBufferView>(ImageBufferChanged);
        InterpolationModeProperty.Changed.AddClassHandler<ImageBufferView>(InterpolationModeChanged);
    }

    public ImageBufferView() : base()
    {
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.MediumQuality);
    }

    #region Properties

    public static readonly StyledProperty<Stretch> StretchProperty =
        AvaloniaProperty.Register<ImageBufferView, Stretch>(nameof(Stretch), Stretch.None);

    public Stretch Stretch
    {
        get => GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    public static readonly StyledProperty<StretchDirection> StretchDirectionProperty =
        AvaloniaProperty.Register<ImageBufferView, StretchDirection>(nameof(StretchDirection), StretchDirection.Both);

    public StretchDirection StretchDirection
    {
        get => GetValue(StretchDirectionProperty);
        set => SetValue(StretchDirectionProperty, value);
    }

    public static readonly StyledProperty<ArraySegment<byte>?> ImageBufferProperty =
        AvaloniaProperty.Register<ImageBufferView, ArraySegment<byte>?>(nameof(ImageBuffer));

    public ArraySegment<byte>? ImageBuffer
    {
        get => GetValue(ImageBufferProperty);
        set => SetValue(ImageBufferProperty, value);
    }

    public static readonly StyledProperty<Bitmap?> BitmapProperty =
        AvaloniaProperty.Register<ImageBufferView, Bitmap?>(nameof(Bitmap));

    public Bitmap? Bitmap
    {
        get => GetValue(BitmapProperty);
        set => SetValue(BitmapProperty, value);
    }

    public static readonly StyledProperty<IBrush?> DefaultBackgroundProperty =
        AvaloniaProperty.Register<ImageBufferView, IBrush?>(nameof(DefaultBackground));

    public IBrush? DefaultBackground
    {
        get => GetValue(DefaultBackgroundProperty);
        set => SetValue(DefaultBackgroundProperty, value);
    }

    /// <summary>
    /// 插值模式：LowQuality 性能最高，HighQuality 质量最好
    /// </summary>
    public static readonly StyledProperty<BitmapInterpolationMode> InterpolationModeProperty =
        AvaloniaProperty.Register<ImageBufferView, BitmapInterpolationMode>(
            nameof(InterpolationMode), BitmapInterpolationMode.MediumQuality);

    public BitmapInterpolationMode InterpolationMode
    {
        get => GetValue(InterpolationModeProperty);
        set => SetValue(InterpolationModeProperty, value);
    }

    private static void InterpolationModeChanged(ImageBufferView sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is BitmapInterpolationMode mode)
        {
            RenderOptions.SetBitmapInterpolationMode(sender, mode);
        }
    }

    #endregion

    #region Fields

    private byte[]? _latestBuffer;
    private int _latestBufferLength;
    private int _decoding;
    private volatile bool _isAttached;
    private CancellationTokenSource? _sessionCts;
    private readonly Lock _sessionLock = new();

    #endregion

    public Size RenderSize => Bounds.Size;
    public Size SourceSize { get; private set; }

    private static void ImageBufferChanged(ImageBufferView sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (!sender._isAttached)
        {
            return;
        }

        if (e.NewValue is ArraySegment<byte> { Array: not null, Count: > 0 } buffer)
        {
            // 使用 ArrayPool 减少 GC 压力
            var pooledBuffer = ArrayPool<byte>.Shared.Rent(buffer.Count);
            buffer.AsSpan().CopyTo(pooledBuffer);

            // 原子替换，回收旧缓冲区
            var oldBuffer = Interlocked.Exchange(ref sender._latestBuffer, pooledBuffer);
            Interlocked.Exchange(ref sender._latestBufferLength, buffer.Count);

            if (oldBuffer is not null)
            {
                ArrayPool<byte>.Shared.Return(oldBuffer);
            }

            if (Interlocked.CompareExchange(ref sender._decoding, 1, 0) == 0)
            {
                ThreadPool.UnsafeQueueUserWorkItem(static ctrl => ctrl.DecodeLoop(), sender, preferLocal: false);
            }
        }
        else
        {
            sender.CancelCurrentSession();
            var oldBitmap = sender.Bitmap;
            sender.Bitmap = null;
            oldBitmap?.Dispose();
        }
    }

    private static void BitmapChanged(ImageBufferView sender, AvaloniaPropertyChangedEventArgs e)
    {
        sender.SourceSize = e.NewValue is Bitmap bitmap ? bitmap.Size : sender.RenderSize;
    }

    private void CancelCurrentSession()
    {
        lock (_sessionLock)
        {
            _sessionCts?.Cancel();
            _sessionCts?.Dispose();
            _sessionCts = null;

            var oldBuffer = Interlocked.Exchange(ref _latestBuffer, null);
            Interlocked.Exchange(ref _latestBufferLength, 0);
            if (oldBuffer is not null)
            {
                ArrayPool<byte>.Shared.Return(oldBuffer);
            }
        }
    }

    private CancellationToken GetOrCreateSessionToken()
    {
        lock (_sessionLock)
        {
            _sessionCts ??= new CancellationTokenSource();
            return _sessionCts.Token;
        }
    }

    private void DecodeLoop()
    {
        var token = GetOrCreateSessionToken();

        while (_isAttached && !token.IsCancellationRequested)
        {
            var buffer = Interlocked.Exchange(ref _latestBuffer, null);
            var length = Interlocked.Exchange(ref _latestBufferLength, 0);

            if (buffer is null || length == 0)
            {
                Volatile.Write(ref _decoding, 0);

                if (_latestBuffer is not null &&
                    Interlocked.CompareExchange(ref _decoding, 1, 0) == 0)
                {
                    continue;
                }
                return;
            }

            if (token.IsCancellationRequested)
            {
                ArrayPool<byte>.Shared.Return(buffer);
                return;
            }

            Bitmap? newBitmap = null;
            try
            {
                // 尝试获取信号量，超时则跳过此帧（避免积压）
                if (!s_decodeSemaphore.Wait(0, token))
                {
                    // 信号量不可用，丢弃此帧，继续处理下一帧
                    ArrayPool<byte>.Shared.Return(buffer);
                    continue;
                }

                try
                {
                    if (token.IsCancellationRequested)
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                        return;
                    }

                    // 使用 UnmanagedMemoryStream 或直接从内存解码，减少拷贝
                    using var stream = new MemoryStream(buffer, 0, length, false);
                    newBitmap = new Bitmap(stream);
                }
                finally
                {
                    s_decodeSemaphore.Release();
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                continue;
            }

            if (!_isAttached || token.IsCancellationRequested)
            {
                newBitmap.Dispose();
                return;
            }

            var capturedToken = token;
            var bitmapToSet = newBitmap;

            // 使用 Send 而非 Post，确保 Bitmap 及时更新（GPU 渲染更流畅）
            Dispatcher.UIThread.Post(() =>
            {
                if (!_isAttached || capturedToken.IsCancellationRequested)
                {
                    bitmapToSet.Dispose();
                    return;
                }
                var oldBitmap = Bitmap;
                Bitmap = bitmapToSet;
                oldBitmap?.Dispose();
            }, DispatcherPriority.Render);
        }

        Volatile.Write(ref _decoding, 0);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        return Bitmap is not null ? Stretch.CalculateSize(availableSize, SourceSize, StretchDirection)
                                  : base.MeasureOverride(availableSize);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        return Bitmap is not null ? Stretch.CalculateSize(finalSize, SourceSize)
                                  : base.ArrangeOverride(finalSize);
    }

    public override void Render(DrawingContext drawingContext)
    {
        if (RenderSize is { Width: > 0.0, Height: > 0.0 })
        {
            if (Bitmap is not null && SourceSize is { Width: > 0.0, Height: > 0.0 })
            {
                var viewPort = new Rect(RenderSize);
                var sourceSize = SourceSize;
                var scale = Stretch.CalculateScaling(RenderSize, sourceSize, StretchDirection);

                if (scale is { X: > 0.0, Y: > 0.0 })
                {
                    var scaledSize = sourceSize * scale;
                    var destRect = viewPort.CenterRect(new Rect(scaledSize)).Intersect(viewPort);

                    if (destRect is { Width: > 0.0, Height: > 0.0 })
                    {
                        var sourceRect = new Rect(sourceSize).CenterRect(new Rect(destRect.Size / scale));
                        drawingContext.DrawImage(Bitmap, sourceRect, destRect);
                    }
                }
            }
            else if (DefaultBackground is { } background)
            {
                drawingContext.FillRectangle(background, new Rect(RenderSize));
            }
        }

        base.Render(drawingContext);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _isAttached = true;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _isAttached = false;
        CancelCurrentSession();
        base.OnDetachedFromVisualTree(e);
        var oldBitmap = Bitmap;
        Bitmap = null;
        oldBitmap?.Dispose();
    }
}