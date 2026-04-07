using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Reactive;
using Avalonia.Threading;
using System;
using System.Buffers;
using System.Threading;

namespace ImageBufferView.Avalonia;

/// <summary>
/// 表示一个可从字节缓冲解码并显示的图像控件，包含预缩放和缓冲复用优化。
/// </summary>
public partial class ImageBufferView : Control
{
    /// <summary>
    /// 并发解码信号量（默认基于 CPU 核心数，可通过静态/实例属性调整）
    /// </summary>
    private static SemaphoreSlim SDecodeSemaphore =
        new(Environment.ProcessorCount, Environment.ProcessorCount);

    // 用于记录当前最大并发数的静态字段
    private static int s_maxDecodeConcurrency = Environment.ProcessorCount;
    private static readonly object SDecodeSemaphoreLock = new();

    /// <summary>
    /// 全局默认的最大并发解码数。设置此值会重新创建用于解码的信号量（线程安全）。
    /// </summary>
    public static int MaxDecodeConcurrency
    {
        get => Volatile.Read(ref s_maxDecodeConcurrency);
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            lock (SDecodeSemaphoreLock)
            {
                var old = Volatile.Read(ref s_maxDecodeConcurrency);
                if (old == value) return;

                var newSem = new SemaphoreSlim(value, value);
                var prev = Interlocked.Exchange(ref SDecodeSemaphore, newSem);
                Volatile.Write(ref s_maxDecodeConcurrency, value);

                try
                {
                    prev?.Dispose();
                }
                catch
                {
                    // 忽略 Dispose 过程中的异常，保证健壮性
                }
            }
        }
    }

    static ImageBufferView()
    {
        AffectsRender<ImageBufferView>(BitmapProperty, StretchProperty, StretchDirectionProperty,
            DefaultBackgroundProperty);
        AffectsMeasure<ImageBufferView>(BitmapProperty, StretchProperty, StretchDirectionProperty,
            DefaultBackgroundProperty);
        AffectsArrange<ImageBufferView>(BitmapProperty, StretchProperty, StretchDirectionProperty,
            DefaultBackgroundProperty);

        BitmapProperty.Changed.AddClassHandler<ImageBufferView>(BitmapChanged);
        ImageBufferProperty.Changed.AddClassHandler<ImageBufferView>(ImageBufferChanged);
        InterpolationModeProperty.Changed.AddClassHandler<ImageBufferView>(InterpolationModeChanged);
        SourceViewProperty.Changed.AddClassHandler<ImageBufferView>(SourceViewChanged);
        PixelBufferFormatProperty.Changed.AddClassHandler<ImageBufferView>(RawFormatChanged);
        RawImageWidthProperty.Changed.AddClassHandler<ImageBufferView>(RawFormatChanged);
        RawImageHeightProperty.Changed.AddClassHandler<ImageBufferView>(RawFormatChanged);
        FrameIndexProperty.Changed.AddClassHandler<ImageBufferView>(FrameIndexChanged);
    }

    public ImageBufferView()
    {
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.MediumQuality);
    }

    #region Properties

    /// <summary>
    /// 控件缩放模式
    /// </summary>
    public static readonly StyledProperty<Stretch> StretchProperty =
        AvaloniaProperty.Register<ImageBufferView, Stretch>(nameof(Stretch), Stretch.None);

    /// <summary>
    /// 控件缩放模式
    /// </summary>
    public Stretch Stretch
    {
        get => GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    /// <summary>
    /// 缩放方向约束
    /// </summary>
    public static readonly StyledProperty<StretchDirection> StretchDirectionProperty =
        AvaloniaProperty.Register<ImageBufferView, StretchDirection>(nameof(StretchDirection), StretchDirection.Both);

    /// <summary>
    /// 缩放方向约束
    /// </summary>
    public StretchDirection StretchDirection
    {
        get => GetValue(StretchDirectionProperty);
        set => SetValue(StretchDirectionProperty, value);
    }

    /// <summary>
    /// 输入的图片字节缓冲（可为 null 表示清空）
    /// </summary>
    public static readonly StyledProperty<ArraySegment<byte>?> ImageBufferProperty =
        AvaloniaProperty.Register<ImageBufferView, ArraySegment<byte>?>(nameof(ImageBuffer));

    /// <summary>
    /// 输入的图片字节缓冲（可为 null 表示清空）
    /// </summary>
    public ArraySegment<byte>? ImageBuffer
    {
        get => GetValue(ImageBufferProperty);
        set => SetValue(ImageBufferProperty, value);
    }

    /// <summary>
    /// 当前显示的 Bitmap（只读）
    /// </summary>
    public static readonly StyledProperty<Bitmap?> BitmapProperty =
        AvaloniaProperty.Register<ImageBufferView, Bitmap?>(nameof(Bitmap));

    /// <summary>
    /// 当前显示的 Bitmap（只读）
    /// </summary>
    public Bitmap? Bitmap
    {
        get => GetValue(BitmapProperty);
        private set => SetValue(BitmapProperty, value);
    }

    /// <summary>
    /// 如果设置为另一个 ImageBufferView，则共享并同步该控件的 Bitmap（只读复用）
    /// </summary>
    public static readonly StyledProperty<ImageBufferView?> SourceViewProperty =
        AvaloniaProperty.Register<ImageBufferView, ImageBufferView?>(nameof(SourceView));

    /// <summary>
    /// 源 ImageBufferView，用于复用其他控件的 Bitmap。设置后会自动同步源控件的 Bitmap。
    /// </summary>
    public ImageBufferView? SourceView
    {
        get => GetValue(SourceViewProperty);
        set => SetValue(SourceViewProperty, value);
    }

    /// <summary>
    /// 当没有图片时使用的默认背景画刷
    /// </summary>
    public static readonly StyledProperty<IBrush?> DefaultBackgroundProperty =
        AvaloniaProperty.Register<ImageBufferView, IBrush?>(nameof(DefaultBackground));

    /// <summary>
    /// 当没有图片时使用的默认背景画刷
    /// </summary>
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

    /// <summary>
    /// 插值模式：LowQuality 性能最高，HighQuality 质量最好
    /// </summary>
    public BitmapInterpolationMode InterpolationMode
    {
        get => GetValue(InterpolationModeProperty);
        set => SetValue(InterpolationModeProperty, value);
    }

    /// <summary>
    /// 是否启用性能优化（默认启用），包含预缩放和缓冲复用
    /// </summary>
    public static readonly StyledProperty<bool> EnableOptimizationProperty =
        AvaloniaProperty.Register<ImageBufferView, bool>(nameof(EnableOptimization), true);

    /// <summary>
    /// 是否启用性能优化（默认启用），包含预缩放和缓冲复用
    /// </summary>
    public bool EnableOptimization
    {
        get => GetValue(EnableOptimizationProperty);
        set => SetValue(EnableOptimizationProperty, value);
    }

    /// <summary>
    /// 像素缓冲格式（默认 Encoded，即解码 JPEG/PNG 等编码格式）
    /// </summary>
    public static readonly StyledProperty<PixelBufferFormat> PixelBufferFormatProperty =
        AvaloniaProperty.Register<ImageBufferView, PixelBufferFormat>(nameof(PixelBufferFormat),
            PixelBufferFormat.Encoded);

    /// <summary>
    /// 像素缓冲格式（默认 Encoded，即解码 JPEG/PNG 等编码格式）。
    /// 设置为非 Encoded 时，<see cref="ImageBuffer"/> 应传入未编码的原始像素数据，
    /// 且必须同时配置 <see cref="RawImageWidth"/> 和 <see cref="RawImageHeight"/>。
    /// </summary>
    public PixelBufferFormat PixelBufferFormat
    {
        get => GetValue(PixelBufferFormatProperty);
        set => SetValue(PixelBufferFormatProperty, value);
    }

    /// <summary>
    /// 原始像素图像宽度（像素数）。当 <see cref="PixelBufferFormat"/> 不为 Encoded 时必须设置。
    /// </summary>
    public static readonly StyledProperty<int> RawImageWidthProperty =
        AvaloniaProperty.Register<ImageBufferView, int>(nameof(RawImageWidth), 0);

    /// <summary>
    /// 原始像素图像宽度（像素数）。当 <see cref="PixelBufferFormat"/> 不为 Encoded 时必须设置。
    /// </summary>
    public int RawImageWidth
    {
        get => GetValue(RawImageWidthProperty);
        set => SetValue(RawImageWidthProperty, value);
    }

    /// <summary>
    /// 原始像素图像高度（像素数）。当 <see cref="PixelBufferFormat"/> 不为 Encoded 时必须设置。
    /// </summary>
    public static readonly StyledProperty<int> RawImageHeightProperty =
        AvaloniaProperty.Register<ImageBufferView, int>(nameof(RawImageHeight), 0);

    /// <summary>
    /// 原始像素图像高度（像素数）。当 <see cref="PixelBufferFormat"/> 不为 Encoded 时必须设置。
    /// </summary>
    public int RawImageHeight
    {
        get => GetValue(RawImageHeightProperty);
        set => SetValue(RawImageHeightProperty, value);
    }

    /// <summary>
    /// 帧号（单调递增计数器）。当数据源直接修改 <see cref="ImageBuffer"/> 内部字节内容、
    /// 而非替换为新对象时，可通过更新此属性强制触发画面刷新。
    /// </summary>
    public static readonly StyledProperty<long> FrameIndexProperty =
        AvaloniaProperty.Register<ImageBufferView, long>(nameof(FrameIndex), 0L);

    /// <summary>
    /// 帧号（单调递增计数器）。当数据源直接修改 <see cref="ImageBuffer"/> 内部字节内容、
    /// 而非替换为新对象时，可通过更新此属性强制触发画面刷新。
    /// </summary>
    public long FrameIndex
    {
        get => GetValue(FrameIndexProperty);
        set => SetValue(FrameIndexProperty, value);
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
    private bool _cachedEnableOptimization;
    private PixelBufferFormat _cachedPixelBufferFormat;
    private int _cachedRawImageWidth;
    private int _cachedRawImageHeight;

    // 缓冲区复用相关字段（双缓冲方式避免竞态条件）
    private WriteableBitmap? _backBuffer; // 后台缓冲区（用于写入）
    private PixelSize _backBufferSize;
    private PixelFormat _backBufferFormat;
    private readonly Lock _backBufferLock = new();
    private PixelSizeBox? _lastDecodedSourceSizeBox; // 上一次解码的源图片尺寸（用于检测分辨率变化）

    // SourceView 订阅相关
    private IDisposable? _sourceViewSubscription;

    #endregion

    /// <summary>
    /// 当前控件的渲染区域大小
    /// </summary>
    public Size RenderSize => Bounds.Size;

    /// <summary>
    /// 源图片尺寸（来自 Bitmap.Size）
    /// </summary>
    public Size SourceSize { get; private set; }

    /// <summary>
    /// 当 SourceView 属性变化时同步订阅或取消订阅源控件的 Bitmap
    /// </summary>
    private static void SourceViewChanged(ImageBufferView sender, AvaloniaPropertyChangedEventArgs e)
    {
        // 取消旧的订阅
        sender._sourceViewSubscription?.Dispose();
        sender._sourceViewSubscription = null;

        if (e.NewValue is ImageBufferView sourceView)
        {
            // 立即同步当前 Bitmap
            sender.Bitmap = sourceView.Bitmap;
            sender.SourceSize = sourceView.SourceSize;

            // 订阅源控件的 Bitmap 变化
            sender._sourceViewSubscription = sourceView.GetObservable(BitmapProperty)
                .Subscribe(new AnonymousObserver<Bitmap?>(bitmap =>
                {
                    sender.Bitmap = bitmap;
                    sender.SourceSize = bitmap?.Size ?? sender.RenderSize;
                }));
        }
        else
        {
            sender.Bitmap = null;
        }
    }

    /// <summary>
    /// 当 ImageBuffer 属性变化时触发：开始解码或取消当前会话并释放旧资源（在无 SourceView 时释放）
    /// </summary>
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
            // 只有在没有 SourceView（即本控件拥有 Bitmap 的情况下）才释放旧 Bitmap
            if (sender.SourceView is null)
            {
                oldBitmap?.Dispose();
            }
        }
    }

    /// <summary>
    /// 当 Bitmap 属性改变时更新 SourceSize
    /// </summary>
    private static void BitmapChanged(ImageBufferView sender, AvaloniaPropertyChangedEventArgs e)
    {
        sender.SourceSize = e.NewValue is Bitmap bitmap ? bitmap.Size : sender.RenderSize;
    }

    /// <summary>
    /// 当 PixelBufferFormat / RawImageWidth / RawImageHeight 变化时，
    /// 若控件已附加且 ImageBuffer 有内容，则重新触发解码
    /// </summary>
    private static void RawFormatChanged(ImageBufferView sender, AvaloniaPropertyChangedEventArgs e)
    {
        sender.TryDecodeCurrentBuffer();
    }

    /// <summary>
    /// 当帧号变化时，若控件已附加且 ImageBuffer 有内容，则强制触发一次解码刷新。
    /// 适用于数据源直接修改 <see cref="ImageBuffer"/> 内部字节内容而不替换对象的场景。
    /// </summary>
    private static void FrameIndexChanged(ImageBufferView sender, AvaloniaPropertyChangedEventArgs e)
    {
        sender.TryDecodeCurrentBuffer();
    }

    /// <summary>
    /// 若控件已附加到可视树且 <see cref="ImageBuffer"/> 有有效内容，则触发一次解码刷新。
    /// </summary>
    private void TryDecodeCurrentBuffer()
    {
        if (this is { _isAttached: true, ImageBuffer: { Array: not null, Count: > 0 } buffer })
        {
            TryStartDecode(buffer);
        }
    }

    /// <summary>
    /// 取消当前解码会话，清理等待缓冲区并释放对应的 pooled buffer
    /// </summary>
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

    /// <summary>
    /// 获取或创建当前会话的 CancellationToken（线程安全）
    /// </summary>
    private CancellationToken GetOrCreateSessionToken()
    {
        lock (_sessionLock)
        {
            _sessionCts ??= new CancellationTokenSource();
            return _sessionCts.Token;
        }
    }

    /// <summary>
    /// 解码循环：从最新缓冲读取、限制并发、解码并在 UI 线程发布 Bitmap 更新。
    /// 使用 try/finally 确保所有退出路径（含取消、异常）都重置 _decoding 标志，
    /// 并检查 lost-wakeup 以防止新数据被遗漏。
    /// </summary>
    private void DecodeLoop()
    {
        var token = GetOrCreateSessionToken();

        try
        {
            while (_isAttached && !token.IsCancellationRequested)
            {
                var buffer = Interlocked.Exchange(ref _latestBuffer, null);
                var length = Interlocked.Exchange(ref _latestBufferLength, 0);

                if (buffer is null || length == 0)
                {
                    return;
                }

                Bitmap? newBitmap = null;
                try
                {
                    if (token.IsCancellationRequested)
                        return;

                    // 尝试获取信号量，超时则跳过此帧（避免积压）
                    if (!SDecodeSemaphore.Wait(0, token))
                        continue;

                    try
                    {
                        if (token.IsCancellationRequested)
                            return;

                        // 使用 SkiaSharp 解码并预缩放
                        newBitmap = DecodeAndScaleBitmap(buffer, length);
                    }
                    finally
                    {
                        SDecodeSemaphore.Release();
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
                finally
                {
                    // buffer 统一在此处归还，避免双归还和泄漏
                    ArrayPool<byte>.Shared.Return(buffer);
                }

                if (newBitmap is null)
                    continue;

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
                    // 并避免释放来自 SourceView 的共享 Bitmap（比较引用）
                    if (oldBitmap is not null && !ReferenceEquals(oldBitmap, SourceView?.Bitmap))
                    {
                        if (oldBitmap is WriteableBitmap oldWriteable)
                            RecycleToBackBuffer(oldWriteable);
                        else
                            oldBitmap.Dispose();
                    }
                }, DispatcherPriority.Render);
            }
        }
        finally
        {
            // 所有退出路径都重置 _decoding 标志
            Volatile.Write(ref _decoding, 0);

            // lost-wakeup 检查：如果有新数据到达但 DecodeLoop 已退出，重新排队
            if (_isAttached && _latestBuffer is not null &&
                Interlocked.CompareExchange(ref _decoding, 1, 0) == 0)
            {
                ThreadPool.UnsafeQueueUserWorkItem(static ctrl => ctrl.DecodeLoop(), this, preferLocal: false);
            }
        }
    }

    /// <summary>
    /// 测量覆盖：根据 Bitmap 和 Stretch 计算所需大小
    /// </summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        return Bitmap is not null
            ? Stretch.CalculateSize(availableSize, SourceSize, StretchDirection)
            : base.MeasureOverride(availableSize);
    }

    /// <summary>
    /// 排列覆盖：根据 Bitmap 和 Stretch 计算最终大小
    /// </summary>
    protected override Size ArrangeOverride(Size finalSize)
    {
        return Bitmap is not null
            ? Stretch.CalculateSize(finalSize, SourceSize)
            : base.ArrangeOverride(finalSize);
    }

    /// <summary>
    /// 渲染逻辑：计算源/目标矩形并绘制 Bitmap 或默认背景
    /// </summary>
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

    /// <summary>
    /// 当控件附加到可视树时启动解码流程（如已有缓冲则立即开始）
    /// </summary>
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

    /// <summary>
    /// 当控件从可视树分离时取消会话并清理资源
    /// </summary>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _isAttached = false;
        CancelCurrentSession();

        // 清理 SourceView 订阅
        _sourceViewSubscription?.Dispose();
        _sourceViewSubscription = null;

        // 清理 Bitmap（仅当不是从 SourceView 复用的情况）
        if (SourceView is null)
        {
            var oldBitmap = Bitmap;
            Bitmap = null;
            oldBitmap?.Dispose();
        }
        else
        {
            Bitmap = null;
        }

        // 清理后台缓冲区
        ClearBackBuffer();

        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>
    /// 重置缓冲区状态，用于源图片分辨率变化时（如切换摄像头）
    /// 不用调用也会自动切换缓存
    /// </summary>
    public void ResetBuffers()
    {
        ClearBackBuffer();
    }
}