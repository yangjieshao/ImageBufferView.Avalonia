using System;
using System.Buffers;
using System.Threading;
using Avalonia.Threading;

namespace ImageBufferView.Avalonia;

public partial class ImageBufferView
{
    private CancellationToken GetOrCreateSessionToken()
    {
        lock (_sessionLock)
        {
            _sessionCts ??= new CancellationTokenSource();
            return _sessionCts.Token;
        }
    }

    private void TryStartDecode(ArraySegment<byte> buffer)
    {
        _cachedRenderSize = Bounds.Size;
        _cachedStretch = Stretch;
        _cachedStretchDirection = StretchDirection;
        _cachedEnableOptimization = EnableOptimization;
        _cachedPixelBufferFormat = PixelBufferFormat;
        _cachedRawImageWidth = RawImageWidth;
        _cachedRawImageHeight = RawImageHeight;

        var pooledBuffer = ArrayPool<byte>.Shared.Rent(buffer.Count);
        buffer.AsSpan().CopyTo(pooledBuffer);

        var oldBuffer = Interlocked.Exchange(ref _latestBuffer, pooledBuffer);
        Interlocked.Exchange(ref _latestBufferLength, buffer.Count);

        if (oldBuffer is not null)
        {
            ArrayPool<byte>.Shared.Return(oldBuffer);
        }

        if (Interlocked.CompareExchange(ref _decoding, 1, 0) == 0)
        {
            ThreadPool.UnsafeQueueUserWorkItem(static ctrl => ctrl.DecodeLoop(), this, preferLocal: false);
        }
    }

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

                SkiaSharp.SKBitmap? newSkBitmap = null;
                try
                {
                    if (token.IsCancellationRequested)
                        return;

                    if (!SDecodeSemaphore.Wait(0, token))
                        continue;

                    try
                    {
                        if (token.IsCancellationRequested)
                            return;

                        var bitmap = DecodeAndScaleBitmap(buffer, length);
                        // DecodeAndScaleBitmap returns WriteableBitmap?; we will set Bitmap on UI thread later
                        newSkBitmap = null; // placeholder
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
                    ArrayPool<byte>.Shared.Return(buffer);
                }

                // Note: actual UI update logic remains in original DecodeLoop in core file to avoid behavior changes.
            }
        }
        finally
        {
            Volatile.Write(ref _decoding, 0);

            if (_isAttached && _latestBuffer is not null &&
                Interlocked.CompareExchange(ref _decoding, 1, 0) == 0)
            {
                ThreadPool.UnsafeQueueUserWorkItem(static ctrl => ctrl.DecodeLoop(), this, preferLocal: false);
            }
        }
    }
}
