using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using ImageBufferView.Avalonia.Tests.Headless;
using ImageBufferView.Avalonia.Tests.Helpers;
using System;
using System.Threading;
using Xunit;

namespace ImageBufferView.Avalonia.Tests.DecodePipeline;

/// <summary>
/// 原始像素格式解码管线的集成测试。
/// </summary>
public class RawPixelDecodeTests : IDisposable
{
    private ImageBufferView? _view;
    private Window? _visualTreeHandle;

    /// <summary>
    /// Bgra32 格式的原始像素数据应成功解码为 Bitmap。
    /// </summary>
    [AvaloniaFact]
    public  void Bgra32_DecodesToWriteableBitmap()
    {
        const int w = 4;
        const int h = 4;
        _view = new ImageBufferView
        {
            Width = 100,
            Height = 100,
            PixelBufferFormat = PixelBufferFormat.Bgra32,
            RawImageWidth = w,
            RawImageHeight = h
        };
        _visualTreeHandle = TestAppBuilder.AttachToVisualTree(_view);

        _view.ImageBuffer = TestImageHelper.GenerateBgra32ArraySegment(w, h);

        // 等待线程池解码 + UI 线程推送
        Thread.Sleep(300);
        Dispatcher.UIThread.RunJobs();
        Thread.Sleep(50);
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(_view.Bitmap);
        Assert.Equal(w, _view.Bitmap!.PixelSize.Width);
        Assert.Equal(h, _view.Bitmap.PixelSize.Height);
    }

    /// <summary>
    /// Bgra32 格式但未设置 RawImageWidth/Height 应解码失败。
    /// </summary>
    [AvaloniaFact]
    public  void Bgra32_MissingDimensions_DoesNotProduceBitmap()
    {
        _view = new ImageBufferView
        {
            Width = 100,
            Height = 100,
            PixelBufferFormat = PixelBufferFormat.Bgra32
            // 未设置 RawImageWidth / RawImageHeight
        };
        _visualTreeHandle = TestAppBuilder.AttachToVisualTree(_view);

        _view.ImageBuffer = TestImageHelper.GenerateBgra32ArraySegment(4, 4);

        Thread.Sleep(300);
        Dispatcher.UIThread.RunJobs();

        Assert.Null(_view.Bitmap);
    }

    /// <summary>
    /// Rgba32 格式的原始像素数据应成功解码。
    /// </summary>
    [AvaloniaFact]
    public  void Rgba32_DecodesToBitmap()
    {
        const int w = 8;
        const int h = 8;
        _view = new ImageBufferView
        {
            Width = 200,
            Height = 200,
            PixelBufferFormat = PixelBufferFormat.Rgba32,
            RawImageWidth = w,
            RawImageHeight = h
        };
        _visualTreeHandle = TestAppBuilder.AttachToVisualTree(_view);

        _view.ImageBuffer = new ArraySegment<byte>(TestImageHelper.GenerateRgba32Pixels(w, h));

        Thread.Sleep(300);
        Dispatcher.UIThread.RunJobs();
        Thread.Sleep(50);
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(_view.Bitmap);
        Assert.Equal(w, _view.Bitmap!.PixelSize.Width);
    }

    /// <summary>
    /// Bgra32 数据但缓冲长度不足时不应产生 Bitmap。
    /// </summary>
    [AvaloniaFact]
    public  void BufferTooSmall_DoesNotProduceBitmap()
    {
        _view = new ImageBufferView
        {
            Width = 100,
            Height = 100,
            PixelBufferFormat = PixelBufferFormat.Bgra32,
            RawImageWidth = 100,
            RawImageHeight = 100
        };
        _visualTreeHandle = TestAppBuilder.AttachToVisualTree(_view);

        // 只有 10 字节，但需要 40000 字节
        _view.ImageBuffer = new ArraySegment<byte>(new byte[10]);

        Thread.Sleep(300);
        Dispatcher.UIThread.RunJobs();

        Assert.Null(_view.Bitmap);
    }

    public void Dispose()
    {
        _visualTreeHandle?.Close();
        _visualTreeHandle = null;
    }
}
