using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Interactivity;
using ImageBufferView.Avalonia;
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

        private void OnRotateLeft(object? sender, RoutedEventArgs e)
        {
            if (this.FindControl<ImageBufferView>("CameraView") is { } view)
            {
                view.Rotation = view.Rotation switch
                {
                    ImageRotation.Rotate0 => ImageRotation.Rotate270,
                    ImageRotation.Rotate90 => ImageRotation.Rotate0,
                    ImageRotation.Rotate180 => ImageRotation.Rotate90,
                    ImageRotation.Rotate270 => ImageRotation.Rotate180,
                    _ => ImageRotation.Rotate0
                };
            }
        }

        private void OnRotateRight(object? sender, RoutedEventArgs e)
        {
            if (this.FindControl<ImageBufferView>("CameraView") is { } view)
            {
                view.Rotation = view.Rotation switch
                {
                    ImageRotation.Rotate0 => ImageRotation.Rotate90,
                    ImageRotation.Rotate90 => ImageRotation.Rotate180,
                    ImageRotation.Rotate180 => ImageRotation.Rotate270,
                    ImageRotation.Rotate270 => ImageRotation.Rotate0,
                    _ => ImageRotation.Rotate0
                };
            }
        }

        private void OnRotate180(object? sender, RoutedEventArgs e)
        {
            if (this.FindControl<ImageBufferView>("CameraView") is { } view)
            {
                view.Rotation = ImageRotation.Rotate180;
            }
        }

        private void OnFlipHorizontal(object? sender, RoutedEventArgs e)
        {
            if (this.FindControl<ImageBufferView>("CameraView") is { } view)
            {
                view.FlipHorizontal = !view.FlipHorizontal;
            }
        }

        private void OnFlipVertical(object? sender, RoutedEventArgs e)
        {
            if (this.FindControl<ImageBufferView>("CameraView") is { } view)
            {
                view.FlipVertical = !view.FlipVertical;
            }
        }

        private void OnResetTransform(object? sender, RoutedEventArgs e)
        {
            if (this.FindControl<ImageBufferView>("CameraView") is { } view)
            {
                view.Rotation = ImageRotation.Rotate0;
                view.FlipHorizontal = false;
                view.FlipVertical = false;
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            this.FindControl<Button>("RotateLeftButton")!.Click += OnRotateLeft;
            this.FindControl<Button>("RotateRightButton")!.Click += OnRotateRight;
            this.FindControl<Button>("Rotate180Button")!.Click += OnRotate180;
            this.FindControl<Button>("FlipHorizontalButton")!.Click += OnFlipHorizontal;
            this.FindControl<Button>("FlipVerticalButton")!.Click += OnFlipVertical;
            this.FindControl<Button>("ResetTransformButton")!.Click += OnResetTransform;

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