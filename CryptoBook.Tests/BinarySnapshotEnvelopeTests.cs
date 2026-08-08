using CryptoBook.Infrastructure;

using System.IO;
using System.Text;

using Xunit;

namespace CryptoBook.Tests;

public sealed class BinarySnapshotEnvelopeTests
{
    [Fact]
    public async Task BinaryEnvelope_StoresDocumentWithoutBase64Expansion()
    {
        byte[] magic = Encoding.ASCII.GetBytes("CBTEST02");
        byte[] document = new byte[5 * 1024 * 1024];
        new Random(42).NextBytes(document);
        var metadata = new TestMetadata("sample.xamlpackage", 17);
        await using var envelope = new MemoryStream();

        await BinarySnapshotEnvelope.WriteHeaderAsync(
            envelope,
            magic,
            metadata);
        await envelope.WriteAsync(document);

        Assert.InRange(envelope.Length, document.Length, document.Length + 1_024);
        envelope.Position = 0;
        TestMetadata? restored = await BinarySnapshotEnvelope
            .TryReadHeaderAsync<TestMetadata>(envelope, magic);
        await using Stream payload =
            BinarySnapshotEnvelope.OpenPayloadStream(envelope);
        using var restoredDocument = new MemoryStream();
        await payload.CopyToAsync(restoredDocument);

        Assert.Equal(metadata, restored);
        Assert.Equal(document, restoredDocument.ToArray());
    }

    private sealed record TestMetadata(string Name, long Revision);
}
