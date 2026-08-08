using System.Buffers.Binary;
using System.IO;
using System.Text.Json;

namespace CryptoBook.Infrastructure;

/// <summary>
/// Бинарный контейнер: сигнатура, JSON-метаданные и исходные байты документа
/// без Base64. Поток после чтения заголовка остаётся на начале документа.
/// </summary>
public static class BinarySnapshotEnvelope
{
    private const int LengthSize = sizeof(int);
    private const int MaximumMetadataLength = 1024 * 1024;

    public static async Task WriteHeaderAsync<TMetadata>(
        Stream destination,
        ReadOnlyMemory<byte> magic,
        TMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if(magic.Length < 4)
            throw new ArgumentException("Snapshot magic is too short.", nameof(magic));

        byte[] metadataBytes = JsonSerializer.SerializeToUtf8Bytes(metadata);
        try
        {
            if(metadataBytes.Length > MaximumMetadataLength)
                throw new InvalidDataException("Snapshot metadata is too large.");

            byte[] length = new byte[LengthSize];
            BinaryPrimitives.WriteInt32LittleEndian(length, metadataBytes.Length);
            await destination.WriteAsync(magic, cancellationToken);
            await destination.WriteAsync(length, cancellationToken);
            await destination.WriteAsync(metadataBytes, cancellationToken);
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(
                metadataBytes);
        }
    }

    public static async Task<TMetadata?> TryReadHeaderAsync<TMetadata>(
        Stream source,
        ReadOnlyMemory<byte> magic,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if(!source.CanSeek)
            throw new ArgumentException("Snapshot stream must be seekable.", nameof(source));

        long initialPosition = source.Position;
        byte[] actualMagic = new byte[magic.Length];
        int magicRead = await ReadUpToAsync(
            source,
            actualMagic,
            cancellationToken);
        if(magicRead != magic.Length ||
           !actualMagic.AsSpan().SequenceEqual(magic.Span))
        {
            source.Position = initialPosition;
            return default;
        }

        byte[] lengthBytes = new byte[LengthSize];
        await source.ReadExactlyAsync(lengthBytes, cancellationToken);
        int metadataLength = BinaryPrimitives.ReadInt32LittleEndian(lengthBytes);
        if(metadataLength is <= 0 or > MaximumMetadataLength ||
           metadataLength > source.Length - source.Position)
        {
            throw new InvalidDataException("Snapshot metadata length is invalid.");
        }

        byte[] metadataBytes = new byte[metadataLength];
        try
        {
            await source.ReadExactlyAsync(metadataBytes, cancellationToken);
            return JsonSerializer.Deserialize<TMetadata>(metadataBytes)
                ?? throw new InvalidDataException("Snapshot metadata is invalid.");
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(
                metadataBytes);
        }
    }

    public static Stream OpenPayloadStream(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if(!source.CanRead || !source.CanSeek)
            throw new ArgumentException("Snapshot stream must be readable and seekable.", nameof(source));
        return new PayloadStream(source, source.Position);
    }

    private static async Task<int> ReadUpToAsync(
        Stream source,
        Memory<byte> destination,
        CancellationToken cancellationToken)
    {
        int total = 0;
        while(total < destination.Length)
        {
            int read = await source.ReadAsync(
                destination[total..],
                cancellationToken);
            if(read == 0)
                break;
            total += read;
        }
        return total;
    }

    private sealed class PayloadStream(Stream source, long origin): Stream
    {
        public override bool CanRead => source.CanRead;
        public override bool CanSeek => source.CanSeek;
        public override bool CanWrite => false;
        public override long Length => source.Length - origin;
        public override long Position
        {
            get => source.Position - origin;
            set
            {
                if(value < 0 || value > Length)
                    throw new ArgumentOutOfRangeException(nameof(value));
                source.Position = origin + value;
            }
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            source.Read(buffer, offset, count);

        public override int Read(Span<byte> buffer) => source.Read(buffer);

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            source.ReadAsync(buffer, cancellationToken);

        public override long Seek(long offset, SeekOrigin seekOrigin)
        {
            long target = seekOrigin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => Position + offset,
                SeekOrigin.End => Length + offset,
                _ => throw new ArgumentOutOfRangeException(nameof(seekOrigin))
            };
            Position = target;
            return Position;
        }

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }
}
