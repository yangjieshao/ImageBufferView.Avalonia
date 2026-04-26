using SkiaSharp;
using System;
using System.Buffers;

namespace ImageBufferView.Avalonia.Tests.Helpers;

/// <summary>
/// 测试用图像数据生成辅助类，提供编码格式（JPEG）和原始像素格式的字节数据。
/// </summary>
internal static class TestImageHelper
{
    /// <summary>
    /// 生成一个简单的 2x2 BGRA32 原始像素缓冲区（红色、绿色、蓝色、白色四个像素）。
    /// </summary>
    public static byte[] GenerateBgra32Pixels(int width = 2, int height = 2)
    {
        var buffer = new byte[width * height * 4];
        if (width >= 2 && height >= 2)
        {
            // 行 0: 红色(BGRA) RGBA, 绿色(BGRA)
            buffer[0] = 0; buffer[1] = 0; buffer[2] = 255; buffer[3] = 255; // 红: B=0 G=0 R=255 A=255
            buffer[4] = 0; buffer[5] = 255; buffer[6] = 0; buffer[7] = 255;   // 绿: B=0 G=255 R=0 A=255
            // 行 1: 蓝色(BGRA), 白色(BGRA)
            buffer[8] = 255; buffer[9] = 0; buffer[10] = 0; buffer[11] = 255;  // 蓝: B=255 G=0 R=0 A=255
            buffer[12] = 255; buffer[13] = 255; buffer[14] = 255; buffer[15] = 255; // 白: B=255 G=255 R=255 A=255
        }

        return buffer;
    }

    /// <summary>
    /// 生成指定尺寸的 Rgba32 原始像素缓冲区。
    /// </summary>
    public static byte[] GenerateRgba32Pixels(int width, int height)
    {
        var buffer = new byte[width * height * 4];
        for (var i = 0; i < buffer.Length; i += 4)
        {
            buffer[i] = 255;     // R
            buffer[i + 1] = 0;   // G
            buffer[i + 2] = 0;   // B
            buffer[i + 3] = 255; // A
        }

        return buffer;
    }

    /// <summary>
    /// 生成一个最小有效的 JPEG 图像字节数组（1x1 黑色像素）。
    /// 通过 SkiaSharp 编码生成，确保数据为有效的 JPEG 格式。
    /// </summary>
    public static unsafe byte[] GenerateJpegBytes(int width = 2, int height = 2)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        var span = new Span<byte>((void*)bitmap.GetPixels(), bitmap.ByteCount);
        span.Fill(128); // 灰色填充

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 80);
        return data.ToArray();
    }

    /// <summary>
    /// 生成指定尺寸的全部红色 BGRA32 像素缓冲。
    /// </summary>
    public static ArraySegment<byte> GenerateBgra32ArraySegment(int width, int height)
    {
        var length = width * height * 4;
        var buffer = ArrayPool<byte>.Shared.Rent(length);
        for (var i = 0; i < length; i += 4)
        {
            buffer[i] = 0;     // B
            buffer[i + 1] = 0; // G
            buffer[i + 2] = 255; // R
            buffer[i + 3] = 255; // A
        }

        return new ArraySegment<byte>(buffer, 0, length);
    }

    /// <summary>
    /// 生成指定尺寸的 Rgb565 原始像素缓冲（灰色）。
    /// </summary>
    public static byte[] GenerateRgb565Pixels(int width, int height)
    {
        var buffer = new byte[width * height * 2];
        for (var i = 0; i < buffer.Length; i += 2)
        {
            // 灰色：R=15(5bit) G=31(6bit) B=15(5bit) = 0x7BEF
            buffer[i] = 0xEF;
            buffer[i + 1] = 0x7B;
        }

        return buffer;
    }

    /// <summary>
    /// 生成无效的 JPEG 数据（全零字节）。
    /// </summary>
    public static byte[] GenerateInvalidImageBytes()
    {
        return new byte[100]; // 全零，无法被任何解码器解析
    }
}
