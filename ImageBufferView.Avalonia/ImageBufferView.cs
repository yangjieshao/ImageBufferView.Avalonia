using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Reactive;
using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading;
// ReSharper disable MemberCanBePrivate.Global

[assembly: InternalsVisibleTo("ImageBufferView.Avalonia.Tests")]

namespace ImageBufferView.Avalonia;

/// <summary>
/// 表示一个可从字节缓冲解码并显示的图像控件，包含预缩放和缓冲复用优化。
/// </summary>
public partial class ImageBufferView : Control
{
    /// <summary>
    /// 并发解码信号量（默认基于 CPU 核心数，可通过静态/实例属性调整）
    /// </summary>
    private static SemaphoreSlim _sDecodeSemaphore =
        new(Environment.ProcessorCount, Environment.ProcessorCount);

    // 用于记录当前最大并发数的静态字段
    private static int _sMaxDecodeConcurrency = Environment.ProcessorCount;
    private static readonly SemaphoreSlim SMaxDecodeConcurrencySlim =
        new(1, 1);

    /// <summary>
    /// 全局默认的最大并发解码数。设置此值会重新创建用于解码的信号量（线程安全）。
    /// </summary>
    public static int MaxDecodeConcurrency
    {
        get
        {
            SMaxDecodeConcurrencySlim.Wait();
            try
            {
                return _sMaxDecodeConcurrency;
            }
            finally
            {
                SMaxDecodeConcurrencySlim.Release();
            }
        }
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            SMaxDecodeConcurrencySlim.Wait();
            try
            {
                if (_sMaxDecodeConcurrency == value) return;

                var newSem = new SemaphoreSlim(value, value);
                var prev = Interlocked.Exchange(ref _sDecodeSemaphore, newSem);
                _sMaxDecodeConcurrency = value;
                prev.Dispose();
            }
            catch 
            {
                // 在极端情况下可能会有并发访问导致的异常，这里捕获并忽略，保持旧的信号量继续工作
            }
            finally
            {
                SMaxDecodeConcurrencySlim.Release();
            }
        }
    }

    static ImageBufferView()
    {
        AffectsRender<ImageBufferView>(BitmapProperty, StretchProperty, StretchDirectionProperty,
            DefaultBackgroundProperty, RotationProperty, FlipHorizontalProperty, FlipVerticalProperty);
        AffectsMeasure<ImageBufferView>(BitmapProperty, StretchProperty, StretchDirectionProperty,
            DefaultBackgroundProperty, RotationProperty, FlipHorizontalProperty, FlipVerticalProperty);
        AffectsArrange<ImageBufferView>(BitmapProperty, StretchProperty, StretchDirectionProperty,
            DefaultBackgroundProperty, RotationProperty, FlipHorizontalProperty, FlipVerticalProperty);

        BitmapProperty.Changed.AddClassHandler<ImageBufferView>(BitmapChanged);
        ImageBufferProperty.Changed.AddClassHandler<ImageBufferView>(ImageBufferChanged);
        InterpolationModeProperty.Changed.AddClassHandler<ImageBufferView>(InterpolationModeChanged);
        SourceViewProperty.Changed.AddClassHandler<ImageBufferView>(SourceViewChanged);
        PixelBufferFormatProperty.Changed.AddClassHandler<ImageBufferView>(DecodeBuffer);
        RawImageWidthProperty.Changed.AddClassHandler<ImageBufferView>(DecodeBuffer);
        RawImageHeightProperty.Changed.AddClassHandler<ImageBufferView>(DecodeBuffer);
        FrameIndexProperty.Changed.AddClassHandler<ImageBufferView>(DecodeBuffer);
        RotationProperty.Changed.AddClassHandler<ImageBufferView>(FlipRotationChanged);
        FlipHorizontalProperty.Changed.AddClassHandler<ImageBufferView>(FlipRotationChanged);
        FlipVerticalProperty.Changed.AddClassHandler<ImageBufferView>(FlipRotationChanged);
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
    /// 图片旋转（以度为单位，0/90/180/270）
    /// </summary>
    public static readonly StyledProperty<ImageRotation> RotationProperty =
        AvaloniaProperty.Register<ImageBufferView, ImageRotation>(nameof(Rotation), ImageRotation.Rotate0);

    /// <summary>
    /// 水平镜像
    /// </summary>
    public static readonly StyledProperty<bool> FlipHorizontalProperty =
        AvaloniaProperty.Register<ImageBufferView, bool>(nameof(FlipHorizontal), false);

    /// <summary>
    /// 垂直镜像
    /// </summary>
    public static readonly StyledProperty<bool> FlipVerticalProperty =
        AvaloniaProperty.Register<ImageBufferView, bool>(nameof(FlipVertical), false);

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
    /// 图片旋转（以度为单位，0/90/180/270）
    /// </summary>
    public ImageRotation Rotation
    {
        get => GetValue(RotationProperty);
        set => SetValue(RotationProperty, value);
    }

    /// <summary>
    /// 水平镜像
    /// </summary>
    public bool FlipHorizontal
    {
        get => GetValue(FlipHorizontalProperty);
        set => SetValue(FlipHorizontalProperty, value);
    }

    /// <summary>
    /// 垂直镜像
    /// </summary>
    public bool FlipVertical
    {
        get => GetValue(FlipVerticalProperty);
        set => SetValue(FlipVerticalProperty, value);
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
    private PixelSizeBox? _lastDecodedSourceSizeBox; // 上一次解码的源图片尺寸（用于检测分辨率变化）

    // SourceView 订阅相关
    private IDisposable? _sourceViewSubscription;

    // 原始（未预缩放）源图片尺寸，用于布局计算以避免预缩放 ↔ 布局反馈振荡
    private Size _originalSourceSize;

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
            // 立即同步当前 Bitmap 及原始源分辨率
            sender.Bitmap = sourceView.Bitmap;
            sender.SourceSize = sourceView.SourceSize;
            sender._originalSourceSize = sourceView._originalSourceSize;

            // 订阅源控件的 Bitmap 变化
            sender._sourceViewSubscription = sourceView.GetObservable(BitmapProperty)
                .Subscribe(new AnonymousObserver<Bitmap?>(bitmap =>
                {
                    sender.Bitmap = bitmap;
                    sender.SourceSize = bitmap?.Size ?? sender.RenderSize;
                    sender._originalSourceSize = sourceView._originalSourceSize;
                }));
        }
        else
        {
            sender.Bitmap = null;
            sender._originalSourceSize = default;
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
    /// 当 Bitmap 属性改变时更新 SourceSize 和 _originalSourceSize
    /// </summary>
    private static void BitmapChanged(ImageBufferView sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is Bitmap bitmap)
        {
            sender.SourceSize = bitmap.Size;
            // 从解码结果读取原始源分辨率（未预缩放），用于稳定布局
            var box = Volatile.Read(ref sender._lastDecodedSourceSizeBox);
            if (box is not null)
            {
                var ps = box.Value;
                sender._originalSourceSize = new Size(ps.Width, ps.Height);
            }
            else
            {
                sender._originalSourceSize = bitmap.Size;
            }
        }
        else
        {
            sender.SourceSize = sender.RenderSize;
            sender._originalSourceSize = default;
        }
    }

    private static void FlipRotationChanged(ImageBufferView sender, AvaloniaPropertyChangedEventArgs e)
    {
        // Flip Rotation affects rendering transform
        sender.InvalidateVisual();
    }

    /// <summary>
    /// 当帧号变化时，若控件已附加且 ImageBuffer 有内容，则强制触发一次解码刷新。
    /// 适用于数据源直接修改 <see cref="ImageBuffer"/> 内部字节内容而不替换对象的场景。
    /// </summary>
    private static void DecodeBuffer(ImageBufferView sender, AvaloniaPropertyChangedEventArgs e)
    {
        sender.TryDecodeCurrentBuffer();
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
    /// 重置缓冲区状态，用于源图片分辨率变化时（如切换摄像头）
    /// 不用调用也会自动切换缓存
    /// </summary>
    public void ResetBuffers()
    {
        ClearBackBuffer();
    }
}