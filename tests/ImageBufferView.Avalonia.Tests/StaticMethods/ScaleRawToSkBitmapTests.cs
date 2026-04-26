using SkiaSharp;
using Xunit;

namespace ImageBufferView.Avalonia.Tests.StaticMethods;

/// <summary>
/// <see cref="ImageBufferView.ScaleRawToSkBitmap"/> 的单元测试，
/// 覆盖零拷贝缩放路径、回退路径及参数校验。
/// </summary>
public  class ScaleRawToSkBitmapTests
{
    /// <summary>
    /// Bgra32 源缩小 2x 后目标尺寸正确。
    /// </summary>
    [Fact]
    public  void ScaleDown2x_ProducesCorrectSize()
    {
        const int srcW = 32;
        const int srcH = 32;
        const int dstW = 16;
        const int dstH = 16;
        var buffer = new byte[srcW * srcH * 4];

        using var result = ImageBufferView.ScaleRawToSkBitmap(buffer, buffer.Length, PixelBufferFormat.Bgra32, srcW, srcH, dstW, dstH);
        Assert.NotNull(result);
        
        Assert.Equal(dstW, result.Width);
        Assert.Equal(dstH, result.Height);
        Assert.Equal(SKColorType.Bgra8888, result.ColorType);
        Assert.Equal(SKAlphaType.Premul, result.AlphaType);
    }

    /// <summary>
    /// 放大 2x 也应正常工作。
    /// </summary>
    [Fact]
    public  void ScaleUp2x_ProducesCorrectSize()
    {
        const int srcW = 8;
        const int srcH = 8;
        const int dstW = 16;
        const int dstH = 16;
        var buffer = new byte[srcW * srcH * 4];

        using var result = ImageBufferView.ScaleRawToSkBitmap(buffer, buffer.Length, PixelBufferFormat.Bgra32, srcW, srcH, dstW, dstH);
        Assert.NotNull(result);
        
        Assert.Equal(dstW, result.Width);
        Assert.Equal(dstH, result.Height);
    }

    /// <summary>
    /// 源尺寸 <= 0 应返回 null。
    /// </summary>
    [Theory]
    [InlineData(0, 10, 5, 5)]
    [InlineData(10, 0, 5, 5)]
    [InlineData(-1, 10, 5, 5)]
    public  void InvalidSourceDimensions_ReturnsNull(int sw, int sh, int dw, int dh)
    {
        var buffer = new byte[100];
        var result = ImageBufferView.ScaleRawToSkBitmap(buffer, buffer.Length, PixelBufferFormat.Bgra32, sw, sh, dw, dh);
        Assert.Null(result);
    }

    /// <summary>
    /// 目标尺寸 <= 0 应返回 null。
    /// </summary>
    [Theory]
    [InlineData(10, 10, 0, 5)]
    [InlineData(10, 10, 5, 0)]
    public  void InvalidTargetDimensions_ReturnsNull(int sw, int sh, int dw, int dh)
    {
        var buffer = new byte[100];
        var result = ImageBufferView.ScaleRawToSkBitmap(buffer, buffer.Length, PixelBufferFormat.Bgra32, sw, sh, dw, dh);
        Assert.Null(result);
    }

    /// <summary>
    /// 缓冲长度不足应返回 null。
    /// </summary>
    [Fact]
    public  void BufferTooSmall_ReturnsNull()
    {
        var buffer = new byte[10]; // 需要至少 16 (2x2x4)
        var result = ImageBufferView.ScaleRawToSkBitmap(buffer, buffer.Length, PixelBufferFormat.Bgra32, 2, 2, 1, 1);
        Assert.Null(result);
    }

    /// <summary>
    /// Encoded 格式（无法映射到 Skia ColorType）应返回 null。
    /// </summary>
    [Fact]
    public  void EncodedFormat_ReturnsNull()
    {
        var buffer = new byte[100];
        var result = ImageBufferView.ScaleRawToSkBitmap(buffer, buffer.Length, PixelBufferFormat.Encoded, 10, 10, 5, 5);
        Assert.Null(result);
    }

    /// <summary>
    /// Gray8 源缩放到 Bgra8888 目标。
    /// </summary>
    [Fact]
    public  void Gray8_ScaleDown_ProducesBgra8888()
    {
        const int srcW = 16;
        const int srcH = 16;
        const int dstW = 8;
        const int dstH = 8;
        var buffer = new byte[srcW * srcH]; // 1 byte/pixel

        using var result = ImageBufferView.ScaleRawToSkBitmap(buffer, buffer.Length, PixelBufferFormat.Gray8, srcW, srcH, dstW, dstH);
        Assert.NotNull(result);
        
        Assert.Equal(dstW, result.Width);
        Assert.Equal(dstH, result.Height);
        // 目标始终为 Bgra8888
        Assert.Equal(SKColorType.Bgra8888, result.ColorType);
    }

    /// <summary>
    /// Rgb565 源缩放到 Bgra8888 目标。
    /// </summary>
    [Fact]
    public  void Rgb565_ScaleDown_ProducesBgra8888()
    {
        const int srcW = 16;
        const int srcH = 16;
        const int dstW = 8;
        const int dstH = 8;
        var buffer = new byte[srcW * srcH * 2];

        using var result = ImageBufferView.ScaleRawToSkBitmap(buffer, buffer.Length, PixelBufferFormat.Rgb565, srcW, srcH, dstW, dstH);
        Assert.NotNull(result);
        
        Assert.Equal(dstW, result.Width);
        Assert.Equal(SKColorType.Bgra8888, result.ColorType);
    }
}
