namespace ImageBufferView.Avalonia;

/// <summary>
/// 源图片分辨率提示，用于优化缓冲区复用策略
/// </summary>
public enum SourceResolutionHint
{
    /// <summary>
    /// 未知/混合模式（默认，最保守）
    /// 不复用任何缓冲区，适用于：
    /// - 源图片分辨率不一致且有大有小
    /// - 源图片分辨率不一致，但都小于渲染区
    /// - 不确定源图片情况
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// 源图片分辨率固定
    /// 可复用 WriteableBitmap 和缩放缓冲区，适用于：
    /// - 摄像头视频流等固定分辨率场景
    /// - 源图片都一样分辨率（无论大于或小于渲染区）
    /// 性能提升：20-30%
    /// </summary>
    Fixed,

    /// <summary>
    /// 源图片分辨率不固定，但都大于渲染区域
    /// 仅可复用目标 WriteableBitmap（因为预缩放后尺寸固定为渲染区大小）
    /// 性能提升：10-20%
    /// </summary>
    VariableLargerThanRender,
}
