using CryptoBook.Interfaces;

using System.Text;

namespace CryptoBook.Services
{
    internal static class MarkdownTextCodec
    {
        private static readonly UTF8Encoding Utf8Strict = new(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);

        public static MarkdownTextDocument Decode(
            ReadOnlyMemory<byte> content)
        {
            ReadOnlySpan<byte> bytes = content.Span;
            (Encoding encoding, int preambleLength) = DetectEncoding(bytes);
            string text;
            try
            {
                text = encoding.GetString(bytes[preambleLength..]);
            }
            catch(DecoderFallbackException)
            {
                // Keep the editor usable for legacy files while making UTF-8
                // the deterministic encoding used after the next save.
                encoding = new UTF8Encoding(false);
                preambleLength = 0;
                text = encoding.GetString(bytes);
            }

            return new MarkdownTextDocument(
                text,
                encoding,
                bytes[..preambleLength].ToArray());
        }

        public static byte[] Encode(MarkdownTextDocument document)
        {
            ArgumentNullException.ThrowIfNull(document);
            byte[] body = document.Encoding.GetBytes(document.Text);
            if(document.Preamble.Length == 0)
                return body;

            byte[] result = new byte[document.Preamble.Length + body.Length];
            document.Preamble.CopyTo(result, 0);
            body.CopyTo(result, document.Preamble.Length);
            return result;
        }

        private static (Encoding Encoding, int PreambleLength) DetectEncoding(
            ReadOnlySpan<byte> bytes)
        {
            if(bytes.Length >= 3 &&
               bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                return (Utf8Strict, 3);
            if(bytes.Length >= 4 &&
               bytes[0] == 0xFF && bytes[1] == 0xFE &&
               bytes[2] == 0x00 && bytes[3] == 0x00)
                return (new UTF32Encoding(false, false, true), 4);
            if(bytes.Length >= 4 &&
               bytes[0] == 0x00 && bytes[1] == 0x00 &&
               bytes[2] == 0xFE && bytes[3] == 0xFF)
                return (new UTF32Encoding(true, false, true), 4);
            if(bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
                return (new UnicodeEncoding(false, false, true), 2);
            if(bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
                return (new UnicodeEncoding(true, false, true), 2);
            return (Utf8Strict, 0);
        }
    }
}
