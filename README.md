# ImageBufferView.Avalonia

Avalonia 高性能图片二进制流显示控件，专为实时图像流场景优化。

## ✨ 特性

- 🚀 **高性能渲染** - 内置 SkiaSharp 预缩放和缓冲区复用优化，性能提升 10-50%
- 📷 **实时流支持** - 专为摄像头视频流、图片序列播放等高帧率场景设计
- 🔄 **智能适配** - 自动检测分辨率变化，无需手动配置
- 🎯 **零配置** - 开箱即用，默认启用所有优化

## 🚀 快速开始

### 1. 添加命名空间

```xml
xmlns:ibv="clr-namespace:ImageBufferView.Avalonia;assembly=ImageBufferView.Avalonia"
```

### 2. 使用控件

```xml
<ibv:ImageBufferView
    ImageBuffer="{Binding ImageBuffer}"
    Stretch="UniformToFill"
    StretchDirection="Both" />
```

## 📋 使用场景

| 场景 | 说明 | 推荐配置 |
|------|------|----------|
| 📹 **摄像头实时预览** | USB/IP 摄像头视频流显示 | 默认配置即可 |
| 🎬 **图片序列播放**   | 连续图片帧播放、动画    | 默认配置即可 |
| 🖼️ **高频图片刷新**   | 需要频繁更新显示的图片  | 默认配置即可 |
| 📊 **静态图片显示**   | 偶尔更新的图片          | `EnableOptimization="False"` |

## ⚙️ 属性说明

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `ImageBuffer`        | `ArraySegment<byte>?`     | `null`          | 图片二进制数据（JPEG/PNG 等） |
| `SourceView`         | `ImageBufferView?`        | `null`          | 复用其他 ImageBufferView 的画面 |
| `Bitmap`             | `Bitmap?`                 | `null`          | 当前显示的 Bitmap（只读） |
| `EnableOptimization` | `bool`                    | `true`          | 启用性能优化（预缩放 + 缓冲区复用） |
| `Stretch`            | `Stretch`                 | `None`          | 图片拉伸模式   |
| `StretchDirection`   | `StretchDirection`        | `Both`          | 拉伸方向限制   |
| `InterpolationMode`  | `BitmapInterpolationMode` | `MediumQuality` | 插值质量       |
| `DefaultBackground`  | `IBrush?`                 | `null`          | 无图片时的背景 |

## 💡 示例

### 摄像头实时预览

```xml
<ibv:ImageBufferView
    Width="640"
    Height="480"
    ImageBuffer="{Binding Camera.CurrentFrame}"
    InterpolationMode="LowQuality"
    Stretch="UniformToFill">
    <ibv:ImageBufferView.DefaultBackground>
        <ImageBrush Source="/Assets/Camera.png" Stretch="UniformToFill" />
    </ibv:ImageBufferView.DefaultBackground>
</ibv:ImageBufferView>
```

### 复用其他控件的画面

```xml
<!-- 主控件：解码图片 -->
<ibv:ImageBufferView
    Name="FirstPic"
    ImageBuffer="{Binding ImageBuffer}"
    Stretch="UniformToFill" />

<!-- 复用控件：共享 FirstPic 的 Bitmap，避免重复解码 -->
<ibv:ImageBufferView
    SourceView="{Binding ElementName=FirstPic}"
    Stretch="Uniform" />
```

### ViewModel 示例

```csharp
public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private ArraySegment<byte>? imageBuffer;

    public async Task StartCameraAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            // 从摄像头获取帧数据
            var frameData = await camera.CaptureFrameAsync();
            ImageBuffer = new ArraySegment<byte>(frameData);
        }
    }
}
```

### 图片序列播放

```csharp
public async Task PlayImageSequenceAsync(string[] imagePaths, CancellationToken token)
{
    var buffers = imagePaths.Select(File.ReadAllBytes).ToList();

    while (!token.IsCancellationRequested)
    {
        foreach (var buffer in buffers)
        {
            if (token.IsCancellationRequested) break;

            ImageBuffer = new ArraySegment<byte>(buffer);
            await Task.Delay(33); // ~30 FPS
        }
    }
}
```

## 🔧 性能优化说明

`EnableOptimization="True"`（默认）时启用以下优化：

1. **预缩放优化** - 当源图片大于渲染区域时，使用 SkiaSharp 预先缩小图片，减少 GPU 负担
2. **缓冲区复用** - 复用 WriteableBitmap 对象，减少内存分配和 GC 压力
3. **智能检测**   - 自动检测分辨率变化，在需要时重置缓冲区

### 性能提升参考

| 场景 | 性能提升 |
|------|----------|
| 1080p → 720p 显示 | ~40-50% |
| 固定分辨率流       | ~20-30% |
| 动态分辨率流       | ~10-20% |

