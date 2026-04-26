using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming

namespace ImageBufferView.Avalonia.Sample.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    /// <summary>
    /// 当前播放的图片
    /// </summary>
    public ArraySegment<byte>? ImageBuffer2
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = Convert.FromBase64String(
        "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");

    /// <summary>
    /// </summary>
    public PixelBufferFormat PixelBufferFormat
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = PixelBufferFormat.Encoded;

    /// <summary>
    /// 当前播放的图片
    /// </summary>
    public ArraySegment<byte>? ImageBuffer
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>
    /// </summary>
    public ArraySegment<byte>? ImageBgr24Buffer
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>
    /// </summary>
    public ArraySegment<byte>? ImageBgra32Buffer
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>
    /// </summary>
    public ArraySegment<byte>? ImageRgb24Buffer
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>
    /// </summary>
    public ArraySegment<byte>? ImageRgba32Buffer
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>
    /// 当前格式名称（用于 UI 显示）
    /// </summary>
    public string CurrentFormatName
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "Encoded (JPEG)";

    /// <summary>
    /// 待播放图片流缓存
    /// </summary>
    private readonly List<ArraySegment<byte>> _encodedBuffers = [];

    /// <summary>
    /// </summary>
    private readonly List<ArraySegment<byte>> _bgra32Buffers = [];

    /// <summary>
    /// </summary>
    private readonly List<ArraySegment<byte>> _rgba32Buffers = [];

    /// <summary>
    /// 摄像头
    /// </summary>
    public UsbCameraViewModel UsbCamera
    {
        get;
        private init => this.RaiseAndSetIfChanged(ref field, value);
    } = new();

    /// <summary>
    /// </summary>
    public CancellationTokenSource? CancellationTokenSource
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public async Task Start()
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

    private bool _useBgra32 = true;

    private async ValueTask LoadImage(CancellationToken token)
    {
        // 预加载两种格式的缓冲区
        await LoadBgra32Buffer(token);
        await LoadEncodedBuffer(token);

        // 默认启动 Bgra32
        _useBgra32 = false;
        PixelBufferFormat = PixelBufferFormat.Encoded;
        CurrentFormatName = "Encoded (JPEG)";
        RunEncodedBuffer(token);
    }

    public void SwitchFormat()
    {
        // 先停止当前播放
        CancellationTokenSource?.Cancel();
        CancellationTokenSource = null;
        ImageBuffer = default;

        // 切换格式
        _useBgra32 = !_useBgra32;

        CancellationTokenSource = new CancellationTokenSource();
        var token = CancellationTokenSource.Token;

        if (_useBgra32)
        {
            PixelBufferFormat = PixelBufferFormat.Bgra32;
            CurrentFormatName = "Bgra32 128x128";
            RunBgra32Buffer(token);
        }
        else
        {
            PixelBufferFormat = PixelBufferFormat.Encoded;
            CurrentFormatName = "Encoded (JPEG)";
            RunEncodedBuffer(token);
        }
    }

    private void RunBgra32Buffer(CancellationToken token)
    {
        if (_bgra32Buffers.Count == 0)
        {
            return;
        }

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                foreach (var buffer in _bgra32Buffers.TakeWhile(_ => !token.IsCancellationRequested))
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
        }, token);
    }

    private void RunRgba32Buffer(CancellationToken token)
    {
        if (_rgba32Buffers.Count == 0)
        {
            return;
        }

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                foreach (var buffer in _rgba32Buffers.TakeWhile(_ => !token.IsCancellationRequested))
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
        }, token);
    }

    private void RunEncodedBuffer(CancellationToken token)
    {
        if (_encodedBuffers.Count == 0)
        {
            return;
        }

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                foreach (var buffer in _encodedBuffers.TakeWhile(_ => !token.IsCancellationRequested))
                {
                    ImageBuffer = buffer;
                    try
                    {
                        await Task.Delay(1000, token);
                        ImageBuffer = default;
                        await Task.Delay(1000, token);
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                }
            }
        }, token);
    }

    private async Task LoadBgra32Buffer(CancellationToken token)
    {
        if (_bgra32Buffers.Count == 0)
        {
            if (!Directory.Exists("Raw"))
            {
                return;
            }

            var files = new DirectoryInfo("Raw").GetFiles("bgra32*.raw");

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
                    _bgra32Buffers.Add(buffer);
                }
                catch (Exception)
                {
                    // ignored
                }
            }
        }
    }

    private async Task LoadRgba32Buffer(CancellationToken token)
    {
        if (_rgba32Buffers.Count == 0)
        {
            if (!Directory.Exists("Raw"))
            {
                return;
            }

            var files = new DirectoryInfo("Raw").GetFiles("rgba32*.raw");

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
                    _rgba32Buffers.Add(buffer);
                }
                catch (Exception)
                {
                    // ignored
                }
            }
        }
    }

    private async Task LoadEncodedBuffer(CancellationToken token)
    {
        if (_encodedBuffers.Count == 0)
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
                    _encodedBuffers.Add(buffer);
                }
                catch (Exception)
                {
                    // ignored
                }
            }
        }
    }
}