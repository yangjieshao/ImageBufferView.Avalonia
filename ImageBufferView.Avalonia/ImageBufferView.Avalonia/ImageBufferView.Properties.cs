using System;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace ImageBufferView.Avalonia;

public partial class ImageBufferView
{
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
        private set => SetValue(BitmapProperty, value);
    }

    public static readonly StyledProperty<ImageBufferView?> SourceViewProperty =
        AvaloniaProperty.Register<ImageBufferView, ImageBufferView?>(nameof(SourceView));

    public ImageBufferView? SourceView
    {
        get => GetValue(SourceViewProperty);
        set => SetValue(SourceViewProperty, value);
    }

    public static readonly StyledProperty<IBrush?> DefaultBackgroundProperty =
        AvaloniaProperty.Register<ImageBufferView, IBrush?>(nameof(DefaultBackground));

    public IBrush? DefaultBackground
    {
        get => GetValue(DefaultBackgroundProperty);
        set => SetValue(DefaultBackgroundProperty, value);
    }

    public static readonly StyledProperty<BitmapInterpolationMode> InterpolationModeProperty =
        AvaloniaProperty.Register<ImageBufferView, BitmapInterpolationMode>(
            nameof(InterpolationMode), BitmapInterpolationMode.MediumQuality);

    public BitmapInterpolationMode InterpolationMode
    {
        get => GetValue(InterpolationModeProperty);
        set => SetValue(InterpolationModeProperty, value);
    }

    public static readonly StyledProperty<bool> EnableOptimizationProperty =
        AvaloniaProperty.Register<ImageBufferView, bool>(nameof(EnableOptimization), true);

    public bool EnableOptimization
    {
        get => GetValue(EnableOptimizationProperty);
        set => SetValue(EnableOptimizationProperty, value);
    }

    public static readonly StyledProperty<PixelBufferFormat> PixelBufferFormatProperty =
        AvaloniaProperty.Register<ImageBufferView, PixelBufferFormat>(nameof(PixelBufferFormat),
            PixelBufferFormat.Encoded);

    public static readonly StyledProperty<ImageRotation> RotationProperty =
        AvaloniaProperty.Register<ImageBufferView, ImageRotation>(nameof(Rotation), ImageRotation.Rotate0);

    public static readonly StyledProperty<bool> FlipHorizontalProperty =
        AvaloniaProperty.Register<ImageBufferView, bool>(nameof(FlipHorizontal), false);

    public static readonly StyledProperty<bool> FlipVerticalProperty =
        AvaloniaProperty.Register<ImageBufferView, bool>(nameof(FlipVertical), false);

    public PixelBufferFormat PixelBufferFormat
    {
        get => GetValue(PixelBufferFormatProperty);
        set => SetValue(PixelBufferFormatProperty, value);
    }

    public ImageRotation Rotation
    {
        get => GetValue(RotationProperty);
        set => SetValue(RotationProperty, value);
    }

    public bool FlipHorizontal
    {
        get => GetValue(FlipHorizontalProperty);
        set => SetValue(FlipHorizontalProperty, value);
    }

    public bool FlipVertical
    {
        get => GetValue(FlipVerticalProperty);
        set => SetValue(FlipVerticalProperty, value);
    }

    public static readonly StyledProperty<int> RawImageWidthProperty =
        AvaloniaProperty.Register<ImageBufferView, int>(nameof(RawImageWidth), 0);

    public int RawImageWidth
    {
        get => GetValue(RawImageWidthProperty);
        set => SetValue(RawImageWidthProperty, value);
    }

    public static readonly StyledProperty<int> RawImageHeightProperty =
        AvaloniaProperty.Register<ImageBufferView, int>(nameof(RawImageHeight), 0);

    public int RawImageHeight
    {
        get => GetValue(RawImageHeightProperty);
        set => SetValue(RawImageHeightProperty, value);
    }

    public static readonly StyledProperty<long> FrameIndexProperty =
        AvaloniaProperty.Register<ImageBufferView, long>(nameof(FrameIndex), 0L);

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
}
