# ImageBufferView.Avalonia
Avalonia 图片二进制流显示 (主要用于显示摄像头画面)

```
    xmlns:ibv="clr-namespace:ImageBufferView.Avalonia;assembly=ImageBufferView.Avalonia"
```

```
	<ibv:ImageBufferView
		Grid.Row="1"
		Margin="5"
        DefaultBackground="Black"
		ImageBuffer="{Binding ImageBuffer}"
		Stretch="Uniform" />
```

ViewModel
```
    public class MainWindowViewModel : ViewModelBase
    {
        /// <summary>
        /// 当前播放的图片
        /// </summary>
        [Reactive]
        public byte[]? ImageBuffer { get; private set; }

        /// <summary>
        /// 待播放图片流缓存
        /// </summary>

        private readonly List<byte[]> buffers = new();

        private CancellationTokenSource? _cancellationTokenSource;

        public void Start()
        {
            if (_cancellationTokenSource is { })
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource = null;
                return;
            }

            _cancellationTokenSource = new CancellationTokenSource();

            LoadImage(_cancellationTokenSource.Token);
        }

        private void LoadImage(CancellationToken token)
        {
            Task.Run(() =>
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
                        var buffer = File.ReadAllBytes(file.FullName);
                        buffers.Add(buffer);
                    }
                    catch
                    {
                    }
                }

                if (buffers.Count == 0)
                {
                    return;
                }
                while (!token.IsCancellationRequested)
                {
                    foreach (var buffer in buffers)
                    {
                        if (token.IsCancellationRequested)
                        {
                            break;
                        }
                        ImageBuffer = buffer;

                        Task.Delay(1).Wait();
                    }
                }
                ImageBuffer = default;
            }, token);
        }
    }
}
```
