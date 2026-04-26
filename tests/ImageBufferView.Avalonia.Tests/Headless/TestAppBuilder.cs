using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;

[assembly: AvaloniaTestApplication(typeof(ImageBufferView.Avalonia.Tests.Headless.TestAppBuilder))]

namespace ImageBufferView.Avalonia.Tests.Headless;

/// <summary>
/// Avalonia Headless 测试应用构建器，配置 Skia 渲染后端以确保 WriteableBitmap 像素数据可用。
/// </summary>
public static class TestAppBuilder
{
    /// <summary>
    /// 构建 Headless 模式的 Avalonia 应用，用于控件集成测试。
    /// </summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<Application>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = true
            });

    /// <summary>
    /// 将控件附加到可视树并立即执行所有待处理的分发器任务。
    /// 用于需要控件处于 Attached 状态且 Bitmap 已通过 UI 线程更新的测试场景。
    /// </summary>
    /// <param name="control">待附加的控件</param>
    /// <returns>承载控件的 <see cref="Window"/></returns>
    public static Window AttachToVisualTree(Control control)
    {
        var window = new Window { Content = control };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }
}
