using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace ImageBufferView.Avalonia;

public partial class ImageBufferView
{
    protected override Size MeasureOverride(Size availableSize)
    {
        return Bitmap is not null
            ? Stretch.CalculateSize(availableSize, GetEffectiveSourceSize(), StretchDirection)
            : base.MeasureOverride(availableSize);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        return Bitmap is not null
            ? Stretch.CalculateSize(finalSize, GetEffectiveSourceSize())
            : base.ArrangeOverride(finalSize);
    }

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
                    var sourceSize = SourceSize;
                    var origSize = _originalSourceSize is { Width: > 0, Height: > 0 } ? _originalSourceSize : sourceSize;
                    var isSwapped = rotation is ImageRotation.Rotate90 or ImageRotation.Rotate270;
                    var layoutSize = isSwapped ? new Size(origSize.Height, origSize.Width) : origSize;

                    var scale = Stretch.CalculateScaling(RenderSize, layoutSize, StretchDirection);

                    if (scale is { X: > 0.0, Y: > 0.0 })
                    {
                        var fullDest = viewPort.CenterRect(new Rect(layoutSize * scale));
                        var center = fullDest.Center;

                        var drawRect = isSwapped
                            ? new Rect(center.X - fullDest.Height / 2, center.Y - fullDest.Width / 2,
                                       fullDest.Height, fullDest.Width)
                            : fullDest;

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

                        var rad = angle * System.Math.PI / 180.0;
                        var c = System.Math.Cos(rad);
                        var s = System.Math.Sin(rad);

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
}
