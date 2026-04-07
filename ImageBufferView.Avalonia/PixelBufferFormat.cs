namespace ImageBufferView.Avalonia;

/// <summary>
/// 原始像素缓冲格式，对应 Avalonia.Platform.PixelFormats 中 WriteableBitmap 原生支持的格式子集。
/// 不支持的格式（如 YUV 等）应在数据源端先编码为 JPEG/PNG，再以 <see cref="Encoded"/> 传入。
/// </summary>
public enum PixelBufferFormat
{
    /// <summary>
    /// 编码图片格式（适用于 JPEG/PNG 等标准编码格式，默认值）
    /// </summary>
    Encoded,

    /// <summary>
    /// BGRA 32 位（每像素 4 字节：蓝、绿、红、透明，预乘 Alpha）
    /// </summary>
    Bgra32,

    /// <summary>
    /// RGBA 32 位（每像素 4 字节：红、绿、蓝、透明，预乘 Alpha）
    /// </summary>
    Rgba32,

    /// <summary>
    /// BGR 32 位（每像素 4 字节：蓝、绿、红、填充，无 Alpha）
    /// </summary>
    Bgr32,

    /// <summary>
    /// RGB 32 位（每像素 4 字节：红、绿、蓝、填充，无 Alpha）
    /// </summary>
    Rgb32,

    /// <summary>
    /// RGB 565（每像素 2 字节，打包格式：R[15:11] G[10:5] B[4:0]）
    /// </summary>
    Rgb565,

    /// <summary>
    /// 灰度 8 位（每像素 1 字节）
    /// </summary>
    Gray8,

    /// <summary>
    /// Alpha 8 位（每像素 1 字节，仅 Alpha 通道）
    /// </summary>
    Alpha8,

    /// <summary>
    /// ARGB 4444（每像素 2 字节：A[15:12] R[11:8] G[7:4] B[3:0]）
    /// </summary>
    Argb4444,

    /// <summary>
    /// RGBA 1010102（每像素 4 字节：R10 G10 B10 A2，高动态范围格式）
    /// </summary>
    Rgba1010102,

    /// <summary>
    /// BGRA 1010102（每像素 4 字节：B10 G10 R10 A2，高动态范围格式）
    /// </summary>
    Bgra1010102,

    /// <summary>
    /// RGB 101010x（每像素 4 字节：R10 G10 B10 X2，高动态范围不透明格式）
    /// </summary>
    Rgb101010x,

    /// <summary>
    /// BGR 101010x（每像素 4 字节：B10 G10 R10 X2，高动态范围不透明格式）
    /// </summary>
    Bgr101010x,

    /// <summary>
    /// sRGBA 8888（每像素 4 字节，sRGB 色彩空间）
    /// </summary>
    Srgba8888,

    /// <summary>
    /// RG 88（每像素 2 字节：R8 G8，双通道格式）
    /// </summary>
    Rg88,

    /// <summary>
    /// RGBA F16（每像素 8 字节，半精度浮点格式）
    /// </summary>
    RgbaF16,

    /// <summary>
    /// RGBA F16 Clamped（每像素 8 字节，半精度浮点格式，值限制在 0.0-1.0）
    /// </summary>
    RgbaF16Clamped,

    /// <summary>
    /// RGBA F32（每像素 16 字节，单精度浮点格式）
    /// </summary>
    RgbaF32,

    /// <summary>
    /// Alpha 16（每像素 2 字节，16 位 Alpha 通道）
    /// </summary>
    Alpha16,

    /// <summary>
    /// RG 1616（每像素 4 字节：R16 G16，双通道 16 位格式）
    /// </summary>
    Rg1616,

    /// <summary>
    /// RGBA 16161616（每像素 8 字节，每通道 16 位）
    /// </summary>
    Rgba16161616,

    /// <summary>
    /// Alpha F16（每像素 2 字节，半精度浮点 Alpha 通道）
    /// </summary>
    AlphaF16,

    /// <summary>
    /// RG F16（每像素 4 字节：R16f G16f，双通道半精度浮点格式）
    /// </summary>
    RgF16,

    /// <summary>
    /// R8 Unorm（每像素 1 字节，单通道 8 位归一化格式）
    /// </summary>
    R8Unorm,
}