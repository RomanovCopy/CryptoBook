using System.IO;

namespace CryptoBook.Security
{
    public sealed class SecureFileValidator: ISecureFileValidator
    {
        public async Task<bool> HasCryptoBookHeaderAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            await using FileStream stream = new(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            int requiredLength = Math.Max(
                SecureFileFormat.MagicHeader.Length,
                SecureFileFormat.V2MagicHeader.Length);
            byte[] header = new byte[requiredLength];
            int bytesRead = 0;

            while(bytesRead < header.Length)
            {
                int read = await stream.ReadAsync(
                    header.AsMemory(bytesRead),
                    cancellationToken);
                if(read == 0)
                    break;
                bytesRead += read;
            }

            return HasPrefix(header, bytesRead, SecureFileFormat.MagicHeader) ||
                   HasPrefix(header, bytesRead, SecureFileFormat.V2MagicHeader);
        }

        private static bool HasPrefix(
            byte[] content,
            int contentLength,
            byte[] expected)
        {
            return contentLength >= expected.Length &&
                   content.AsSpan(0, expected.Length).SequenceEqual(expected);
        }
    }
}
