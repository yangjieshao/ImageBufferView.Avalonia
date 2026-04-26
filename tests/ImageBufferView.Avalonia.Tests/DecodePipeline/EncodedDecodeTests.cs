using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using ImageBufferView.Avalonia.Tests.Headless;
using ImageBufferView.Avalonia.Tests.Helpers;
using System;
using System.Threading;
using Xunit;

namespace ImageBufferView.Avalonia.Tests.DecodePipeline;

/// <summary>
/// 编码格式（JPEG）解码管线的集成测试。
/// </summary>
public class EncodedDecodeTests : IDisposable
{
    private ImageBufferView? _view;
    private Window? _visualTreeHandle;

    /// <summary>
    /// 有效的 JPEG 数据应成功解码为 Bitmap。
    /// </summary>
    [AvaloniaFact]
    public  void ValidJpeg_DecodesToBitmap()
    {
        // 先创建控件并设置渲染大小，再附加到可视树
        _view = new ImageBufferView
        {
            Width = 200,
            Height = 200,
            Stretch = Stretch.Uniform
        };
        _visualTreeHandle = TestAppBuilder.AttachToVisualTree(_view);

        _view.ImageBuffer = new ArraySegment<byte>(TestImageHelper.GenerateJpegBytes(16, 16));

        // 等待线程池解码 + UI 线程推送完成
        WaitForDecode();

        Assert.NotNull(_view.Bitmap);
        Assert.True(_view.Bitmap!.Size.Width > 0);
        Assert.True(_view.Bitmap.Size.Height > 0);
    }

    /// <summary>
    /// 无效的 JPEG 数据（全零字节）不应导致 Bitmap 被设置。
    /// </summary>
    [AvaloniaFact]
    public  void InvalidJpeg_DoesNotSetBitmap()
    {
        _view = new ImageBufferView
        {
            Width = 100,
            Height = 100
        };
        _visualTreeHandle = TestAppBuilder.AttachToVisualTree(_view);

        _view.ImageBuffer = new ArraySegment<byte>(TestImageHelper.GenerateInvalidImageBytes());

        WaitForDecode();

        Assert.Null(_view.Bitmap);
    }

    /// <summary>
    /// 将 ImageBuffer 设为 null 后 Bitmap 应被清空。
    /// </summary>
    [AvaloniaFact]
    public  void SetImageBufferToNull_ClearsBitmap()
    {
        _view = new ImageBufferView
        {
            Width = 100,
            Height = 100
        };
        _visualTreeHandle = TestAppBuilder.AttachToVisualTree(_view);

        _view.ImageBuffer = new ArraySegment<byte>(TestImageHelper.GenerateJpegBytes(16, 16));
        WaitForDecode();
        Assert.NotNull(_view.Bitmap);

        _view.ImageBuffer = null;
        WaitForDecode();
        Assert.Null(_view.Bitmap);
    }

    /// <summary>
    /// 未附加到可视树时设置 ImageBuffer 不触发解码。
    /// </summary>
    [AvaloniaFact]
    public  void NotAttached_DoesNotDecode()
    {
        _view = new ImageBufferView
        {
            Width = 100,
            Height = 100
        };
        // 不附加到可视树
        _view.ImageBuffer = new ArraySegment<byte>(TestImageHelper.GenerateJpegBytes(16, 16));

        Dispatcher.UIThread.RunJobs();

        Assert.Null(_view.Bitmap);
    }

    /// <summary>
    /// 连续设置不同 ImageBuffer 应始终解码最新的。
    /// </summary>
    [AvaloniaFact]
    public  void RapidUpdates_DecodesLatest()
    {
        _view = new ImageBufferView
        {
            Width = 200,
            Height = 200
        };
        _visualTreeHandle = TestAppBuilder.AttachToVisualTree(_view);

        // 先发无效数据，再发有效数据
        _view.ImageBuffer = new ArraySegment<byte>(TestImageHelper.GenerateInvalidImageBytes());
        _view.ImageBuffer = new ArraySegment<byte>(TestImageHelper.GenerateJpegBytes(16, 16));

        WaitForDecode();

        // 最终应解码有效数据
        Assert.NotNull(_view.Bitmap);
    }

    /// <summary>
    /// 等待线程池解码完成并清空 UI 分发器。
    /// </summary>
    private static void WaitForDecode()
    {
        // 给线程池足够时间完成解码和 Post
        Thread.Sleep(300);
        // 清空 UI 线程任务队列
        Dispatcher.UIThread.RunJobs();
        // 给 UI 更新后的后续处理留出时间
        Thread.Sleep(50);
        Dispatcher.UIThread.RunJobs();
    }

    public void Dispose()
    {
        _visualTreeHandle?.Close();
        _visualTreeHandle = null;
    }
}
