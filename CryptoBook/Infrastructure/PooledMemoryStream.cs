using System.Buffers;
using System.IO;
using System.Security.Cryptography;

namespace CryptoBook.Infrastructure;

/// <summary>
/// Seekable-поток из небольших арендованных блоков. Не создаёт растущий
/// непрерывный массив в LOH и очищает блоки перед возвратом в общий пул.
/// </summary>
public sealed class PooledMemoryStream: Stream
{
    private const int BlockSize = 64 * 1024;
    private readonly List<byte[]> blocks = [];
    private long length;
    private long position;
    private bool disposed;

    public override bool CanRead => !disposed;
    public override bool CanSeek => !disposed;
    public override bool CanWrite => !disposed;
    public override long Length
    {
        get
        {
            ThrowIfDisposed();
            return length;
        }
    }
    public override long Position
    {
        get
        {
            ThrowIfDisposed();
            return position;
        }
        set
        {
            ThrowIfDisposed();
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            position = value;
        }
    }

    public override void Flush() => ThrowIfDisposed();

    public override int Read(byte[] buffer, int offset, int count)
    {
        ValidateBuffer(buffer, offset, count);
        return Read(buffer.AsSpan(offset, count));
    }

    public override int Read(Span<byte> buffer)
    {
        ThrowIfDisposed();
        if(position >= length)
            return 0;
        int remaining = (int)Math.Min(buffer.Length, length - position);
        int total = remaining;
        while(remaining > 0)
        {
            int blockIndex = checked((int)(position / BlockSize));
            int blockOffset = (int)(position % BlockSize);
            int count = Math.Min(remaining, BlockSize - blockOffset);
            blocks[blockIndex].AsSpan(blockOffset, count)
                .CopyTo(buffer[(total - remaining)..]);
            position += count;
            remaining -= count;
        }
        return total;
    }

    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Read(buffer.Span));
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        ThrowIfDisposed();
        long target = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => checked(position + offset),
            SeekOrigin.End => checked(length + offset),
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };
        ArgumentOutOfRangeException.ThrowIfNegative(target);
        position = target;
        return position;
    }

    public override void SetLength(long value)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        EnsureCapacity(value);
        if(value != length)
            ClearRange(Math.Min(value, length), Math.Abs(value - length));
        length = value;
        if(position > value)
            position = value;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        ValidateBuffer(buffer, offset, count);
        Write(buffer.AsSpan(offset, count));
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        ThrowIfDisposed();
        long end = checked(position + buffer.Length);
        EnsureCapacity(end);
        if(position > length)
            ClearRange(length, position - length);
        int remaining = buffer.Length;
        int sourceOffset = 0;
        while(remaining > 0)
        {
            int blockIndex = checked((int)(position / BlockSize));
            int blockOffset = (int)(position % BlockSize);
            int count = Math.Min(remaining, BlockSize - blockOffset);
            buffer.Slice(sourceOffset, count)
                .CopyTo(blocks[blockIndex].AsSpan(blockOffset, count));
            position += count;
            sourceOffset += count;
            remaining -= count;
        }
        if(position > length)
            length = position;
    }

    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Write(buffer.Span);
        return ValueTask.CompletedTask;
    }

    protected override void Dispose(bool disposing)
    {
        if(disposed)
            return;
        disposed = true;
        foreach(byte[] block in blocks)
        {
            CryptographicOperations.ZeroMemory(block.AsSpan(0, BlockSize));
            ArrayPool<byte>.Shared.Return(block);
        }
        blocks.Clear();
        length = 0;
        position = 0;
        base.Dispose(disposing);
    }

    private void EnsureCapacity(long value)
    {
        long requiredBlocks = (value + BlockSize - 1) / BlockSize;
        if(requiredBlocks > int.MaxValue)
            throw new IOException("Stream is too large.");
        while(blocks.Count < requiredBlocks)
            blocks.Add(ArrayPool<byte>.Shared.Rent(BlockSize));
    }

    private void ClearRange(long start, long count)
    {
        while(count > 0)
        {
            int blockIndex = checked((int)(start / BlockSize));
            int blockOffset = (int)(start % BlockSize);
            int chunk = (int)Math.Min(count, BlockSize - blockOffset);
            blocks[blockIndex].AsSpan(blockOffset, chunk).Clear();
            start += chunk;
            count -= chunk;
        }
    }

    private static void ValidateBuffer(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if(buffer.Length - offset < count)
            throw new ArgumentException("Offset and count exceed the buffer.");
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(disposed, this);
}
