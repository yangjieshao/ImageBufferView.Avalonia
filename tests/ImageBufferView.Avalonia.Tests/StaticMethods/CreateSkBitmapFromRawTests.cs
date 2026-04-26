using SkiaSharp;
using Xunit;

namespace ImageBufferView.Avalonia.Tests.StaticMethods;

/// <summary>
/// <see cref="ImageBufferView.CreateSkBitmapFromRaw"/> 的单元测试，
/// 覆盖有效参数创建、无效参数拒绝及各格式像素数据验证。
/// </summary>
public  class CreateSkBitmapFromRawTests
{
    /// <summary>
    /// 使用有效的 Bgra32 数据创建 SKBitmap，像素尺寸应匹配输入。
    /// </summary>
    [Fact]
    public  void ValidBgra32Data_CreatesCorrectBitmap()
    {
        var width = 4;
        var height = 4;
        var buffer = new byte[width * height * 4];
        // 填充已知像素值：红色 BGRA
        for (var i = 0; i < buffer.Length; i += 4)
        {
            buffer[i] = 0;     // B
            buffer[i + 1] = 0; // G
            buffer[i + 2] = 255; // R
            buffer[i + 3] = 255; // A
        }

        var bitmap = ImageBufferView.CreateSkBitmapFromRaw(buffer, buffer.Length, PixelBufferFormat.Bgra32, width, height);
        Assert.NotNull(bitmap);
        using (bitmap)
        {
            Assert.Equal(width, bitmap.Width);
            Assert.Equal(height, bitmap.Height);
            Assert.Equal(SKColorType.Bgra8888, bitmap.ColorType);
        }
    }

    /// <summary>
    /// 使用 Rgba32 格式创建位图，映射为 Rgba8888。
    /// </summary>
    [Fact]
    public  void ValidRgba32Data_CreatesBitmap()
    {
        const int w = 2;
        const int h = 2;
        var buffer = new byte[w * h * 4];

        var bitmap = ImageBufferView.CreateSkBitmapFromRaw(buffer, buffer.Length, PixelBufferFormat.Rgba32, w, h);
        Assert.NotNull(bitmap);
        try
        {
            Assert.Equal(w, bitmap.Width);
            Assert.Equal(SKColorType.Rgba8888, bitmap.ColorType);
        }
        finally
        {
            bitmap.Dispose();
        }
    }

    /// <summary>
    /// 使用 Gray8 格式创建位图。
    /// </summary>
    [Fact]
    public  void Gray8_CreatesSingleChannelBitmap()
    {
        const int w = 128;
        const int h = 128;
        var buffer = new byte[w * h];

        var bitmap = ImageBufferView.CreateSkBitmapFromRaw(buffer, buffer.Length, PixelBufferFormat.Gray8, w, h);
        Assert.NotNull(bitmap);
        try
        {
            Assert.Equal(w, bitmap.Width);
            Assert.Equal(h, bitmap.Height);
            Assert.Equal(SKColorType.Gray8, bitmap.ColorType);
        }
        finally
        {
            bitmap.Dispose();
        }
    }

    /// <summary>
    /// 使用 Rgb565 格式创建位图。
    /// </summary>
    [Fact]
    public  void Rgb565_CreatesBitmap()
    {
        const int w = 16;
        const int h = 16;
        var buffer = new byte[w * h * 2];

        var bitmap = ImageBufferView.CreateSkBitmapFromRaw(buffer, buffer.Length, PixelBufferFormat.Rgb565, w, h);
        Assert.NotNull(bitmap);
        try
        {
            Assert.Equal(w, bitmap.Width);
            Assert.Equal(SKColorType.Rgb565, bitmap.ColorType);
        }
        finally
        {
            bitmap.Dispose();
        }
    }

    /// <summary>
    /// 宽度为零应返回 null。
    /// </summary>
    [Fact]
    public  void ZeroWidth_ReturnsNull()
    {
        var buffer = new byte[100];
        var bitmap = ImageBufferView.CreateSkBitmapFromRaw(buffer, buffer.Length, PixelBufferFormat.Bgra32, 0, 10);
        Assert.Null(bitmap);
    }

    /// <summary>
    /// 高度为零应返回 null。
    /// </summary>
    [Fact]
    public  void ZeroHeight_ReturnsNull()
    {
        var buffer = new byte[100];
        var bitmap = ImageBufferView.CreateSkBitmapFromRaw(buffer, buffer.Length, PixelBufferFormat.Bgra32, 10, 0);
        Assert.Null(bitmap);
    }

    /// <summary>
    /// 缓冲长度不足应返回 null。
    /// </summary>
    [Fact]
    public  void BufferTooSmall_ReturnsNull()
    {
        var buffer = new byte[10]; // 需要至少 16 字节 (2x2x4)
        var bitmap = ImageBufferView.CreateSkBitmapFromRaw(buffer, buffer.Length, PixelBufferFormat.Bgra32, 2, 2);
        Assert.Null(bitmap);
    }

    /// <summary>
    /// Encoded 格式应返回 null（无法直接创建 SKBitmap）。
    /// </summary>
    [Fact]
    public  void EncodedFormat_ReturnsNull()
    {
        var buffer = new byte[100];
        var bitmap = ImageBufferView.CreateSkBitmapFromRaw(buffer, buffer.Length, PixelBufferFormat.Encoded, 5, 5);
        Assert.Null(bitmap);
    }

    /// <summary>
    /// RgbaF16 浮点格式创建位图。
    /// </summary>
    [Fact]
    public  void RgbaF16_CreatesFloatBitmap()
    {
        const int w = 2;
        const int h = 2;
        var buffer = new byte[w * h * 8];

        var bitmap = ImageBufferView.CreateSkBitmapFromRaw(buffer, buffer.Length, PixelBufferFormat.RgbaF16, w, h);
        Assert.NotNull(bitmap);
        try
        {
            Assert.Equal(SKColorType.RgbaF16, bitmap.ColorType);
        }
        finally
        {
            bitmap.Dispose();
        }
    }

    /// <summary>
    /// R8Unorm 格式创建位图。
    /// </summary>
    [Fact]
    public  void R8Unorm_CreatesBitmap()
    {
        const int w = 4;
        const int h = 4;
        var buffer = new byte[w * h];

        var bitmap = ImageBufferView.CreateSkBitmapFromRaw(buffer, buffer.Length, PixelBufferFormat.R8Unorm, w, h);
        Assert.NotNull(bitmap);
        try
        {
            Assert.Equal(SKColorType.R8Unorm, bitmap.ColorType);
        }
        finally
        {
            bitmap.Dispose();
        }
    }
}
