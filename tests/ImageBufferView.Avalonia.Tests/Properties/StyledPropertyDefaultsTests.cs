using Avalonia.Media;
using Avalonia.Media.Imaging;
using Xunit;

namespace ImageBufferView.Avalonia.Tests.Properties;

/// <summary>
/// 验证 <see cref="ImageBufferView"/> 所有 StyledProperty 的默认值。
/// </summary>
public  class StyledPropertyDefaultsTests
{
    /// <summary>
    /// 创建新的控件实例，所有属性应为文档定义的默认值。
    /// </summary>
    [Fact]
    public  void Stretch_DefaultIsNone()
    {
        var view = new ImageBufferView();
        Assert.Equal(Stretch.None, view.Stretch);
    }

    /// <summary>
    /// StretchDirection 默认值为 Both。
    /// </summary>
    [Fact]
    public  void StretchDirection_DefaultIsBoth()
    {
        var view = new ImageBufferView();
        Assert.Equal(StretchDirection.Both, view.StretchDirection);
    }

    /// <summary>
    /// ImageBuffer 默认值为 null。
    /// </summary>
    [Fact]
    public  void ImageBuffer_DefaultIsNull()
    {
        var view = new ImageBufferView();
        Assert.Null(view.ImageBuffer);
    }

    /// <summary>
    /// Bitmap 默认值为 null。
    /// </summary>
    [Fact]
    public  void Bitmap_DefaultIsNull()
    {
        var view = new ImageBufferView();
        Assert.Null(view.Bitmap);
    }

    /// <summary>
    /// SourceView 默认值为 null。
    /// </summary>
    [Fact]
    public  void SourceView_DefaultIsNull()
    {
        var view = new ImageBufferView();
        Assert.Null(view.SourceView);
    }

    /// <summary>
    /// DefaultBackground 默认值为 null。
    /// </summary>
    [Fact]
    public  void DefaultBackground_DefaultIsNull()
    {
        var view = new ImageBufferView();
        Assert.Null(view.DefaultBackground);
    }

    /// <summary>
    /// InterpolationMode 默认值为 MediumQuality。
    /// </summary>
    [Fact]
    public  void InterpolationMode_DefaultIsMediumQuality()
    {
        var view = new ImageBufferView();
        Assert.Equal(BitmapInterpolationMode.MediumQuality, view.InterpolationMode);
    }

    /// <summary>
    /// EnableOptimization 默认值为 true。
    /// </summary>
    [Fact]
    public  void EnableOptimization_DefaultIsTrue()
    {
        var view = new ImageBufferView();
        Assert.True(view.EnableOptimization);
    }

    /// <summary>
    /// PixelBufferFormat 默认值为 Encoded。
    /// </summary>
    [Fact]
    public  void PixelBufferFormat_DefaultIsEncoded()
    {
        var view = new ImageBufferView();
        Assert.Equal(PixelBufferFormat.Encoded, view.PixelBufferFormat);
    }

    /// <summary>
    /// Rotation 默认值为 Rotate0。
    /// </summary>
    [Fact]
    public  void Rotation_DefaultIsRotate0()
    {
        var view = new ImageBufferView();
        Assert.Equal(ImageRotation.Rotate0, view.Rotation);
    }

    /// <summary>
    /// FlipHorizontal 默认值为 false。
    /// </summary>
    [Fact]
    public  void FlipHorizontal_DefaultIsFalse()
    {
        var view = new ImageBufferView();
        Assert.False(view.FlipHorizontal);
    }

    /// <summary>
    /// FlipVertical 默认值为 false。
    /// </summary>
    [Fact]
    public  void FlipVertical_DefaultIsFalse()
    {
        var view = new ImageBufferView();
        Assert.False(view.FlipVertical);
    }

    /// <summary>
    /// RawImageWidth 默认值为 0。
    /// </summary>
    [Fact]
    public  void RawImageWidth_DefaultIsZero()
    {
        var view = new ImageBufferView();
        Assert.Equal(0, view.RawImageWidth);
    }

    /// <summary>
    /// RawImageHeight 默认值为 0。
    /// </summary>
    [Fact]
    public  void RawImageHeight_DefaultIsZero()
    {
        var view = new ImageBufferView();
        Assert.Equal(0, view.RawImageHeight);
    }

    /// <summary>
    /// FrameIndex 默认值为 0。
    /// </summary>
    [Fact]
    public  void FrameIndex_DefaultIsZero()
    {
        var view = new ImageBufferView();
        Assert.Equal(0L, view.FrameIndex);
    }

    /// <summary>
    /// 控件构造函数中会设置 BitmapInterpolationMode 到 RenderOptions。
    /// </summary>
    [Fact]
    public  void RenderOptionsInterpolationMode_IsMediumQuality()
    {
        var view = new ImageBufferView();
        var mode = RenderOptions.GetBitmapInterpolationMode(view);
        Assert.Equal(BitmapInterpolationMode.MediumQuality, mode);
    }
}
