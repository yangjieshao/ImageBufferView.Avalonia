using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using SkiaSharp;
using System;
using System.Buffers;
using System.Diagnostics;
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

    /// <summary>
    /// 是否启用 SkiaSharp 预缩放优化
    /// 当输入图片与渲染区域大小不一致时，在解码阶段预先缩放可显著提升渲染性能
    /// </summary>
    public static readonly StyledProperty<bool> EnablePreScaleProperty =
        AvaloniaProperty.Register<ImageBufferView, bool>(nameof(EnablePreScale), true);

    public bool EnablePreScale
    {
        get => GetValue(EnablePreScaleProperty);
        set => SetValue(EnablePreScaleProperty, value);
    }

    /// <summary>
    /// 源图片分辨率提示，用于优化缓冲区复用策略
    /// 默认值为 Unknown（最保守模式，不复用缓冲区）
    /// </summary>
    public static readonly StyledProperty<SourceResolutionHint> SourceResolutionHintProperty =
        AvaloniaProperty.Register<ImageBufferView, SourceResolutionHint>(
            nameof(SourceResolutionHint), SourceResolutionHint.Unknown);

    /// <summary>
    /// 源图片分辨率提示，用于优化缓冲区复用策略
    /// <list type="bullet">
    /// <item><description>Unknown：最保守模式，不复用缓冲区</description></item>
    /// <item><description>Fixed：源图片分辨率固定，可复用所有缓冲区（性能提升 20-30%）</description></item>
    /// <item><description>VariableLargerThanRender：源图片分辨率不固定但都大于渲染区，可复用目标缓冲区（性能提升 10-20%）</description></item>
    /// </list>
    /// </summary>
    public SourceResolutionHint SourceResolutionHint
    {
        get => GetValue(SourceResolutionHintProperty);
        set => SetValue(SourceResolutionHintProperty, value);
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
    private Size _cachedRenderSize;
    private Stretch _cachedStretch;
    private StretchDirection _cachedStretchDirection;
    private bool _cachedEnablePreScale;
    private SourceResolutionHint _cachedSourceResolutionHint;

    // 缓冲区复用相关字段（双缓冲方式避免竞态条件）
    private WriteableBitmap? _backBuffer;        // 后台缓冲区（用于写入）
    private PixelSize _backBufferSize;
    private PixelFormat _backBufferFormat;
    private readonly Lock _backBufferLock = new();

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
            sender.TryStartDecode(buffer);
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

                    // 使用 SkiaSharp 解码并预缩放
                    newBitmap = DecodeAndScaleBitmap(buffer, length);
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

            if (newBitmap is null)
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

            // 使用 Post 确保 Bitmap 及时更新（GPU 渲染更流畅）
            Dispatcher.UIThread.Post(() =>
            {
                if (!_isAttached || capturedToken.IsCancellationRequested)
                {
                    bitmapToSet?.Dispose();
                    return;
                }

                var oldBitmap = Bitmap;
                Bitmap = bitmapToSet;

                // 将旧 Bitmap 回收到后台缓冲区（如果尺寸匹配）
                if (oldBitmap is WriteableBitmap oldWriteable)
                {
                    RecycleToBackBuffer(oldWriteable);
                }
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
        _cachedRenderSize = Bounds.Size;
        // 处理在 attach 之前已设置的 ImageBuffer
        if (ImageBuffer is { Array: not null, Count: > 0 } buffer)
        {
            TryStartDecode(buffer);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _isAttached = false;
        CancelCurrentSession();

        // 清理 Bitmap
        var oldBitmap = Bitmap;
        Bitmap = null;
        oldBitmap?.Dispose();

        // 清理后台缓冲区
        ClearBackBuffer();

        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>
    /// 将旧的 WriteableBitmap 回收到后台缓冲区（用于下次复用）
    /// </summary>
    private void RecycleToBackBuffer(WriteableBitmap bitmap)
    {
        if (_cachedSourceResolutionHint == SourceResolutionHint.Unknown)
        {
            // Unknown 模式不复用，直接释放
            bitmap.Dispose();
            return;
        }

        lock (_backBufferLock)
        {
            var size = bitmap.PixelSize;
            var format = bitmap.Format ?? PixelFormat.Bgra8888;

            // 如果后台缓冲区为空或尺寸不匹配，替换它
            if (_backBuffer is null || _backBufferSize != size || _backBufferFormat != format)
            {
                _backBuffer?.Dispose();
                _backBuffer = bitmap;
                _backBufferSize = size;
                _backBufferFormat = format;
            }
            else
            {
                // 尺寸匹配但已有缓冲区，释放新的
                bitmap.Dispose();
            }
        }
    }

    /// <summary>
    /// 清理后台缓冲区
    /// </summary>
    private void ClearBackBuffer()
    {
        WriteableBitmap? bitmapToDispose = null;

        lock (_backBufferLock)
        {
            bitmapToDispose = _backBuffer;
            _backBuffer = null;
            _backBufferSize = default;
        }

        bitmapToDispose?.Dispose();
    }

    /// <summary>
    /// 使用 SkiaSharp 解码图片，并根据渲染区域进行预缩放优化
    /// </summary>
    private WriteableBitmap? DecodeAndScaleBitmap(byte[] buffer, int length)
    {
        using var skData = SKData.CreateCopy(new ReadOnlySpan<byte>(buffer, 0, length));
        using var skBitmap = SKBitmap.Decode(skData);

        if (skBitmap is null)
        {
            return null;
        }

        var sourceWidth = skBitmap.Width;
        var sourceHeight = skBitmap.Height;
        var sourceSize = new PixelSize(sourceWidth, sourceHeight);
        var renderSize = _cachedRenderSize;
        var stretch = _cachedStretch;
        var stretchDirection = _cachedStretchDirection;
        var resolutionHint = _cachedSourceResolutionHint;

        // 判断是否需要预缩放
        if (!_cachedEnablePreScale || renderSize.Width <= 0 || renderSize.Height <= 0)
        {
            // 不缩放，直接转换为 Avalonia Bitmap
            return ConvertSkBitmapToAvaloniaWithReuse(skBitmap, sourceSize, resolutionHint);
        }

        // 计算缩放比例（使用缓存的值，避免跨线程访问）
        var scale = stretch.CalculateScaling(renderSize, new Size(sourceWidth, sourceHeight), stretchDirection);

        if (scale.X >= 1.0 && scale.Y >= 1.0)
        {
            // 图片小于或等于渲染区域，不需要预缩放（放大由 GPU 处理更高效）
            return ConvertSkBitmapToAvaloniaWithReuse(skBitmap, sourceSize, resolutionHint);
        }

        // 图片大于渲染区域，预先缩小以减少渲染负担
        var targetWidth = Math.Max(1, (int)(sourceWidth * scale.X));
        var targetHeight = Math.Max(1, (int)(sourceHeight * scale.Y));
        var targetSize = new PixelSize(targetWidth, targetHeight);

        // 使用 SkiaSharp 高效缩放
        using var resizedBitmap = skBitmap.Resize(new SKImageInfo(targetWidth, targetHeight), SKFilterQuality.Medium);

        if (resizedBitmap is null)
        {
            return ConvertSkBitmapToAvaloniaWithReuse(skBitmap, sourceSize, resolutionHint);
        }

        // 对于缩放后的图片，目标尺寸是固定的（基于渲染区域），可以考虑复用
        return ConvertSkBitmapToAvaloniaWithReuse(resizedBitmap, targetSize, resolutionHint, isScaled: true);
    }

    /// <summary>
    /// 将 SKBitmap 转换为 Avalonia Bitmap，支持缓冲区复用（双缓冲方式）
    /// </summary>
    /// <param name="skBitmap">源 SKBitmap</param>
    /// <param name="expectedSize">预期的输出尺寸</param>
    /// <param name="resolutionHint">分辨率提示</param>
    /// <param name="isScaled">是否已经过缩放处理</param>
    private WriteableBitmap? ConvertSkBitmapToAvaloniaWithReuse(
        SKBitmap skBitmap,
        PixelSize expectedSize,
        SourceResolutionHint resolutionHint,
        bool isScaled = false)
    {
        var info = skBitmap.Info;
        var pixelFormat = info.ColorType switch
        {
            SKColorType.Rgba8888 => PixelFormat.Rgba8888,
            SKColorType.Bgra8888 => PixelFormat.Bgra8888,
            _ => PixelFormat.Bgra8888
        };

        // 如果颜色类型不匹配，需要转换
        SKBitmap? convertedBitmap = null;
        var bitmapToUse = skBitmap;

        if (info.ColorType != SKColorType.Bgra8888 && info.ColorType != SKColorType.Rgba8888)
        {
            convertedBitmap = new SKBitmap(info.Width, info.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas(convertedBitmap);
            canvas.DrawBitmap(skBitmap, 0, 0);
            bitmapToUse = convertedBitmap;
            pixelFormat = PixelFormat.Bgra8888;
        }

        try
        {
            var pixels = bitmapToUse.GetPixels();
            var rowBytes = bitmapToUse.RowBytes;
            var width = bitmapToUse.Width;
            var height = bitmapToUse.Height;
            var currentSize = new PixelSize(width, height);

            // 根据分辨率提示决定是否尝试复用后台缓冲区
            var canReuse = resolutionHint switch
            {
                // Fixed 模式：源图片分辨率固定，可以复用（无论是否缩放）
                SourceResolutionHint.Fixed => true,
                // VariableLargerThanRender 模式：只有缩放后的图片可以复用（目标尺寸固定）
                SourceResolutionHint.VariableLargerThanRender => isScaled,
                // Unknown 模式：不复用
                _ => false
            };

            WriteableBitmap? bitmap = null;

            if (canReuse)
            {
                // 尝试从后台缓冲区获取可复用的 Bitmap
                lock (_backBufferLock)
                {
                    if (_backBuffer is not null &&
                        _backBufferSize == currentSize &&
                        _backBufferFormat == pixelFormat)
                    {
                        // 复用后台缓冲区
                        bitmap = _backBuffer;
                        _backBuffer = null;
                    }
                }
            }

            // 如果没有可复用的缓冲区，创建新的
            bitmap ??= new WriteableBitmap(
                    currentSize,
                    new Vector(96, 96),
                    pixelFormat,
                    AlphaFormat.Premul);

            // 写入像素数据
            using var fb = bitmap.Lock();
            unsafe
            {
                Buffer.MemoryCopy(
                    (void*)pixels,
                    (void*)fb.Address,
                    fb.RowBytes * height,
                    rowBytes * height);
            }

            return bitmap;
        }
        catch
        {
            return null;
        }
        finally
        {
            convertedBitmap?.Dispose();
        }
    }

    private void TryStartDecode(ArraySegment<byte> buffer)
    {
        // 缓存当前渲染大小和缩放设置，供后台线程使用
        _cachedRenderSize = Bounds.Size;
        _cachedStretch = Stretch;
        _cachedStretchDirection = StretchDirection;
        _cachedEnablePreScale = EnablePreScale;
        _cachedSourceResolutionHint = SourceResolutionHint;

        var pooledBuffer = ArrayPool<byte>.Shared.Rent(buffer.Count);
        buffer.AsSpan().CopyTo(pooledBuffer);

        var oldBuffer = Interlocked.Exchange(ref _latestBuffer, pooledBuffer);
        Interlocked.Exchange(ref _latestBufferLength, buffer.Count);

        if (oldBuffer is not null)
        {
            ArrayPool<byte>.Shared.Return(oldBuffer);
        }

        if (Interlocked.CompareExchange(ref _decoding, 1, 0) == 0)
        {
            ThreadPool.UnsafeQueueUserWorkItem(static ctrl => ctrl.DecodeLoop(), this, preferLocal: false);
        }
    }
}