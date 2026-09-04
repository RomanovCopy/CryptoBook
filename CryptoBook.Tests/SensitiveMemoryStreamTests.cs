using CryptoBook.Security;

using Xunit;

namespace CryptoBook.Tests;

public sealed class SensitiveMemoryStreamTests
{
    [Fact]
    public void Dispose_ClearsEntireAllocatedBuffer()
    {
        byte[] allocatedBuffer;
        using(var stream = new SensitiveMemoryStream(maximumLength: 1024))
        {
            stream.Write([1, 2, 3, 4]);
            allocatedBuffer = stream.GetBuffer();
            Assert.Contains(allocatedBuffer, value => value != 0);
        }

        Assert.All(allocatedBuffer, value => Assert.Equal(0, value));
    }
}
