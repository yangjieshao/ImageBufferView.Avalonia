using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using System;
using System.Buffers;

namespace ImageBufferView.Avalonia;

public partial class ImageBufferView
{
    // 将 Decode.cs 中与缓冲重用相关的方法移动到此文件，保持逻辑不变。
    private void RecycleToBackBuffer(WriteableBitmap bitmap)
    {
        if (!_cachedEnableOptimization)
        {
            bitmap.Dispose();
            return;
        }

        lock (_backBufferLock)
        {
            var size = bitmap.PixelSize;
            var format = bitmap.Format ?? PixelFormats.Bgra8888;

            if (_backBuffer is null || _backBufferSize != size || _backBufferFormat != format)
            {
                _backBuffer?.Dispose();
                _backBuffer = bitmap;
                _backBufferSize = size;
                _backBufferFormat = format;
            }
            else
            {
                bitmap.Dispose();
            }
        }
    }

    private void ClearBackBuffer()
    {
        WriteableBitmap? bitmapToDispose;

        lock (_backBufferLock)
        {
            bitmapToDispose = _backBuffer;
            _backBuffer = null;
            _backBufferSize = default;
            Volatile.Write(ref _lastDecodedSourceSizeBox, null);
        }

        bitmapToDispose?.Dispose();
    }

    private WriteableBitmap? ConvertSkBitmapToAvaloniaWithReuse(
        SkiaSharp.SKBitmap skBitmap,
        bool enableReuse)
    {
        var info = skBitmap.Info;
        var pixelFormat = info.ColorType switch
        {
            SkiaSharp.SKColorType.Rgba8888 => PixelFormats.Rgba8888,
            SkiaSharp.SKColorType.Bgra8888 => PixelFormats.Bgra8888,
            _ => PixelFormats.Bgra8888
        };

        SkiaSharp.SKBitmap? convertedBitmap = null;
        var bitmapToUse = skBitmap;

        if (info.ColorType != SkiaSharp.SKColorType.Bgra8888 && info.ColorType != SkiaSharp.SKColorType.Rgba8888)
        {
            convertedBitmap = new SkiaSharp.SKBitmap(info.Width, info.Height, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
            using var canvas = new SkiaSharp.SKCanvas(convertedBitmap);
            canvas.DrawBitmap(skBitmap, 0, 0);
            bitmapToUse = convertedBitmap;
            pixelFormat = PixelFormats.Bgra8888;
        }

        try
        {
            var pixels = bitmapToUse.GetPixels();
            var rowBytes = bitmapToUse.RowBytes;
            var width = bitmapToUse.Width;
            var height = bitmapToUse.Height;
            var currentSize = new PixelSize(width, height);

            var canReuse = enableReuse;

            WriteableBitmap? bitmap = null;

            if (canReuse)
            {
                lock (_backBufferLock)
                {
                    if (_backBuffer is not null &&
                        _backBufferSize == currentSize &&
                        _backBufferFormat == pixelFormat)
                    {
                        bitmap = _backBuffer;
                        _backBuffer = null;
                    }
                }
            }

            bitmap ??= new WriteableBitmap(
                currentSize,
                new Vector(96, 96),
                pixelFormat,
                AlphaFormat.Premul);

            using var fb = bitmap.Lock();
            unsafe
            {
                Buffer.MemoryCopy(
                    (void*)pixels,
                    (void*)fb.Address,
                    fb.RowBytes * height,
                    rowBytes * height);
            }

            return bitmap;
        }
        catch
        {
            return null;
        }
        finally
        {
            convertedBitmap?.Dispose();
        }
    }
}
