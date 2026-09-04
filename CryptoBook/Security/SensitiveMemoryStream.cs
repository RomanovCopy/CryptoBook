using CryptoBook.Infrastructure;

using System.IO;
using System.Security.Cryptography;

namespace CryptoBook.Security;

/// <summary>
/// Memory-backed plaintext storage which enforces a hard size limit and clears
/// its entire allocated buffer when disposed.
/// </summary>
internal sealed class SensitiveMemoryStream: MemoryStream
{
    private readonly long maximumLength;
    private bool cleared;

    public SensitiveMemoryStream(long maximumLength = int.MaxValue)
    {
        if(maximumLength is <= 0 or > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(maximumLength));

        this.maximumLength = maximumLength;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        EnsureWithinLimit(count);
        base.Write(buffer, offset, count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        EnsureWithinLimit(buffer.Length);
        base.Write(buffer);
    }

    public override void WriteByte(byte value)
    {
        EnsureWithinLimit(1);
        base.WriteByte(value);
    }

    public override Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        EnsureWithinLimit(count);
        return base.WriteAsync(buffer, offset, count, cancellationToken);
    }

    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        EnsureWithinLimit(buffer.Length);
        return base.WriteAsync(buffer, cancellationToken);
    }

    public override void SetLength(long value)
    {
        if(value > maximumLength)
            ThrowLimitExceeded();
        base.SetLength(value);
    }

    protected override void Dispose(bool disposing)
    {
        if(!cleared)
        {
            cleared = true;
            if(TryGetBuffer(out ArraySegment<byte> buffer) &&
               buffer.Array is not null)
            {
                CryptographicOperations.ZeroMemory(
                    buffer.Array.AsSpan(
                        buffer.Offset,
                        buffer.Array.Length - buffer.Offset));
            }
        }

        base.Dispose(disposing);
    }

    public override ValueTask DisposeAsync()
    {
        Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    private void EnsureWithinLimit(int bytesToWrite)
    {
        if(bytesToWrite < 0 || Position > maximumLength - bytesToWrite)
            ThrowLimitExceeded();
    }

    private void ThrowLimitExceeded() =>
        throw new IOException(
            LocalizationManager.Format(
                "Media.LegacyRamLimitExceeded",
                maximumLength / (1024d * 1024d)));
}
