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
    /// BGR 24 位（每像素 3 字节：蓝、绿、红，无 Alpha）
    /// </summary>
    Bgr24,

    /// <summary>
    /// RGB 24 位（每像素 3 字节：红、绿、蓝，无 Alpha）
    /// </summary>
    Rgb24,

    /// <summary>
    /// RGB 565（每像素 2 字节，打包格式：R[15:11] G[10:5] B[4:0]）
    /// </summary>
    Rgb565,

    /// <summary>
    /// 灰度 8 位（每像素 1 字节）
    /// </summary>
    Gray8,
}