using System.IO;

namespace CryptoBook.Services;

internal sealed class ArchivePasswordRequiredException: IOException
{
    public ArchivePasswordRequiredException() : base(ArchivePathPolicy.Text("PasswordRequired")) { }
}
