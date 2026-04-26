using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using ImageBufferView.Avalonia.Tests.Helpers;
using System;
using System.Threading;
using Xunit;

namespace ImageBufferView.Avalonia.Tests.DecodePipeline;

/// <summary>
/// <see cref="ImageBufferView.SourceView"/> Bitmap 共享机制的集成测试。
/// </summary>
public class SourceViewTests
{
    private ImageBufferView? _sourceView;
    private ImageBufferView? _targetView;

    /// <summary>
    /// 设置 SourceView 后，目标控件的 Bitmap 应与源控件同步。
    /// </summary>
    [AvaloniaFact]
    public void SourceViewSet_WhenBitmapDecoded_TargetReceivesBitmap()
    {
        _sourceView = new ImageBufferView
        {
            Width = 100,
            Height = 100
        };
        _targetView = new ImageBufferView
        {
            Width = 100,
            Height = 100,
            SourceView = _sourceView
        };

        var window = new Window();
        var panel = new StackPanel();
        panel.Children.Add(_sourceView);
        panel.Children.Add(_targetView);
        window.Content = panel;
        window.Show();
        Dispatcher.UIThread.RunJobs();

        _sourceView.ImageBuffer = new ArraySegment<byte>(TestImageHelper.GenerateJpegBytes(16, 16));

        Thread.Sleep(300);
        Dispatcher.UIThread.RunJobs();
        Thread.Sleep(50);
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(_sourceView.Bitmap);
        Assert.NotNull(_targetView.Bitmap);
        Assert.Same(_sourceView.Bitmap, _targetView.Bitmap);
    }

    /// <summary>
    /// 将 SourceView 设为 null 后，目标控件的 Bitmap 应被清空。
    /// </summary>
    [AvaloniaFact]
    public void SourceViewSetToNull_ClearsTargetBitmap()
    {
        _sourceView = new ImageBufferView
        {
            Width = 100,
            Height = 100
        };
        _targetView = new ImageBufferView
        {
            Width = 100,
            Height = 100,
            SourceView = _sourceView
        };

        var window = new Window();
        var panel = new StackPanel();
        panel.Children.Add(_sourceView);
        panel.Children.Add(_targetView);
        window.Content = panel;
        window.Show();
        Dispatcher.UIThread.RunJobs();

        _sourceView.ImageBuffer = new ArraySegment<byte>(TestImageHelper.GenerateJpegBytes(16, 16));
        Thread.Sleep(300);
        Dispatcher.UIThread.RunJobs();
        Assert.NotNull(_targetView.Bitmap);

        _targetView.SourceView = null;
        Dispatcher.UIThread.RunJobs();
        Assert.Null(_targetView.Bitmap);
    }

    /// <summary>
    /// 先设置 SourceView 再解码：目标应在源解码完成后自动同步 Bitmap。
    /// </summary>
    [AvaloniaFact]
    public void SourceViewSetBeforeDecode_TargetSyncsOnDecode()
    {
        _sourceView = new ImageBufferView
        {
            Width = 100,
            Height = 100
        };
        _targetView = new ImageBufferView
        {
            Width = 100,
            Height = 100
        };

        var window = new Window();
        var panel = new StackPanel();
        panel.Children.Add(_sourceView);
        panel.Children.Add(_targetView);
        window.Content = panel;
        window.Show();

        _targetView.SourceView = _sourceView;
        Dispatcher.UIThread.RunJobs();

        _sourceView.ImageBuffer = new ArraySegment<byte>(TestImageHelper.GenerateJpegBytes(16, 16));

        Thread.Sleep(300);
        Dispatcher.UIThread.RunJobs();
        Thread.Sleep(50);
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(_sourceView.Bitmap);
        Assert.Same(_sourceView.Bitmap, _targetView.Bitmap);
    }

    /// <summary>
    /// 切换 SourceView 到不同源：旧订阅应取消，新源的 Bitmap 应同步。
    /// </summary>
    [AvaloniaFact]
    public void SwitchingSourceView_CleansOldSubscription()
    {
        var source1 = new ImageBufferView { Width = 50, Height = 50 };
        var source2 = new ImageBufferView { Width = 50, Height = 50 };
        _targetView = new ImageBufferView
        {
            Width = 50,
            Height = 50,
            SourceView = source1
        };

        var window = new Window();
        var panel = new StackPanel();
        panel.Children.Add(source1);
        panel.Children.Add(source2);
        panel.Children.Add(_targetView);
        window.Content = panel;
        window.Show();
        Dispatcher.UIThread.RunJobs();

        // 给 source2 解码
        source2.ImageBuffer = new ArraySegment<byte>(TestImageHelper.GenerateJpegBytes(8, 8));
        Thread.Sleep(300);
        Dispatcher.UIThread.RunJobs();
        Assert.NotNull(source2.Bitmap);

        // 切换到 source2
        _targetView.SourceView = source2;
        Dispatcher.UIThread.RunJobs();

        Assert.Same(source2.Bitmap, _targetView.Bitmap);
    }
}
