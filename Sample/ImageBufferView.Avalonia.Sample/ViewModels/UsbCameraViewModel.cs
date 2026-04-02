using Avalonia.Controls;
using Avalonia.Threading;
using DynamicData;
using FlashCap;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace ImageBufferView.Avalonia.Sample.ViewModels
{
    /// <summary>
    /// 摄像头流配置
    /// </summary>
    public class VideoCharacteristicModel : ViewModelBase
    {
        public VideoCharacteristics? VideoCharacteristic { init; get; }

        public string Name => VideoCharacteristic == null ? "无流方案"
            : $"{VideoCharacteristic.Width}x{VideoCharacteristic.Height} [{VideoCharacteristic.PixelFormat},{(double)VideoCharacteristic.FramesPerSecond:F0}fps]";
    }

    /// <summary>
    /// 摄像头
    /// </summary>
    public class UsbCameraViewModel : ViewModelBase
    {
        public event EventHandler<int>? DeviceIndexChanged;

        public event EventHandler<int>? VideoResolutionChanged;

        private readonly int _defaultVideoResolution;

        private bool _needStart;

        public UsbCameraViewModel(int defaultDevcieIndex = -1, int defaultVideoResolution = 0, bool autoStart = true)
        {
            _defaultVideoResolution = defaultVideoResolution;
            if (autoStart)
            {
                _needStart = true;
            }
            DeviceList.Add(null);
            Device = DeviceList.FirstOrDefault();

            if (Design.IsDesignMode)
            {
                return;
            }

            Task.Run(() =>
            {
                var devices = new CaptureDevices();
                var devicesList = new List<CaptureDeviceDescriptor>();
                foreach (var descriptor in devices.EnumerateDescriptors()
                             // You could filter by device type and characteristics.
                             .Where(d => d.DeviceType != DeviceTypes.VideoForWindows)
                             .Where(d => !d.Name.Contains("Virtual", StringComparison.InvariantCultureIgnoreCase))
                             .Where(d => d.Characteristics.Any(r =>
                             {
                                 return r.PixelFormat != PixelFormats.Unknown;
                             })))
                {
                    devicesList.Add(descriptor);
                }
                Dispatcher.UIThread.Invoke(() =>
                {
                    if (devicesList.Count > 0)
                    {
                        DeviceList.AddRange(devicesList.OrderBy(r => r.Name));
                    }
                    if (defaultDevcieIndex < 0)
                    {
                        Device = DeviceList.FirstOrDefault();
                    }
                    else
                    {
                        Device = defaultDevcieIndex + 1 < DeviceList.Count ? DeviceList[defaultDevcieIndex + 1] : DeviceList.FirstOrDefault();
                    }

                    this.WhenAnyValue(x => x.Device).Do(OnDeviceListChanged).Subscribe();
                    this.WhenAnyValue(x => x.Characteristic).Do(OnCharacteristicsChangedAsync).Subscribe();
                });
            });
        }

        /// <summary>
        /// Constructed capture device.
        /// </summary>
        private CaptureDevice? _captureDevice;

        /// <summary>
        /// 当前帧数据
        /// </summary>
        public ArraySegment<byte> CurrentImageBuffer
        {
            get => _currentImageBuffer;
            set
            {
                Debug.WriteLine("OnPixelBufferArrived");

                //this.RaiseAndSetIfChanged(ref _currentImageBuffer, value);

                // when use YUYV by FlashCap.1.10.0
                // allways get the same `ArraySegment<byte>` object from `OnPixelBufferArrived`
                // it means `EqualityComparer<TRet>.Default.Equals(backingField, newValue)` always return true
                // so can not use RaiseAndSetIfChanged

                _currentImageBuffer = value;
                this.RaisePropertyChanged(nameof(CurrentImageBuffer));

                var ret = value is { Count: > 0 };
                if (ret != IsPlaying)
                {
                    IsPlaying = ret;
                }
            }
        }

        private ArraySegment<byte> _currentImageBuffer;

        /// <summary>
        /// 是否有画面
        /// </summary>
        public bool IsPlaying
        {
            get => _isPlaying;
            set => this.RaiseAndSetIfChanged(ref _isPlaying, value);
        }

        private bool _isPlaying;

        /// <summary>
        /// 当前帧的像素缓冲格式，由当前选中的摄像头流参数决定
        /// </summary>
        public PixelBufferFormat CurrentPixelBufferFormat
        {
            get => _currentPixelBufferFormat;
            set => this.RaiseAndSetIfChanged(ref _currentPixelBufferFormat, value);
        }

        private PixelBufferFormat _currentPixelBufferFormat = PixelBufferFormat.Encoded;

        /// <summary>
        /// 当前帧原始像素图像宽度（仅在非编码格式下有效）
        /// </summary>
        public int CurrentImageWidth
        {
            get => _currentImageWidth;
            set => this.RaiseAndSetIfChanged(ref _currentImageWidth, value);
        }

        private int _currentImageWidth;

        /// <summary>
        /// 当前帧原始像素图像高度（仅在非编码格式下有效）
        /// </summary>
        public int CurrentImageHeight
        {
            get => _currentImageHeight;
            set => this.RaiseAndSetIfChanged(ref _currentImageHeight, value);
        }

        private int _currentImageHeight;

        /// <summary>
        /// 设备清单
        /// </summary>
        public ObservableCollection<CaptureDeviceDescriptor?> DeviceList
        {
            get => _deviceList;
            init => this.RaiseAndSetIfChanged(ref _deviceList, value);
        }

        private readonly ObservableCollection<CaptureDeviceDescriptor?> _deviceList = [];

        /// <summary>
        /// 当前/选中的设备
        /// </summary>
        public CaptureDeviceDescriptor? Device
        {
            get => _device;
            set => this.RaiseAndSetIfChanged(ref _device, value);
        }

        private CaptureDeviceDescriptor? _device;

        /// <summary>
        /// 当前/选中的设备的配置
        /// </summary>
        public ObservableCollection<VideoCharacteristicModel> CharacteristicList
        {
            get => _characteristicList;
            init => this.RaiseAndSetIfChanged(ref _characteristicList, value);
        }

        private readonly ObservableCollection<VideoCharacteristicModel> _characteristicList = [];

        /// <summary>
        /// 当前/选中的配置
        /// </summary>
        public VideoCharacteristicModel? Characteristic
        {
            get => _characteristic;
            set => this.RaiseAndSetIfChanged(ref _characteristic, value);
        }

        private VideoCharacteristicModel? _characteristic;

        /// <summary>
        /// Devices changed.
        /// </summary>
        /// <param name="device"> </param>
        /// <returns> </returns>
        private void OnDeviceListChanged(CaptureDeviceDescriptor? device)
        {
            if (Design.IsDesignMode)
            {
                return;
            }
            // Use selected device.
            CharacteristicList.Clear();
            if (device != null)
            {
                DeviceIndexChanged?.Invoke(this, DeviceList.IndexOf(device) - 1);
                // Or, you could choice from device descriptor:
                var list = new List<VideoCharacteristicModel>();
                foreach (var characteristic in device.Characteristics)
                {
                    if (characteristic.PixelFormat == PixelFormats.Unknown)
                    {
                        continue;
                    }
                    list.Add(new VideoCharacteristicModel
                    {
                        VideoCharacteristic = characteristic
                    });
                }

                CharacteristicList.AddRange(list.OrderByDescending(r => r?.VideoCharacteristic?.FramesPerSecond));

                if (_defaultVideoResolution < 0)
                {
                    Characteristic = CharacteristicList.FirstOrDefault();
                }
                else
                {
                    Characteristic = _defaultVideoResolution < CharacteristicList.Count ? CharacteristicList[_defaultVideoResolution] : CharacteristicList.FirstOrDefault();
                }
            }
            else
            {
                Characteristic = null;
                DeviceIndexChanged?.Invoke(this, -1);
            }
        }

        /// <summary>
        /// Characteristics changed.
        /// </summary>
        /// <param name="characteristicsModel"> </param>
        private async void OnCharacteristicsChangedAsync(VideoCharacteristicModel? characteristicsModel)
        {
            if (Design.IsDesignMode)
            {
                return;
            }
            try
            {
                // Close when already opened.
                if (_captureDevice is not null)
                {
                    var captureDevice = _captureDevice;
                    _captureDevice = null;
                    await captureDevice.StopAsync();
                    await captureDevice.DisposeAsync();
                    CurrentImageBuffer = default;
                }

                // Descriptor is assigned and set valid characteristics:
                if (Device != null
                    && characteristicsModel is not null
                    && characteristicsModel.VideoCharacteristic is not null)
                {
                    var vc = characteristicsModel.VideoCharacteristic;
                    VideoResolutionChanged?.Invoke(this, CharacteristicList.IndexOf(characteristicsModel));

                    // 将 FlashCap 的像素格式映射为 ImageBufferView 的 PixelBufferFormat
                    var (pbFormat, transcodeFormat) = MapFlashCapFormat(vc.PixelFormat);
                    CurrentPixelBufferFormat = pbFormat;

                    // 对于非编码格式，需要提供图像宽高供 ImageBufferView 解析原始像素
                    if (pbFormat != PixelBufferFormat.Encoded)
                    {
                        CurrentImageWidth  = vc.Width;
                        CurrentImageHeight = vc.Height;
                    }
                    else
                    {
                        CurrentImageWidth  = 0;
                        CurrentImageHeight = 0;
                    }

                    // Open capture device:
                    _captureDevice = await Device.OpenAsync(
                        vc, transcodeFormat,
                        OnPixelBufferArrived);

                    if (_needStart)
                    {
                        // Start capturing.
                        await _captureDevice.StartAsync();
                    }
                }
            }
            catch
            {
                // no use
            }
        }

        /// <summary>
        /// 将 FlashCap <see cref="PixelFormats"/> 映射为
        /// <see cref="PixelBufferFormat"/> 与对应的 <see cref="TranscodeFormats"/>。
        /// <list type="bullet">
        ///   <item>JPEG / PNG → Encoded，不转码（保留原始编码数据）</item>
        ///   <item>其余格式 → 对应原始像素格式，不转码（由 ImageBufferView 负责解码）</item>
        /// </list>
        /// </summary>
        /// <param name="flashCapFormat">FlashCap 摄像头像素格式</param>
        /// <returns>（ImageBufferView 像素格式, FlashCap 转码格式）</returns>
        private static (PixelBufferFormat pbFormat, TranscodeFormats transcodeFormat)
            MapFlashCapFormat(PixelFormats flashCapFormat)
        {
            return flashCapFormat switch
            {
                // 编码格式：直接传递原始编码数据给 ImageBufferView
                PixelFormats.JPEG => (PixelBufferFormat.Encoded, TranscodeFormats.DoNotTranscode),
                PixelFormats.PNG  => (PixelBufferFormat.Encoded, TranscodeFormats.DoNotTranscode),

                // RGB 系列：不转码，由 ImageBufferView 解析原始像素
                // 注意：Windows DIB 中 RGB24/RGB32 在内存中实际为 BGR 顺序
                PixelFormats.RGB8   => (PixelBufferFormat.Rgb8,   TranscodeFormats.DoNotTranscode),
                PixelFormats.RGB15  => (PixelBufferFormat.Rgb15,  TranscodeFormats.DoNotTranscode),
                PixelFormats.RGB16  => (PixelBufferFormat.Rgb16,  TranscodeFormats.DoNotTranscode),
                PixelFormats.RGB24  => (PixelBufferFormat.Bgr24,  TranscodeFormats.DoNotTranscode),
                PixelFormats.RGB32  => (PixelBufferFormat.Bgr32,  TranscodeFormats.DoNotTranscode),
                PixelFormats.ARGB32 => (PixelBufferFormat.Argb32, TranscodeFormats.DoNotTranscode),

                // YUV 系列：不转码，由 ImageBufferView 完成 YUV→RGB 转换
                PixelFormats.UYVY => (PixelBufferFormat.Uyvy, TranscodeFormats.DoNotTranscode),
                PixelFormats.YUYV => (PixelBufferFormat.Yuyv, TranscodeFormats.DoNotTranscode),
                PixelFormats.NV12 => (PixelBufferFormat.Nv12, TranscodeFormats.DoNotTranscode),

                // 未知或其他格式：回退到 Auto 转码（FlashCap 转为 JPEG），当作编码格式处理
                _ => (PixelBufferFormat.Encoded, TranscodeFormats.Auto),
            };
        }

        /// <summary>
        /// 实时帧数据回调
        /// </summary>
        /// <param name="bufferScope"> </param>
        private void OnPixelBufferArrived(PixelBufferScope bufferScope)
        {
            // ReferImage() 返回当前帧缓冲区的引用（零拷贝），适用于所有格式：
            //   - 编码格式（JPEG/PNG）：返回完整的编码字节流
            //   - 原始像素格式（RGB/YUV 等）：返回原始像素字节，由 ImageBufferView 负责解析
            // 注意：YUYV 等格式下每次回调返回同一 ArraySegment 对象实例（FlashCap 内部复用缓冲），
            //       因此 CurrentImageBuffer 的 setter 不能使用 RaiseAndSetIfChanged。
            CurrentImageBuffer = bufferScope.Buffer.ReferImage();
        }

        public async void Start()
        {
            _needStart = true;
            if (_captureDevice == null)
            {
                return;
            }
            await _captureDevice.StartAsync();
        }

        public async void Stop()
        {
            _needStart = false;
            if (_captureDevice == null)
            {
                return;
            }

            try
            {
                await _captureDevice?.StopAsync()!;
            }
            catch
            {
                // ignored
            }

            CurrentImageBuffer = default;
        }
    }
}