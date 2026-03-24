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
                    VideoResolutionChanged?.Invoke(this, CharacteristicList.IndexOf(characteristicsModel));
                    // Open capture device:
                    _captureDevice = await Device.OpenAsync(
                        characteristicsModel.VideoCharacteristic, TranscodeFormats.Auto,
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
        /// 实时帧数据回调
        /// </summary>
        /// <param name="bufferScope"> </param>
        private void OnPixelBufferArrived(PixelBufferScope bufferScope)
        {
            CurrentImageBuffer = bufferScope.Buffer.ReferImage();//.ExtractImage();
            //CurrentImageBuffer = bufferScope.Buffer.ExtractImage();
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