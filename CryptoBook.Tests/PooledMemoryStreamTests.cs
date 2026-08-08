using CryptoBook.Infrastructure;

using Xunit;

namespace CryptoBook.Tests;

public sealed class PooledMemoryStreamTests
{
    [Fact]
    public void ReadWriteSeek_CrossesBlockBoundariesAndPreservesStreamSemantics()
    {
        byte[] source = new byte[200_000];
        new Random(42).NextBytes(source);
        using var stream = new PooledMemoryStream();

        stream.Write(source);
        stream.Position = 60_000;
        byte[] slice = new byte[100_000];
        int read = stream.Read(slice);

        Assert.Equal(slice.Length, read);
        Assert.Equal(source.AsSpan(60_000, slice.Length).ToArray(), slice);
        stream.Position = source.Length + 10;
        stream.WriteByte(123);
        Assert.Equal(source.Length + 11, stream.Length);
        stream.Position = source.Length;
        Assert.Equal(0, stream.ReadByte());
        stream.Position = source.Length + 10;
        Assert.Equal(123, stream.ReadByte());
    }
}
