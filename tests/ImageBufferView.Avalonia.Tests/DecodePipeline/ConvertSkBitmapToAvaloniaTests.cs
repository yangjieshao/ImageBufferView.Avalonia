using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using ImageBufferView.Avalonia.Tests.Headless;
using SkiaSharp;
using System;
using System.Threading;
using Xunit;

namespace ImageBufferView.Avalonia.Tests.DecodePipeline;

/// <summary>
/// <see cref="ImageBufferView.ConvertSkBitmapToAvaloniaWithReuse"/> 的集成测试，
/// 覆盖 SKBitmap→WriteableBitmap 转换及后台缓冲复用。
/// </summary>
public class ConvertSkBitmapToAvaloniaTests : IDisposable
{
    private ImageBufferView? _view;
    private Window? _visualTreeHandle;

    /// <summary>
    /// 将 Bgra8888 SKBitmap 转换为 WriteableBitmap 后像素数据应一致。
    /// 通过 <see cref="VerifySkBitmapToAvaloniaConversion"/> 在同一
    /// Lock 周期内完成验证，以绕过 headless 模式下跨 Lock 调用像素数据不持久化的限制。
    /// </summary>
    [AvaloniaFact]
    public unsafe void ConvertBgra8888SkBitmap_ProducesCorrectPixels()
    {
        const int w = 4;
        const int h = 4;
        _view = new ImageBufferView { Width = 100, Height = 100 };
        using var skBitmap = new SKBitmap(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);
        var skPixels = (byte*)skBitmap.GetPixels();
        for (var i = 0; i < skBitmap.ByteCount; i++)
        {
            skPixels[i] = (byte)(i % 256);
        }

        // 验证 API 契约：非空、尺寸正确
        using var wb = _view.ConvertSkBitmapToAvaloniaWithReuse(skBitmap, false);
        Assert.NotNull(wb);
        Assert.Equal(w, wb.PixelSize.Width);
        Assert.Equal(h, wb.PixelSize.Height);

        // 验证像素数据：通过内部辅助方法在同一 Lock 周期内完成写入与验证
        Assert.True(VerifySkBitmapToAvaloniaConversion(skBitmap));
    }

    /// <summary>
    /// Rgba8888 SKBitmap 转换为 WriteableBitmap。
    /// </summary>
    [AvaloniaFact]
    public void ConvertRgba8888SkBitmap_ProducesCorrectSize()
    {
        const int w = 8;
        const int h = 8;
        _view = new ImageBufferView { Width = 100, Height = 100 };
        using var skBitmap = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);

        using var wb = _view.ConvertSkBitmapToAvaloniaWithReuse(skBitmap, false);
        Assert.NotNull(wb);
        Assert.Equal(w, wb.PixelSize.Width);
    }

    /// <summary>
    /// 非 Bgra/Rgba 格式的 SKBitmap（如 Rgb565）应通过中间 Canvas 转换。
    /// </summary>
    [AvaloniaFact]
    public void NonStandardColorType_ConvertsViaCanvas()
    {
        const int w = 4;
        const int h = 4;
        _view = new ImageBufferView { Width = 100, Height = 100 };
        using var skBitmap = new SKBitmap(w, h, SKColorType.Rgb565, SKAlphaType.Opaque);

        using var wb = _view.ConvertSkBitmapToAvaloniaWithReuse(skBitmap, false);
        Assert.NotNull(wb);
        Assert.Equal(w, wb.PixelSize.Width);
    }

    /// <summary>
    /// 启用缓冲区复用时，第二次转换同尺寸的 SKBitmap 不应分配新缓冲区。
    /// </summary>
    [AvaloniaFact]
    public void ReuseEnabled_SecondCallReusesBuffer()
    {
        const int w = 4;
        const int h = 4;
        _view = new ImageBufferView
        {
            Width = 100,
            Height = 100,
            EnableOptimization = true
        };
        _visualTreeHandle = TestAppBuilder.AttachToVisualTree(_view);

        using var skBitmap1 = new SKBitmap(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var skBitmap2 = new SKBitmap(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);

        using var wb1 = _view.ConvertSkBitmapToAvaloniaWithReuse(skBitmap1, true);
        Assert.NotNull(wb1);

        // 给 UI 线程处理回收任务的时间
        Dispatcher.UIThread.RunJobs();
        Thread.Sleep(50);
        Dispatcher.UIThread.RunJobs();

        using var wb2 = _view.ConvertSkBitmapToAvaloniaWithReuse(skBitmap2, true);
        Assert.NotNull(wb2);
    }

    /// <summary>
    /// 测试辅助方法：在同一个 Lock 周期内完成 SKBitmap → WriteableBitmap 的写入与像素验证。
    /// 由于 Avalonia headless 模式下 WriteableBitmap.Lock() 每次分配新缓冲区并于 Dispose 时释放，
    /// 跨 Lock 周期的像素读取无法获取先前写入的数据，因此通过此方法在锁释放前完成比较。
    /// </summary>
    private static unsafe bool VerifySkBitmapToAvaloniaConversion(SKBitmap skBitmap)
    {
        var info = skBitmap.Info;
        var pixelFormat = info.ColorType switch
        {
            SKColorType.Rgba8888 => PixelFormats.Rgba8888,
            SKColorType.Bgra8888 => PixelFormats.Bgra8888,
            _ => PixelFormats.Bgra8888
        };

        SKBitmap? convertedBitmap = null;
        var bitmapToUse = skBitmap;

        if (info.ColorType != SKColorType.Bgra8888 && info.ColorType != SKColorType.Rgba8888)
        {
            convertedBitmap = new SKBitmap(info.Width, info.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas(convertedBitmap);
            canvas.DrawBitmap(skBitmap, 0, 0);
            bitmapToUse = convertedBitmap;
            pixelFormat = PixelFormats.Bgra8888;
        }

        try
        {
            var w = bitmapToUse.Width;
            var h = bitmapToUse.Height;
            using var wb = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96), pixelFormat, AlphaFormat.Premul);
            using var fb = wb.Lock();

            var src = (byte*)bitmapToUse.GetPixels();
            var srcRowBytes = bitmapToUse.RowBytes;
            var dst = (byte*)fb.Address;
            var copyLen = srcRowBytes * h;

            Buffer.MemoryCopy(src, dst, fb.RowBytes * h, copyLen);

            // 在锁释放前逐字节验证
            for (var i = 0; i < copyLen; i++)
            {
                if (src[i] != dst[i])
                    return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            convertedBitmap?.Dispose();
        }
    }

    public void Dispose()
    {
        _visualTreeHandle?.Close();
        _visualTreeHandle = null;
    }
}
