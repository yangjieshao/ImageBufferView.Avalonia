namespace ImageBufferView.Avalonia;

/// <summary>
/// 源图片分辨率提示，用于优化缓冲区复用策略
/// </summary>
public enum SourceResolutionHint
{
    /// <summary>
    /// 未知/混合模式（默认，最保守）
    /// 不复用任何缓冲区，每帧都创建新的 WriteableBitmap
    /// 适用于不确定源图片情况或对内存敏感的场景
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// 启用缓冲区复用优化
    /// 自动检测分辨率变化并智能复用缓冲区
    /// 适用于：
    /// - 摄像头视频流（固定分辨率）
    /// - 图片序列播放
    /// - 任何需要高帧率显示的场景
    /// 性能提升：减少 ~50% 内存分配，降低 GC 压力
    /// </summary>
    EnableReuse,
}
