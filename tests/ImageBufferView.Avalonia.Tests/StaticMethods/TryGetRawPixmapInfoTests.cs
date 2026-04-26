using SkiaSharp;
using System;
using Xunit;

namespace ImageBufferView.Avalonia.Tests.StaticMethods;

/// <summary>
/// <see cref="ImageBufferView.TryGetRawPixmapInfo"/> 的单元测试，覆盖 23 种原始像素格式映射、
/// Encoded 格式拒绝及边界值行为。
/// </summary>
public  class TryGetRawPixmapInfoTests
{
    #region 有效格式映射验证

    /// <summary>
    /// 验证 Bgra32 格式映射为 Bgra8888 Premul，期望长度 = width * height * 4
    /// </summary>
    [Fact]
    public  void Bgra32_ReturnsBgra8888Premul()
    {
        var result = ImageBufferView.TryGetRawPixmapInfo(PixelBufferFormat.Bgra32, 10, 20, out var info, out var len);
        Assert.True(result);
        Assert.Equal(10 * 20 * 4, len);
        Assert.Equal(SKColorType.Bgra8888, info.ColorType);
        Assert.Equal(SKAlphaType.Premul, info.AlphaType);
        Assert.Equal(10, info.Width);
        Assert.Equal(20, info.Height);
    }

    /// <summary>
    /// 验证 Rgba32 格式映射为 Rgba8888 Premul，期望长度 = width * height * 4
    /// </summary>
    [Fact]
    public  void Rgba32_ReturnsRgba8888Premul()
    {
        var result = ImageBufferView.TryGetRawPixmapInfo(PixelBufferFormat.Rgba32, 5, 5, out var info, out var len);
        Assert.True(result);
        Assert.Equal(5 * 5 * 4, len);
        Assert.Equal(SKColorType.Rgba8888, info.ColorType);
        Assert.Equal(SKAlphaType.Premul, info.AlphaType);
    }

    /// <summary>
    /// 验证 Bgr32 格式映射为 Bgra8888 Opaque（X 填充字节被忽略）
    /// </summary>
    [Fact]
    public  void Bgr32_ReturnsBgra8888Opaque()
    {
        var result = ImageBufferView.TryGetRawPixmapInfo(PixelBufferFormat.Bgr32, 8, 8, out var info, out var len);
        Assert.True(result);
        Assert.Equal(8 * 8 * 4, len);
        Assert.Equal(SKColorType.Bgra8888, info.ColorType);
        Assert.Equal(SKAlphaType.Opaque, info.AlphaType);
    }

    /// <summary>
    /// 验证 Rgb32 格式映射为 Rgb888x Opaque
    /// </summary>
    [Fact]
    public  void Rgb32_ReturnsRgb888xOpaque()
    {
        var result = ImageBufferView.TryGetRawPixmapInfo(PixelBufferFormat.Rgb32, 4, 4, out var info, out var len);
        Assert.True(result);
        Assert.Equal(4 * 4 * 4, len);
        Assert.Equal(SKColorType.Rgb888x, info.ColorType);
        Assert.Equal(SKAlphaType.Opaque, info.AlphaType);
    }

    /// <summary>
    /// 验证 Rgb565 格式映射（2 字节/像素）
    /// </summary>
    [Fact]
    public  void Rgb565_ReturnsCorrectInfo()
    {
        var result = ImageBufferView.TryGetRawPixmapInfo(PixelBufferFormat.Rgb565, 16, 16, out var info, out var len);
        Assert.True(result);
        Assert.Equal(16 * 16 * 2, len);
        Assert.Equal(SKColorType.Rgb565, info.ColorType);
    }

    /// <summary>
    /// 验证 Gray8 格式映射（1 字节/像素）
    /// </summary>
    [Fact]
    public  void Gray8_ReturnsCorrectInfo()
    {
        var result = ImageBufferView.TryGetRawPixmapInfo(PixelBufferFormat.Gray8, 32, 16, out var info, out var len);
        Assert.True(result);
        Assert.Equal(32 * 16, len);
        Assert.Equal(SKColorType.Gray8, info.ColorType);
    }

    /// <summary>
    /// 验证 Alpha8 格式映射
    /// </summary>
    [Fact]
    public  void Alpha8_ReturnsCorrectInfo()
    {
        var result = ImageBufferView.TryGetRawPixmapInfo(PixelBufferFormat.Alpha8, 10, 10, out var info, out var len);
        Assert.True(result);
        Assert.Equal(100, len);
        Assert.Equal(SKColorType.Alpha8, info.ColorType);
        Assert.Equal(SKAlphaType.Premul, info.AlphaType);
    }

    /// <summary>
    /// 验证 Argb4444 格式映射（2 字节/像素）
    /// </summary>
    [Fact]
    public  void Argb4444_ReturnsCorrectInfo()
    {
        var result = ImageBufferView.TryGetRawPixmapInfo(PixelBufferFormat.Argb4444, 8, 8, out var info, out var len);
        Assert.True(result);
        Assert.Equal(8 * 8 * 2, len);
        Assert.Equal(SKColorType.Argb4444, info.ColorType);
        Assert.Equal(SKAlphaType.Premul, info.AlphaType);
    }

