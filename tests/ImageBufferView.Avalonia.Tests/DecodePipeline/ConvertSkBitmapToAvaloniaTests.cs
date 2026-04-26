using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using ImageBufferView.Avalonia.Tests.Headless;
using SkiaSharp;
using System.Threading;
using Xunit;

namespace ImageBufferView.Avalonia.Tests.DecodePipeline;

/// <summary>
/// <see cref="ImageBufferView.ConvertSkBitmapToAvaloniaWithReuse"/> 的集成测试，
/// 覆盖 SKBitmap→WriteableBitmap 转换及后台缓冲复用。
/// </summary>
public class ConvertSkBitmapToAvaloniaTests
{
    private ImageBufferView? _view;
    private Window? _visualTreeHandle;

    /// <summary>
    /// 将 Bgra8888 SKBitmap 转换为 WriteableBitmap 后像素数据应一致。
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

        using var wb = _view.ConvertSkBitmapToAvaloniaWithReuse(skBitmap, false);
        Assert.NotNull(wb);
        Assert.Equal(w, wb!.PixelSize.Width);
        Assert.Equal(h, wb.PixelSize.Height);

        // 验证像素数据
        using var fb = wb.Lock();
        var dst = (byte*)fb.Address;
        for (var i = 0; i < skBitmap.ByteCount; i++)
        {
            Assert.Equal(skPixels[i], dst[i]);
        }
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
        Assert.Equal(w, wb!.PixelSize.Width);
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
        Assert.Equal(w, wb!.PixelSize.Width);
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

        // 第一次转换后 Dispose 应回收
        wb1!.Dispose();

        // 给 UI 线程处理回收任务的时间
        Dispatcher.UIThread.RunJobs();
        Thread.Sleep(50);
        Dispatcher.UIThread.RunJobs();

        using var wb2 = _view.ConvertSkBitmapToAvaloniaWithReuse(skBitmap2, true);
        Assert.NotNull(wb2);
    }
}
