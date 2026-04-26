using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;
using System;

namespace ImageBufferView.Avalonia;

public partial class ImageBufferView
{
    /// <summary>
    /// 尝试获取原始像素格式对应的 Skia 图像信息，仅适用于可直接映射到 Skia ColorType 的格式。
    /// SkiaSharp 3.x 增加了更多 ColorType 支持，可直接映射大部分常用格式。
    /// </summary>
    /// <param name="format">原始像素格式</param>
    /// <param name="width">图像宽度（像素）</param>
    /// <param name="height">图像高度（像素）</param>
    /// <param name="info">成功时输出对应的 <see cref="SKImageInfo"/></param>
    /// <param name="expectedLen">成功时输出期望的最小缓冲字节数</param>
    /// <returns>格式可直接映射时返回 <see langword="true"/>，否则 <see langword="false"/></returns>
    internal static bool TryGetRawPixmapInfo(
        PixelBufferFormat format,
        int width, int height,
        out SKImageInfo info,
        out int expectedLen)
    {
        info = default;
        expectedLen = 0;

        switch (format)
        {
            case PixelBufferFormat.Bgra32:
                // 内存布局：B G R A，与 Skia Bgra8888 完全一致
                // 约定 Bgra32 数据为预乘格式（调用方责任）
                expectedLen = checked(width * height * 4);
                info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
                return true;

            case PixelBufferFormat.Rgba32:
                // 内存布局：R G B A，与 Skia Rgba8888 完全一致
                // 约定 Rgba32 数据为预乘格式（调用方责任）
                expectedLen = checked(width * height * 4);
                info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
                return true;

            case PixelBufferFormat.Bgr32:
                // 内存布局：B G R X（X 为填充字节），视作 Bgra8888 Opaque（X 字节被忽略）
                expectedLen = checked(width * height * 4);
                info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque);
                return true;

            case PixelBufferFormat.Rgb32:
                // 内存布局：R G B X，与 Skia Rgb888x 完全一致
                expectedLen = checked(width * height * 4);
                info = new SKImageInfo(width, height, SKColorType.Rgb888x, SKAlphaType.Opaque);
                return true;

            case PixelBufferFormat.Rgb565:
                // 16bpp RGB565，Skia 原生支持
                expectedLen = checked(width * height * 2);
                info = new SKImageInfo(width, height, SKColorType.Rgb565, SKAlphaType.Opaque);
                return true;

            case PixelBufferFormat.Gray8:
                // 8bpp 灰度，Skia 原生支持
                expectedLen = checked(width * height);
                info = new SKImageInfo(width, height, SKColorType.Gray8, SKAlphaType.Opaque);
                return true;

            case PixelBufferFormat.Alpha8:
                // 8bpp 仅 Alpha 通道
                expectedLen = checked(width * height);
                info = new SKImageInfo(width, height, SKColorType.Alpha8, SKAlphaType.Premul);
                return true;

            case PixelBufferFormat.Argb4444:
                // 16bpp ARGB4444
                expectedLen = checked(width * height * 2);
                info = new SKImageInfo(width, height, SKColorType.Argb4444, SKAlphaType.Premul);
                return true;

            case PixelBufferFormat.Rgba1010102:
                // 32bpp RGBA 10-10-10-2，高动态范围格式
                expectedLen = checked(width * height * 4);
                info = new SKImageInfo(width, height, SKColorType.Rgba1010102, SKAlphaType.Premul);
                return true;

            case PixelBufferFormat.Bgra1010102:
                // 32bpp BGRA 10-10-10-2，高动态范围格式
                expectedLen = checked(width * height * 4);
                info = new SKImageInfo(width, height, SKColorType.Bgra1010102, SKAlphaType.Premul);
                return true;

            case PixelBufferFormat.Rgb101010X:
                // 32bpp RGB 10-10-10-x，高动态范围不透明格式
                expectedLen = checked(width * height * 4);
                info = new SKImageInfo(width, height, SKColorType.Rgb101010x, SKAlphaType.Opaque);
                return true;

            case PixelBufferFormat.Bgr101010X:
                // 32bpp BGR 10-10-10-x，高动态范围不透明格式
                expectedLen = checked(width * height * 4);
                info = new SKImageInfo(width, height, SKColorType.Bgr101010x, SKAlphaType.Opaque);
                return true;

            case PixelBufferFormat.Srgba8888:
                // 32bpp sRGBA，sRGB 色彩空间
                expectedLen = checked(width * height * 4);
                info = new SKImageInfo(width, height, SKColorType.Srgba8888, SKAlphaType.Premul);
                return true;

            case PixelBufferFormat.Rg88:
                // 16bpp RG 双通道
                expectedLen = checked(width * height * 2);
                info = new SKImageInfo(width, height, SKColorType.Rg88, SKAlphaType.Opaque);
                return true;

            case PixelBufferFormat.RgbaF16:
                // 64bpp RGBA 半精度浮点
                expectedLen = checked(width * height * 8);
                info = new SKImageInfo(width, height, SKColorType.RgbaF16, SKAlphaType.Premul);
                return true;

            case PixelBufferFormat.RgbaF16Clamped:
                // 64bpp RGBA 半精度浮点（值限制在 0.0-1.0）
                expectedLen = checked(width * height * 8);
                info = new SKImageInfo(width, height, SKColorType.RgbaF16Clamped, SKAlphaType.Premul);
                return true;

            case PixelBufferFormat.RgbaF32:
                // 128bpp RGBA 单精度浮点
                expectedLen = checked(width * height * 16);
                info = new SKImageInfo(width, height, SKColorType.RgbaF32, SKAlphaType.Premul);
                return true;

            case PixelBufferFormat.Alpha16:
                // 16bpp 仅 Alpha 通道
                expectedLen = checked(width * height * 2);
                info = new SKImageInfo(width, height, SKColorType.Alpha16, SKAlphaType.Premul);
                return true;

            case PixelBufferFormat.Rg1616:
                // 32bpp RG 双通道 16 位
                expectedLen = checked(width * height * 4);
                info = new SKImageInfo(width, height, SKColorType.Rg1616, SKAlphaType.Opaque);
                return true;

            case PixelBufferFormat.Rgba16161616:
                // 64bpp RGBA 每通道 16 位
                expectedLen = checked(width * height * 8);
                info = new SKImageInfo(width, height, SKColorType.Rgba16161616, SKAlphaType.Premul);
                return true;

            case PixelBufferFormat.AlphaF16:
                // 16bpp 半精度浮点 Alpha 通道
                expectedLen = checked(width * height * 2);
                info = new SKImageInfo(width, height, SKColorType.AlphaF16, SKAlphaType.Premul);
                return true;

            case PixelBufferFormat.RgF16:
                // 32bpp RG 双通道半精度浮点
                expectedLen = checked(width * height * 4);
                info = new SKImageInfo(width, height, SKColorType.RgF16, SKAlphaType.Opaque);
                return true;

            case PixelBufferFormat.R8Unorm:
                // 8bpp 单通道归一化格式
                expectedLen = checked(width * height);
                info = new SKImageInfo(width, height, SKColorType.R8Unorm, SKAlphaType.Opaque);
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// 将原始像素缓冲直接缩放到目标尺寸的 <see cref="SKBitmap"/>（输出格式：Bgra8888 Premul）。
    /// 通过 SKBitmap.InstallPixels 零拷贝包装原始缓冲区，再借助 Skia 的
    /// SKCanvas.DrawBitmap 完成缩放，避免分配全尺寸中间 <see cref="SKBitmap"/>。
    /// <para>
    /// SkiaSharp 3.x 支持更多 ColorType，包括 BGRA32/RGBA32/Bgr32/Rgb32/Rgb565/Gray8/
    /// Alpha8/Argb4444/Rgba1010102/Bgra1010102/Rgb101010x/Bgr101010x/Srgba8888/
    /// Rg88/RgbaF16/RgbaF16Clamped/RgbaF32/Alpha16/Rg1616/Rgba16161616/AlphaF16/RgF16/R8Unorm 等。
    /// 不支持的格式返回 <see langword="null"/>，调用方应回退到
    /// <see cref="CreateSkBitmapFromRaw"/> + Resize 路径。
    /// </para>
    /// </summary>
    /// <param name="buffer">包含原始像素数据的字节数组</param>
    /// <param name="length">有效字节数</param>
    /// <param name="format">原始像素格式</param>
    /// <param name="srcWidth">源图像宽度（像素）</param>
    /// <param name="srcHeight">源图像高度（像素）</param>
    /// <param name="dstWidth">目标图像宽度（像素）</param>
    /// <param name="dstHeight">目标图像高度（像素）</param>
    /// <returns>成功返回目标尺寸的 <see cref="SKBitmap"/>（调用方负责 Dispose），失败返回 <see langword="null"/></returns>
    internal static SKBitmap? ScaleRawToSkBitmap(
        byte[] buffer, int length,
        PixelBufferFormat format,
        int srcWidth, int srcHeight,
        int dstWidth, int dstHeight)
    {
        if (srcWidth <= 0 || srcHeight <= 0 || dstWidth <= 0 || dstHeight <= 0)
        {
            return null;
        }

        if (!TryGetRawPixmapInfo(format, srcWidth, srcHeight, out var srcInfo, out var expectedLen))
        {
            // 格式无法直接映射到 Skia ColorType，通知调用方走回退路径
            return null;
        }

        if (length < expectedLen)
        {
            return null;
        }

        // 目标统一输出 Bgra8888 Premul，ConvertSkBitmapToAvaloniaWithReuse 可直接使用
        var dstInfo = new SKImageInfo(dstWidth, dstHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
        var dstBitmap = new SKBitmap(dstInfo);

        try
        {
            unsafe
            {
                fixed (byte* p = buffer)
                {
                    // InstallPixels 零拷贝包装原始缓冲区，无需额外分配源尺寸 SKBitmap
                    using var srcBitmap = new SKBitmap();
                    if (!srcBitmap.InstallPixels(srcInfo, (IntPtr)p, srcInfo.RowBytes))
                    {
                        dstBitmap.Dispose();
                        return null;
                    }

                    // 使用 ScalePixels 直接缩放到目标 bitmap，避免 Canvas 绘制开销
                    // Skia 内部处理色彩空间转换与缩放，可利用 SIMD 优化路径
                    if (!srcBitmap.ScalePixels(dstBitmap, DefaultSamplingOptions))
                    {
                        dstBitmap.Dispose();
                        return null;
                    }
                }
            }

            return dstBitmap;
        }
        catch
        {
            dstBitmap.Dispose();
            return null;
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
        // 计算原始缓冲区的期望字节数、源每行字节数与目标 PixelFormat
        PixelFormat pixelFormat;
        int expectedLen;
        int srcBytesPerPixel;

        switch (format)
        {
            case PixelBufferFormat.Bgra32:
                pixelFormat = PixelFormats.Bgra8888;
                srcBytesPerPixel = 4;
                expectedLen = imageWidth * imageHeight * 4;
                break;
            case PixelBufferFormat.Rgba32:
                pixelFormat = PixelFormats.Rgba8888;
                srcBytesPerPixel = 4;
                expectedLen = imageWidth * imageHeight * 4;
                break;
            case PixelBufferFormat.Bgr32:
                pixelFormat = PixelFormats.Bgr32;
                srcBytesPerPixel = 4;
                expectedLen = imageWidth * imageHeight * 4;
                break;
            case PixelBufferFormat.Rgb32:
                pixelFormat = PixelFormats.Rgb32;
                srcBytesPerPixel = 4;
                expectedLen = imageWidth * imageHeight * 4;
                break;
            case PixelBufferFormat.Rgb565:
                pixelFormat = PixelFormats.Rgb565;
                srcBytesPerPixel = 2;
                expectedLen = imageWidth * imageHeight * 2;
                break;
            case PixelBufferFormat.Gray8:
                pixelFormat = PixelFormats.Gray8;
                srcBytesPerPixel = 1;
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

        // 含预乘 Alpha 的格式使用 Premul，其余均不透明
        var alphaFormat = format is PixelBufferFormat.Bgra32 or PixelBufferFormat.Rgba32
            ? AlphaFormat.Premul
            : AlphaFormat.Opaque;

        bitmap ??= new WriteableBitmap(currentSize, new Vector(96, 96), pixelFormat, alphaFormat);

        try
        {
            using var fb = bitmap.Lock();
            var srcRowBytes = imageWidth * srcBytesPerPixel;
            var dstRowBytes = fb.RowBytes;

            // 源/目标行字节不对齐时，拒绝处理（调用方应改用 Encoded 路径）
            if (dstRowBytes != srcRowBytes)
            {
                bitmap.Dispose();
                return null;
            }

            unsafe
            {
                fixed (byte* src = buffer)
                {
                    // 单次 MemoryCopy，无逐行循环
                    Buffer.MemoryCopy(src, (byte*)fb.Address,
                        (long)dstRowBytes * imageHeight,
                        (long)srcRowBytes * imageHeight);
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
    /// 将原始像素缓冲区转换为 SKBitmap，支持 SkiaSharp 3.x 中所有可映射的 ColorType。
    /// 通过 TryGetRawPixmapInfo 统一获取格式信息，避免重复逻辑。
    /// </summary>
    /// <param name="buffer">包含原始像素数据的字节数组</param>
    /// <param name="length">有效字节数</param>
    /// <param name="format">原始像素格式</param>
    /// <param name="imageWidth">图像宽度（像素）</param>
    /// <param name="imageHeight">图像高度（像素）</param>
    /// <returns>成功返回 SKBitmap（调用方负责 Dispose），失败返回 null</returns>
    internal static SKBitmap? CreateSkBitmapFromRaw(
        byte[] buffer, int length,
        PixelBufferFormat format, int imageWidth, int imageHeight)
    {
        if (imageWidth <= 0 || imageHeight <= 0)
        {
            return null;
        }

        // 使用统一的格式映射逻辑
        if (!TryGetRawPixmapInfo(format, imageWidth, imageHeight, out var info, out var expectedLen))
        {
            return null;
        }

        if (length < expectedLen)
        {
            return null;
        }

        var bitmap = new SKBitmap(info);
        try
        {
            unsafe
            {
                fixed (byte* src = buffer)
                {
                    // 单次 MemoryCopy，无逐行循环
                    Buffer.MemoryCopy(src, (void*)bitmap.GetPixels(), expectedLen, expectedLen);
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
}