    /// <summary>
    /// 验证所有 HDR 10bit 格式映射
    /// </summary>
    [Theory]
    [InlineData(PixelBufferFormat.Rgba1010102, SKColorType.Rgba1010102, SKAlphaType.Premul)]
    [InlineData(PixelBufferFormat.Bgra1010102, SKColorType.Bgra1010102, SKAlphaType.Premul)]
    [InlineData(PixelBufferFormat.Rgb101010X, SKColorType.Rgb101010x, SKAlphaType.Opaque)]
    [InlineData(PixelBufferFormat.Bgr101010X, SKColorType.Bgr101010x, SKAlphaType.Opaque)]
    public  void Hdr10BitFormats_ReturnCorrectInfo(PixelBufferFormat format, SKColorType expectedCT, SKAlphaType expectedAT)
    {
        var result = ImageBufferView.TryGetRawPixmapInfo(format, 4, 4, out var info, out var len);
        Assert.True(result);
        Assert.Equal(4 * 4 * 4, len);
        Assert.Equal(expectedCT, info.ColorType);
        Assert.Equal(expectedAT, info.AlphaType);
    }

    /// <summary>
    /// 验证浮点格式映射
    /// </summary>
    [Theory]
    [InlineData(PixelBufferFormat.RgbaF16, 128, SKColorType.RgbaF16)]
    [InlineData(PixelBufferFormat.RgbaF16Clamped, 128, SKColorType.RgbaF16Clamped)]
    [InlineData(PixelBufferFormat.RgbaF32, 256, SKColorType.RgbaF32)]
    [InlineData(PixelBufferFormat.AlphaF16, 32, SKColorType.AlphaF16)]
    [InlineData(PixelBufferFormat.RgF16, 64, SKColorType.RgF16)]
    public  void FloatFormats_ReturnCorrectInfo(PixelBufferFormat format, int expectedBytes, SKColorType expectedCT)
    {
        var result = ImageBufferView.TryGetRawPixmapInfo(format, 4, 4, out var info, out var len);
        Assert.True(result);
        Assert.Equal(expectedBytes, len);
        Assert.Equal(expectedCT, info.ColorType);
    }

    /// <summary>
    /// 验证 16 位整型格式映射
    /// </summary>
    [Theory]
    [InlineData(PixelBufferFormat.Alpha16, 32, SKAlphaType.Premul)]
    [InlineData(PixelBufferFormat.Rg1616, 64, SKAlphaType.Opaque)]
    [InlineData(PixelBufferFormat.Rgba16161616, 128, SKAlphaType.Premul)]
    public  void Int16Formats_ReturnCorrectInfo(PixelBufferFormat format, int expectedBytes, SKAlphaType expectedAT)
    {
        var result = ImageBufferView.TryGetRawPixmapInfo(format, 4, 4, out var info, out var len);
        Assert.True(result);
        Assert.Equal(expectedBytes, len);
        Assert.Equal(expectedAT, info.AlphaType);
    }

    /// <summary>
    /// 验证 sRGBA 和 RG 双通道格式
    /// </summary>
    [Theory]
    [InlineData(PixelBufferFormat.Srgba8888, SKColorType.Srgba8888, 64)]
    [InlineData(PixelBufferFormat.Rg88, SKColorType.Rg88, 32)]
    [InlineData(PixelBufferFormat.R8Unorm, SKColorType.R8Unorm, 16)]
    public  void MiscFormats_ReturnCorrectInfo(PixelBufferFormat format, SKColorType expectedCT, int expectedBytes)
    {
        var result = ImageBufferView.TryGetRawPixmapInfo(format, 4, 4, out var info, out var len);
        Assert.True(result);
        Assert.Equal(expectedBytes, len);
        Assert.Equal(expectedCT, info.ColorType);
    }

    #endregion

    #region 不支持格式

    /// <summary>
    /// Encoded 格式应返回 false（需走 SkiaSharp 解码路径）
    /// </summary>
    [Fact]
    public  void Encoded_ReturnsFalse()
    {
        var result = ImageBufferView.TryGetRawPixmapInfo(PixelBufferFormat.Encoded, 100, 100, out _, out _);
        Assert.False(result);
    }

    #endregion

    #region 边界值验证

    /// <summary>
    /// 宽度为零时应触发 OverflowException（checked 乘法溢出）
    /// </summary>
    [Fact]
    public  void ZeroWidth_StillReturnsTrue_ZeroLength()
    {
        // checked(0 * height * bpp) == 0，所以不会溢出
        var result = ImageBufferView.TryGetRawPixmapInfo(PixelBufferFormat.Bgra32, 0, 10, out var info, out var len);
        Assert.True(result);
        Assert.Equal(0, len);
        Assert.Equal(0, info.Width);
        Assert.Equal(10, info.Height);
    }

    /// <summary>
    /// 极大尺寸应触发 OverflowException
    /// </summary>
    [Fact]
    public  void ExtremeDimensions_ThrowsOverflowException()
    {
        // width * height * 4 超出 int.MaxValue
        Assert.Throws<OverflowException>(() =>
            ImageBufferView.TryGetRawPixmapInfo(PixelBufferFormat.Bgra32, 100000, 100000, out _, out _));
    }

    /// <summary>
    /// 1x1 最小合法尺寸正常返回
    /// </summary>
    [Fact]
    public  void MinimumDimensions_ReturnsCorrectInfo()
    {
        var result = ImageBufferView.TryGetRawPixmapInfo(PixelBufferFormat.Gray8, 1, 1, out var info, out var len);
        Assert.True(result);
        Assert.Equal(1, len);
        Assert.Equal(1, info.Width);
        Assert.Equal(1, info.Height);
    }

    #endregion
}
