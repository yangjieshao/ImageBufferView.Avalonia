using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Reactive;
using Avalonia.Threading;
using SkiaSharp;
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
        SourceViewProperty.Changed.AddClassHandler<ImageBufferView>(SourceViewChanged);
        PixelBufferFormatProperty.Changed.AddClassHandler<ImageBufferView>(RawFormatChanged);
        RawImageWidthProperty.Changed.AddClassHandler<ImageBufferView>(RawFormatChanged);
        RawImageHeightProperty.Changed.AddClassHandler<ImageBufferView>(RawFormatChanged);
    }

    public ImageBufferView() : base()
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
        AvaloniaProperty.Register<ImageBufferView, PixelBufferFormat>(nameof(PixelBufferFormat), PixelBufferFormat.Encoded);

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
    private WriteableBitmap? _backBuffer;        // 后台缓冲区（用于写入）
    private PixelSize _backBufferSize;
    private PixelFormat _backBufferFormat;
    private readonly Lock _backBufferLock = new();
    private PixelSize _lastDecodedSourceSize;    // 上一次解码的源图片尺寸（用于检测分辨率变化）

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
        if (sender._isAttached && sender.ImageBuffer is { Array: not null, Count: > 0 } buffer)
        {
            sender.TryStartDecode(buffer);
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
    /// 解码循环：从最新缓冲读取、限制并发、解码并在 UI 线程发布 Bitmap 更新
    /// </summary>
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
                // 并避免释放来自 SourceView 的共享 Bitmap（比较引用）
                if (oldBitmap is not null && !ReferenceEquals(oldBitmap, SourceView?.Bitmap))
                {
                    if (oldBitmap is WriteableBitmap oldWriteable)
                    {
                        RecycleToBackBuffer(oldWriteable);
                    }
                    else
                    {
                        // 非可回收的 Bitmap，直接释放以避免泄漏
                        oldBitmap.Dispose();
                    }
                }
            }, DispatcherPriority.Render);
        }

        Volatile.Write(ref _decoding, 0);
    }

    /// <summary>
    /// 测量覆盖：根据 Bitmap 和 Stretch 计算所需大小
    /// </summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        return Bitmap is not null ? Stretch.CalculateSize(availableSize, SourceSize, StretchDirection)
                                  : base.MeasureOverride(availableSize);
    }

    /// <summary>
    /// 排列覆盖：根据 Bitmap 和 Stretch 计算最终大小
    /// </summary>
    protected override Size ArrangeOverride(Size finalSize)
    {
        return Bitmap is not null ? Stretch.CalculateSize(finalSize, SourceSize)
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
    /// 将旧的 WriteableBitmap 回收到后台缓冲区（用于下次复用）
    /// </summary>
    private void RecycleToBackBuffer(WriteableBitmap bitmap)
    {
        if (!_cachedEnableOptimization)
        {
            // 未启用优化，不复用，直接释放
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
        WriteableBitmap? bitmapToDispose;

        lock (_backBufferLock)
        {
            bitmapToDispose = _backBuffer;
            _backBuffer = null;
            _backBufferSize = default;
            _lastDecodedSourceSize = default;
        }

        bitmapToDispose?.Dispose();
    }

    /// <summary>
    /// 重置缓冲区状态，用于源图片分辨率变化时（如切换摄像头）
    /// </summary>
    public void ResetBuffers()
    {
        ClearBackBuffer();
    }

    /// <summary>
    /// 使用 SkiaSharp 解码图片，并根据渲染区域进行预缩放优化，返回可复用的 WriteableBitmap 或 null。
    /// 对原始像素格式（非 Encoded）且无需缩放的情况，直接写入 WriteableBitmap 以跳过中间 SKBitmap 分配。
    /// </summary>
    private WriteableBitmap? DecodeAndScaleBitmap(byte[] buffer, int length)
    {
        if (_cachedPixelBufferFormat != PixelBufferFormat.Encoded)
        {
            return DecodeAndScaleRawBitmap(buffer, length);
        }

        // 编码格式（JPEG/PNG 等），使用 SkiaSharp 解码
        SKData? skData = null;
        SKBitmap? skBitmap = null;

        try
        {
            skData = SKData.CreateCopy(new ReadOnlySpan<byte>(buffer, 0, length));
            skBitmap = SKBitmap.Decode(skData);

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
            var enableOptimization = _cachedEnableOptimization;

            // 检测源图片分辨率是否发生变化（如切换摄像头）
            var lastSourceSize = _lastDecodedSourceSize;
            if (lastSourceSize != default && lastSourceSize != sourceSize)
            {
                // 分辨率变化，清理后台缓冲区
                lock (_backBufferLock)
                {
                    if (_backBuffer is not null && _backBufferSize != default)
                    {
                        // 将旧缓冲区标记为需要释放
                        var oldBuffer = _backBuffer;
                        _backBuffer = null;
                        _backBufferSize = default;
                        // 在 UI 线程释放
                        Dispatcher.UIThread.Post(() => oldBuffer?.Dispose(), DispatcherPriority.Background);
                    }
                }
            }
            _lastDecodedSourceSize = sourceSize;

            // 判断是否需要预缩放
            if (!enableOptimization || renderSize.Width <= 0 || renderSize.Height <= 0)
            {
                // 不缩放，直接转换为 Avalonia Bitmap
                return ConvertSkBitmapToAvaloniaWithReuse(skBitmap, enableOptimization);
            }

            // 计算缩放比例（使用缓存的值，避免跨线程访问）
            var scale = stretch.CalculateScaling(renderSize, new Size(sourceWidth, sourceHeight), stretchDirection);

            if (scale.X >= 1.0 && scale.Y >= 1.0)
            {
                // 图片小于或等于渲染区域，不需要预缩放（放大由 GPU 处理更高效）
                return ConvertSkBitmapToAvaloniaWithReuse(skBitmap, enableOptimization);
            }

            // 图片大于渲染区域，预先缩小以减少渲染负担
            var targetWidth = Math.Max(1, (int)(sourceWidth * scale.X));
            var targetHeight = Math.Max(1, (int)(sourceHeight * scale.Y));

            // 使用 SkiaSharp 高效缩放
            using var resizedBitmap = skBitmap.Resize(new SKImageInfo(targetWidth, targetHeight), SKFilterQuality.Medium);

            if (resizedBitmap is null)
            {
                return ConvertSkBitmapToAvaloniaWithReuse(skBitmap, enableOptimization);
            }

            // 对于缩放后的图片，目标尺寸是固定的（基于渲染区域），可以考虑复用
            return ConvertSkBitmapToAvaloniaWithReuse(resizedBitmap, enableOptimization);
        }
        finally
        {
            skBitmap?.Dispose();
            skData?.Dispose();
        }
    }

    /// <summary>
    /// 处理原始像素格式（非 Encoded）的解码与缩放路径。
    /// 无需缩放时直接写入 WriteableBitmap，跳过 SKBitmap 中间层；
    /// 需要缩放时借助 SKBitmap.Resize 完成后再写入 WriteableBitmap。
    /// </summary>
    /// <param name="buffer">包含原始像素数据的字节数组</param>
    /// <param name="length">有效字节数</param>
    private WriteableBitmap? DecodeAndScaleRawBitmap(byte[] buffer, int length)
    {
        var format = _cachedPixelBufferFormat;
        var imageWidth = _cachedRawImageWidth;
        var imageHeight = _cachedRawImageHeight;

        if (imageWidth <= 0 || imageHeight <= 0)
        {
            return null;
        }

        var sourceSize = new PixelSize(imageWidth, imageHeight);
        var renderSize = _cachedRenderSize;
        var stretch = _cachedStretch;
        var stretchDirection = _cachedStretchDirection;
        var enableOptimization = _cachedEnableOptimization;

        // 检测源图片分辨率是否发生变化（如切换摄像头）
        var lastSourceSize = _lastDecodedSourceSize;
        if (lastSourceSize != default && lastSourceSize != sourceSize)
        {
            lock (_backBufferLock)
            {
                if (_backBuffer is not null && _backBufferSize != default)
                {
                    var oldBackBuffer = _backBuffer;
                    _backBuffer = null;
                    _backBufferSize = default;
                    Dispatcher.UIThread.Post(() => oldBackBuffer?.Dispose(), DispatcherPriority.Background);
                }
            }
        }
        _lastDecodedSourceSize = sourceSize;

        // 判断是否需要预缩放
        var needsScale = enableOptimization && renderSize.Width > 0 && renderSize.Height > 0;
        var targetWidth = imageWidth;
        var targetHeight = imageHeight;

        if (needsScale)
        {
            var scale = stretch.CalculateScaling(renderSize, new Size(imageWidth, imageHeight), stretchDirection);
            if (scale.X >= 1.0 && scale.Y >= 1.0)
            {
                // 图片小于或等于渲染区域，不需要预缩放
                needsScale = false;
            }
            else
            {
                targetWidth = Math.Max(1, (int)(imageWidth * scale.X));
                targetHeight = Math.Max(1, (int)(imageHeight * scale.Y));
            }
        }

        if (!needsScale)
        {
            // 快速路径：直接将原始像素写入 WriteableBitmap，完全跳过 SKBitmap 分配
            return WriteRawDirectToWriteableBitmap(buffer, length, format, imageWidth, imageHeight, enableOptimization);
        }

        // 缩放路径：借助 SKBitmap 完成 resize，再写入 WriteableBitmap
        var skBitmap = CreateSkBitmapFromRaw(buffer, length, format, imageWidth, imageHeight);
        if (skBitmap is null)
        {
            return null;
        }

        try
        {
            using var resized = skBitmap.Resize(new SKImageInfo(targetWidth, targetHeight), SKFilterQuality.Medium);
            if (resized is null)
            {
                return ConvertSkBitmapToAvaloniaWithReuse(skBitmap, enableOptimization);
            }
            return ConvertSkBitmapToAvaloniaWithReuse(resized, enableOptimization);
        }
        finally
        {
            skBitmap.Dispose();
        }
    }

    /// <summary>
    /// 将原始像素缓冲区直接写入可复用的 WriteableBitmap，无需经过 SKBitmap 中间层。
    /// 适用于原始像素格式且无需预缩放的高频渲染场景（如相机实时预览），可避免每帧额外分配 SKBitmap。
    /// </summary>
    /// <param name="buffer">包含原始像素数据的字节数组</param>
    /// <param name="length">有效字节数</param>
    /// <param name="format">原始像素格式</param>
    /// <param name="imageWidth">图像宽度（像素）</param>
    /// <param name="imageHeight">图像高度（像素）</param>
    /// <param name="enableReuse">是否启用后台缓冲区复用</param>
    /// <returns>成功返回 WriteableBitmap（调用方负责最终释放），失败返回 null</returns>
    private WriteableBitmap? WriteRawDirectToWriteableBitmap(
        byte[] buffer, int length,
        PixelBufferFormat format, int imageWidth, int imageHeight,
        bool enableReuse)
    {
        // 计算原始缓冲区的期望字节数与目标 PixelFormat
        PixelFormat pixelFormat;
        int expectedLen;

        switch (format)
        {
            case PixelBufferFormat.Bgra32:
                pixelFormat = PixelFormat.Bgra8888;
                expectedLen = imageWidth * imageHeight * 4;
                break;
            case PixelBufferFormat.Rgba32:
                pixelFormat = PixelFormat.Rgba8888;
                expectedLen = imageWidth * imageHeight * 4;
                break;
            case PixelBufferFormat.Bgr24:
            case PixelBufferFormat.Rgb24:
                pixelFormat = PixelFormat.Bgra8888;
                expectedLen = imageWidth * imageHeight * 3;
                break;
            case PixelBufferFormat.Gray8:
                pixelFormat = PixelFormat.Bgra8888;
                expectedLen = imageWidth * imageHeight;
                break;
            default:
                return null;
        }

        if (length < expectedLen)
        {
            return null;
        }

        var currentSize = new PixelSize(imageWidth, imageHeight);
        WriteableBitmap? bitmap = null;

        if (enableReuse)
        {
            // 尝试从后台缓冲区获取可复用的 Bitmap
            lock (_backBufferLock)
            {
                if (_backBuffer is not null &&
                    _backBufferSize == currentSize &&
                    _backBufferFormat == pixelFormat)
                {
                    bitmap = _backBuffer;
                    _backBuffer = null;
                }
            }
        }

        bitmap ??= new WriteableBitmap(currentSize, new Vector(96, 96), pixelFormat, AlphaFormat.Premul);

        try
        {
            using var fb = bitmap.Lock();
            var dstRowBytes = fb.RowBytes;

            unsafe
            {
                fixed (byte* src = buffer)
                {
                    var dst = (byte*)fb.Address;

                    switch (format)
                    {
                        case PixelBufferFormat.Bgra32:
                        case PixelBufferFormat.Rgba32:
                        {
                            // 源行字节与目标行字节相同时，单次 MemoryCopy 最优
                            var srcRowBytes = imageWidth * 4;
                            if (dstRowBytes == srcRowBytes)
                            {
                                Buffer.MemoryCopy(src, dst, dstRowBytes * imageHeight, srcRowBytes * imageHeight);
                            }
                            else
                            {
                                // 逐行复制以兼容目标行对齐填充
                                for (var row = 0; row < imageHeight; row++)
                                {
                                    Buffer.MemoryCopy(
                                        src + row * srcRowBytes,
                                        dst + row * dstRowBytes,
                                        dstRowBytes,
                                        srcRowBytes);
                                }
                            }
                            break;
                        }

                        case PixelBufferFormat.Bgr24:
                        {
                            // BGR24 → BGRA8888：逐像素扩展，填充 Alpha=255
                            for (var row = 0; row < imageHeight; row++)
                            {
                                var srcRow = src + row * imageWidth * 3;
                                var dstRow = dst + row * dstRowBytes;
                                for (var col = 0; col < imageWidth; col++)
                                {
                                    dstRow[col * 4]     = srcRow[col * 3];     // B
                                    dstRow[col * 4 + 1] = srcRow[col * 3 + 1]; // G
                                    dstRow[col * 4 + 2] = srcRow[col * 3 + 2]; // R
                                    dstRow[col * 4 + 3] = 255;                 // A
                                }
                            }
                            break;
                        }

                        case PixelBufferFormat.Rgb24:
                        {
                            // RGB24 → BGRA8888：逐像素交换 R/B，填充 Alpha=255
                            for (var row = 0; row < imageHeight; row++)
                            {
                                var srcRow = src + row * imageWidth * 3;
                                var dstRow = dst + row * dstRowBytes;
                                for (var col = 0; col < imageWidth; col++)
                                {
                                    dstRow[col * 4]     = srcRow[col * 3 + 2]; // B（源 RGB24 的 R 通道）
                                    dstRow[col * 4 + 1] = srcRow[col * 3 + 1]; // G
                                    dstRow[col * 4 + 2] = srcRow[col * 3];     // R（源 RGB24 的 B 通道）
                                    dstRow[col * 4 + 3] = 255;                 // A
                                }
                            }
                            break;
                        }

                        case PixelBufferFormat.Gray8:
                        {
                            // Gray8 → BGRA8888：灰度值展开到三通道，填充 Alpha=255
                            for (var row = 0; row < imageHeight; row++)
                            {
                                var srcRow = src + row * imageWidth;
                                var dstRow = dst + row * dstRowBytes;
                                for (var col = 0; col < imageWidth; col++)
                                {
                                    var gray = srcRow[col];
                                    dstRow[col * 4]     = gray; // B
                                    dstRow[col * 4 + 1] = gray; // G
                                    dstRow[col * 4 + 2] = gray; // R
                                    dstRow[col * 4 + 3] = 255;  // A
                                }
                            }
                            break;
                        }
                    }
                }
            }

            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            return null;
        }
    }

    /// <summary>
    /// 将原始像素缓冲区转换为 SKBitmap。
    /// 对于有 Alpha 通道的格式（BGRA32/RGBA32/Gray8）直接内存复制；
    /// 对于无 Alpha 通道的格式（BGR24/RGB24），逐像素转换为 BGRA32。
    /// </summary>
    /// <param name="buffer">包含原始像素数据的字节数组</param>
    /// <param name="length">有效字节数</param>
    /// <param name="format">原始像素格式</param>
    /// <param name="imageWidth">图像宽度（像素）</param>
    /// <param name="imageHeight">图像高度（像素）</param>
    /// <returns>成功返回 SKBitmap（调用方负责 Dispose），失败返回 null</returns>
    private static SKBitmap? CreateSkBitmapFromRaw(
        byte[] buffer, int length,
        PixelBufferFormat format, int imageWidth, int imageHeight)
    {
        if (imageWidth <= 0 || imageHeight <= 0)
        {
            return null;
        }

        switch (format)
        {
            case PixelBufferFormat.Bgra32:
            {
                var expectedLen = imageWidth * imageHeight * 4;
                if (length < expectedLen)
                {
                    return null;
                }

                var bitmap = new SKBitmap(new SKImageInfo(imageWidth, imageHeight, SKColorType.Bgra8888, SKAlphaType.Premul));
                unsafe
                {
                    fixed (byte* src = buffer)
                    {
                        Buffer.MemoryCopy(src, (void*)bitmap.GetPixels(), expectedLen, expectedLen);
                    }
                }
                return bitmap;
            }

            case PixelBufferFormat.Rgba32:
            {
                var expectedLen = imageWidth * imageHeight * 4;
                if (length < expectedLen)
                {
                    return null;
                }

                var bitmap = new SKBitmap(new SKImageInfo(imageWidth, imageHeight, SKColorType.Rgba8888, SKAlphaType.Premul));
                unsafe
                {
                    fixed (byte* src = buffer)
                    {
                        Buffer.MemoryCopy(src, (void*)bitmap.GetPixels(), expectedLen, expectedLen);
                    }
                }
                return bitmap;
            }

            case PixelBufferFormat.Bgr24:
            {
                var expectedLen = imageWidth * imageHeight * 3;
                if (length < expectedLen)
                {
                    return null;
                }

                var bitmap = new SKBitmap(new SKImageInfo(imageWidth, imageHeight, SKColorType.Bgra8888, SKAlphaType.Opaque));
                unsafe
                {
                    fixed (byte* src = buffer)
                    {
                        var dst = (byte*)bitmap.GetPixels();
                        var totalPixels = imageWidth * imageHeight;
                        for (var i = 0; i < totalPixels; i++)
                        {
                            dst[i * 4]     = src[i * 3];     // B
                            dst[i * 4 + 1] = src[i * 3 + 1]; // G
                            dst[i * 4 + 2] = src[i * 3 + 2]; // R
                            dst[i * 4 + 3] = 255;             // A
                        }
                    }
                }
                return bitmap;
            }

            case PixelBufferFormat.Rgb24:
            {
                var expectedLen = imageWidth * imageHeight * 3;
                if (length < expectedLen)
                {
                    return null;
                }

                var bitmap = new SKBitmap(new SKImageInfo(imageWidth, imageHeight, SKColorType.Bgra8888, SKAlphaType.Opaque));
                unsafe
                {
                    fixed (byte* src = buffer)
                    {
                        var dst = (byte*)bitmap.GetPixels();
                        var totalPixels = imageWidth * imageHeight;
                        for (var i = 0; i < totalPixels; i++)
                        {
                            dst[i * 4]     = src[i * 3 + 2]; // B（来自 R）
                            dst[i * 4 + 1] = src[i * 3 + 1]; // G
                            dst[i * 4 + 2] = src[i * 3];     // R（来自 B）
                            dst[i * 4 + 3] = 255;             // A
                        }
                    }
                }
                return bitmap;
            }

            case PixelBufferFormat.Gray8:
            {
                var expectedLen = imageWidth * imageHeight;
                if (length < expectedLen)
                {
                    return null;
                }

                var bitmap = new SKBitmap(new SKImageInfo(imageWidth, imageHeight, SKColorType.Gray8, SKAlphaType.Opaque));
                unsafe
                {
                    fixed (byte* src = buffer)
                    {
                        Buffer.MemoryCopy(src, (void*)bitmap.GetPixels(), expectedLen, expectedLen);
                    }
                }
                return bitmap;
            }

            default:
                return null;
        }
    }

    /// <summary>
    /// 将 SKBitmap 转换为 Avalonia 的 WriteableBitmap，支持后台缓冲复用（成功则返回 WriteableBitmap）
    /// </summary>
    /// <param name="skBitmap">源 SKBitmap</param>
    /// <param name="enableReuse">是否启用缓冲区复用</param>
    private WriteableBitmap? ConvertSkBitmapToAvaloniaWithReuse(
        SKBitmap skBitmap,
        bool enableReuse)
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

            // 启用优化时复用缓冲区，自动检测机制会在分辨率变化时重置
            var canReuse = enableReuse;

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

    /// <summary>
    /// 尝试开始解码：缓存渲染参数、租用缓冲区并排队后台解码任务
    /// </summary>
    private void TryStartDecode(ArraySegment<byte> buffer)
    {
        // 缓存当前渲染大小和缩放设置，供后台线程使用
        _cachedRenderSize = Bounds.Size;
        _cachedStretch = Stretch;
        _cachedStretchDirection = StretchDirection;
        _cachedEnableOptimization = EnableOptimization;
        _cachedPixelBufferFormat = PixelBufferFormat;
        _cachedRawImageWidth = RawImageWidth;
        _cachedRawImageHeight = RawImageHeight;

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

/// <summary>
/// 原始像素缓冲格式，用于接收 BGRA/RGBA/BGR/RGB/Gray 等未编码的图像流
/// </summary>
public enum PixelBufferFormat
{
    /// <summary>
    /// 编码图片格式（适用于 JPEG/PNG 等标准编码格式，默认值）
    /// </summary>
    Encoded,

    /// <summary>
    /// BGRA 32 位（每像素 4 字节：蓝、绿、红、透明，预乘 Alpha）
    /// </summary>
    Bgra32,

    /// <summary>
    /// RGBA 32 位（每像素 4 字节：红、绿、蓝、透明，预乘 Alpha）
    /// </summary>
    Rgba32,

    /// <summary>
    /// BGR 24 位（每像素 3 字节：蓝、绿、红，无 Alpha）
    /// </summary>
    Bgr24,

    /// <summary>
    /// RGB 24 位（每像素 3 字节：红、绿、蓝，无 Alpha）
    /// </summary>
    Rgb24,

    /// <summary>
    /// 灰度 8 位（每像素 1 字节）
    /// </summary>
    Gray8,
}