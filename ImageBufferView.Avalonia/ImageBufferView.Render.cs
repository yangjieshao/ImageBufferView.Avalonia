using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Reactive;
using Avalonia.Threading;
using System;
using System.Buffers;
using System.Threading;

namespace ImageBufferView.Avalonia;

/// <summary>
/// 表示一个可从字节缓冲解码并显示的图像控件，包含预缩放和缓冲复用优化。
/// </summary>
public partial class ImageBufferView
{
    /// <summary>
    /// 若控件已附加到可视树且 <see cref="ImageBuffer"/> 有有效内容，则触发一次解码刷新。
    /// </summary>
    private void TryDecodeCurrentBuffer()
    {
        if (this is { _isAttached: true, ImageBuffer: { Array: not null, Count: > 0 } buffer })
        {
            TryStartDecode(buffer);
        }
    }

    /// <summary>
    /// 解码循环：从最新缓冲读取、限制并发、解码并在 UI 线程发布 Bitmap 更新。
    /// 使用 try/finally 确保所有退出路径（含取消、异常）都重置 _decoding 标志，
    /// 并检查 lost-wakeup 以防止新数据被遗漏。
    /// </summary>
    private void DecodeLoop()
    {
        var token = GetOrCreateSessionToken();

        try
        {
            while (_isAttached && !token.IsCancellationRequested)
            {
                var buffer = Interlocked.Exchange(ref _latestBuffer, null);
                var length = Interlocked.Exchange(ref _latestBufferLength, 0);

                if (buffer is null || length == 0)
                {
                    return;
                }

                Bitmap? newBitmap = null;
                try
                {
                    if (token.IsCancellationRequested)
                        return;

                    // 尝试获取信号量，超时则跳过此帧（避免积压）
                    if (!SDecodeSemaphore.Wait(0, token))
                        continue;

                    try
                    {
                        if (token.IsCancellationRequested)
                            return;

                        // 使用 SkiaSharp 解码并预缩放
                        newBitmap = DecodeAndScaleBitmap(buffer, length);
                    }
                    finally
                    {
                        SDecodeSemaphore.Release();
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch
                {
                    continue;
                }
                finally
                {
                    // buffer 统一在此处归还，避免双归还和泄漏
                    ArrayPool<byte>.Shared.Return(buffer);
                }

                if (newBitmap is null)
                    continue;

                if (!_isAttached || token.IsCancellationRequested)
                {
                    newBitmap.Dispose();
                    return;
                }

                var capturedToken = token;
                var bitmapToSet = newBitmap;

                // 使用 Post 确保 Bitmap 及时更新（GPU 渲染更流畅）
                Dispatcher.UIThread.Post(() =>
                {
                    if (!_isAttached || capturedToken.IsCancellationRequested)
                    {
                        bitmapToSet?.Dispose();
                        return;
                    }

                    var oldBitmap = Bitmap;
                    Bitmap = bitmapToSet;

                    // 将旧 Bitmap 回收到后台缓冲区（如果尺寸匹配）
                    // 并避免释放来自 SourceView 的共享 Bitmap（比较引用）
                    if (oldBitmap is not null && !ReferenceEquals(oldBitmap, SourceView?.Bitmap))
                    {
                        if (oldBitmap is WriteableBitmap oldWriteable)
                            RecycleToBackBuffer(oldWriteable);
                        else
                            oldBitmap.Dispose();
                    }
                }, DispatcherPriority.Render);
            }
        }
        finally
        {
            // 所有退出路径都重置 _decoding 标志
            Volatile.Write(ref _decoding, 0);

            // lost-wakeup 检查：如果有新数据到达但 DecodeLoop 已退出，重新排队
            if (_isAttached && _latestBuffer is not null &&
                Interlocked.CompareExchange(ref _decoding, 1, 0) == 0)
            {
                ThreadPool.UnsafeQueueUserWorkItem(static ctrl => ctrl.DecodeLoop(), this, preferLocal: false);
            }
        }
    }

    /// <summary>
    /// 获取考虑旋转后的有效源图片尺寸（90°/270° 时交换宽高）。
    /// 优先使用原始（未预缩放）分辨率以避免预缩放 ↔ 布局反馈振荡。
    /// </summary>
    private Size GetEffectiveSourceSize()
    {
        var src = _originalSourceSize is { Width: > 0, Height: > 0 } ? _originalSourceSize : SourceSize;
        return Rotation is ImageRotation.Rotate90 or ImageRotation.Rotate270
            ? new Size(src.Height, src.Width)
            : src;
    }

    /// <summary>
    /// 测量覆盖：根据 Bitmap 和 Stretch 计算所需大小
    /// </summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        return Bitmap is not null
            ? Stretch.CalculateSize(availableSize, GetEffectiveSourceSize(), StretchDirection)
            : base.MeasureOverride(availableSize);
    }

    /// <summary>
    /// 排列覆盖：根据 Bitmap 和 Stretch 计算最终大小
    /// </summary>
    protected override Size ArrangeOverride(Size finalSize)
    {
        return Bitmap is not null
            ? Stretch.CalculateSize(finalSize, GetEffectiveSourceSize())
            : base.ArrangeOverride(finalSize);
    }

    /// <summary>
    /// 渲染逻辑：计算源/目标矩形并绘制 Bitmap 或默认背景
    /// </summary>
    public override void Render(DrawingContext drawingContext)
    {
        if (RenderSize is { Width: > 0.0, Height: > 0.0 })
        {
            if (Bitmap is not null && SourceSize is { Width: > 0.0, Height: > 0.0 })
            {
                var viewPort = new Rect(RenderSize);

                var rotation = Rotation;
                var flipH = FlipHorizontal;
                var flipV = FlipVertical;

                // Fast path: no rotation nor flip -> original drawing logic
                if (rotation == ImageRotation.Rotate0 && !flipH && !flipV)
                {
                    var sourceSize = SourceSize;
                    var scale = Stretch.CalculateScaling(RenderSize, sourceSize, StretchDirection);

                    if (scale is { X: > 0.0, Y: > 0.0 })
                    {
                        var scaledSize = sourceSize * scale;
                        var destRect = viewPort.CenterRect(new Rect(scaledSize)).Intersect(viewPort);

                        if (destRect is { Width: > 0.0, Height: > 0.0 })
                        {
                            var sourceRect = new Rect(sourceSize).CenterRect(new Rect(destRect.Size / scale));
                            drawingContext.DrawImage(Bitmap, sourceRect, destRect);
                        }
                    }
                }
                else
                {
                    // Transform path: use original source size for layout to avoid pre-scale feedback jitter
                    var sourceSize = SourceSize; // bitmap pixel dimensions for sourceRect
                    var origSize = _originalSourceSize is { Width: > 0, Height: > 0 } ? _originalSourceSize : sourceSize;
                    var isSwapped = rotation is ImageRotation.Rotate90 or ImageRotation.Rotate270;
                    var layoutSize = isSwapped ? new Size(origSize.Height, origSize.Width) : origSize;

                    var scale = Stretch.CalculateScaling(RenderSize, layoutSize, StretchDirection);

                    if (scale is { X: > 0.0, Y: > 0.0 })
                    {
                        // fullDest: the final on-screen rect after rotation (may exceed viewport for UniformToFill)
                        var fullDest = viewPort.CenterRect(new Rect(layoutSize * scale));
                        var center = fullDest.Center;

                        // drawRect: the rect we actually draw the bitmap into BEFORE rotation.
                        // For 90/270 the bitmap is landscape but the layout is portrait (or vice-versa),
                        // so swap width/height so the bitmap keeps its native aspect ratio.
                        var drawRect = isSwapped
                            ? new Rect(center.X - fullDest.Height / 2, center.Y - fullDest.Width / 2,
                                       fullDest.Height, fullDest.Width)
                            : fullDest;

                        // sourceRect covers the full bitmap in its original (un-rotated) pixel space
                        var sourceRect = new Rect(sourceSize);

                        var sx = flipH ? -1.0 : 1.0;
                        var sy = flipV ? -1.0 : 1.0;

                        var angle = rotation switch
                        {
                            ImageRotation.Rotate90 => 90.0,
                            ImageRotation.Rotate180 => 180.0,
                            ImageRotation.Rotate270 => 270.0,
                            _ => 0.0
                        };

                        // Compose matrix: Scale(flip) then Rotate, all about center
                        var rad = angle * Math.PI / 180.0;
                        var c = Math.Cos(rad);
                        var s = Math.Sin(rad);

                        var L00 = c * sx;
                        var L01 = -s * sy;
                        var L10 = s * sx;
                        var L11 = c * sy;

                        var offsetX = -L00 * center.X - L01 * center.Y + center.X;
                        var offsetY = -L10 * center.X - L11 * center.Y + center.Y;

                        var m = new Matrix(L00, L10, L01, L11, offsetX, offsetY);

                        using (drawingContext.PushClip(viewPort))
                        using (drawingContext.PushTransform(m))
                        {
                            drawingContext.DrawImage(Bitmap, sourceRect, drawRect);
                        }
                    }
                }
            }
            else if (DefaultBackground is { } background)
            {
                drawingContext.FillRectangle(background, new Rect(RenderSize));
            }
        }

        base.Render(drawingContext);
    }

    /// <summary>
    /// 当控件附加到可视树时启动解码流程（如已有缓冲则立即开始）
    /// </summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _isAttached = true;
        _cachedRenderSize = Bounds.Size;
        // 处理在 attach 之前已设置的 ImageBuffer
        if (ImageBuffer is { Array: not null, Count: > 0 } buffer)
        {
            TryStartDecode(buffer);
        }
    }

    /// <summary>
    /// 当控件从可视树分离时取消会话并清理资源
    /// </summary>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _isAttached = false;
        CancelCurrentSession();

        // 清理 SourceView 订阅
        _sourceViewSubscription?.Dispose();
        _sourceViewSubscription = null;

        // 清理 Bitmap（仅当不是从 SourceView 复用的情况）
        if (SourceView is null)
        {
            var oldBitmap = Bitmap;
            Bitmap = null;
            oldBitmap?.Dispose();
        }
        else
        {
            Bitmap = null;
        }

        // 清理后台缓冲区
        ClearBackBuffer();
        _originalSourceSize = default;

        base.OnDetachedFromVisualTree(e);
    }
}