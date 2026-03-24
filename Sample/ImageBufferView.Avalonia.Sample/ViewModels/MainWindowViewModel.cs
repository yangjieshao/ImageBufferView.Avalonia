using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ImageBufferView.Avalonia.Sample.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        /// <summary>
        /// 当前播放的图片
        /// </summary>
        public ArraySegment<byte>? ImageBuffer
        {
            get => _ImageBuffer;
            set => this.RaiseAndSetIfChanged(ref _ImageBuffer, value);
        }
        private ArraySegment<byte>? _ImageBuffer;

        /// <summary>
        /// 待播放图片流缓存
        /// </summary>

        private readonly List<ArraySegment<byte>> _buffers = [];

        /// <summary>
        /// 摄像头
        /// </summary>
        public UsbCameraViewModel UsbCamera
        {
            get => _usbCamera;
            private init => this.RaiseAndSetIfChanged(ref _usbCamera, value);
        }

        private readonly UsbCameraViewModel _usbCamera = new ();

        /// <summary>
        /// </summary>
        public CancellationTokenSource? CancellationTokenSource
        {
            get => _cancellationTokenSource;
            set => this.RaiseAndSetIfChanged(ref _cancellationTokenSource, value);
        }

        private CancellationTokenSource? _cancellationTokenSource;

        public async void Start()
        {
            if (CancellationTokenSource is { })
            {
                return;
            }

            CancellationTokenSource = new CancellationTokenSource();

            await LoadImage(CancellationTokenSource.Token);
        }

        public void Clean()
        {
            CancellationTokenSource?.Cancel();
            CancellationTokenSource = null;
            ImageBuffer = default;
        }

        public void Pause()
        {
            if (CancellationTokenSource is null)
            {
                return;
            }
            CancellationTokenSource.Cancel();
            CancellationTokenSource = null;
        }

        private async ValueTask LoadImage(CancellationToken token)
        {
            if (_buffers.Count == 0)
            {
                if (!Directory.Exists("Images"))
                {
                    return;
                }
                var files = new DirectoryInfo("Images").GetFiles("*.jpeg");

                // Ready buffers
                foreach (var file in files)
                {
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }
                    try
                    {
                        var buffer = await File.ReadAllBytesAsync(file.FullName, token);
                        _buffers.Add(buffer);
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                }
            }

            if (_buffers.Count == 0)
            {
                return;
            }
            while (!token.IsCancellationRequested)
            {
                foreach (var buffer in _buffers.TakeWhile(buffer => !token.IsCancellationRequested))
                {
                    ImageBuffer = buffer;
                    try
                    {
                        await Task.Delay(1, token);
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                }
            }
        }
    }
}