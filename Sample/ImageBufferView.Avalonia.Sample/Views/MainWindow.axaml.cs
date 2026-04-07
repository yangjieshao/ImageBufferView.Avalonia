using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;
using System.Diagnostics;

namespace ImageBufferView.Avalonia.Sample.Views
{
    public partial class MainWindow : Window
    {
        private readonly Stopwatch _fpsStopwatch = Stopwatch.StartNew();
        private int _frameCount;
        private long _lastFpsUpdateTime;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            // 使用 RequestAnimationFrame 统计渲染帧率（类似 WPF 的 CompositionTarget.Rendering）
            RequestAnimationFrame();
        }

        private void RequestAnimationFrame()
        {
            TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnAnimationFrame);
        }

        private void OnAnimationFrame(TimeSpan time)
        {
            _frameCount++;
            var currentTime = _fpsStopwatch.ElapsedMilliseconds;
            var elapsed = currentTime - _lastFpsUpdateTime;

            if (elapsed > 500)
            {
                var fps = _frameCount * 1000.0 / elapsed;
                _frameCount = 0;
                _lastFpsUpdateTime = currentTime;
                Title = $"ImageBufferView Sample - FPS: {fps:F1}";
            }

            // 继续请求下一帧
            RequestAnimationFrame();
        }
    }
}