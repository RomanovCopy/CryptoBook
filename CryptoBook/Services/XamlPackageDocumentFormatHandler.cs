using CryptoBook.FileTemplates;
using CryptoBook.Interfaces;

using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows;
using System.Xml.Linq;

namespace CryptoBook.Services
{
    public sealed class XamlPackageDocumentFormatHandler:
        TextRangeDocumentFormatHandler<XamlPackageFileTemplate>
    {
        public XamlPackageDocumentFormatHandler(IDispatcherService dispatcher)
            : base(dispatcher)
        {
        }

        protected override string DataFormat =>
            System.Windows.DataFormats.XamlPackage;
        protected override bool PreserveTextElements => false;

        protected override byte[] PrepareLoadContent(byte[] content)
        {
            if(!IsXamlPackage(content))
                return content;

            using var package = new MemoryStream();
            package.Write(content);
            package.Position = 0;

            using(var archive = new ZipArchive(
                package,
                ZipArchiveMode.Update,
                leaveOpen: true))
            {
                ZipArchiveEntry? documentEntry =
                    archive.GetEntry("Xaml/Document.xaml");
                if(documentEntry is null)
                    return content;

                string xaml;
                using(var reader = new StreamReader(
                    documentEntry.Open(),
                    Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: true))
                {
                    xaml = reader.ReadToEnd();
                }

                if(!xaml.Contains(
                    "ParagraphService",
                    StringComparison.Ordinal))
                {
                    return content;
                }

                string normalized = NormalizeApplicationTextElements(xaml);
                documentEntry.Delete();
                ZipArchiveEntry replacement = archive.CreateEntry(
                    "Xaml/Document.xaml",
                    CompressionLevel.Optimal);
                using var writer = new StreamWriter(
                    replacement.Open(),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                writer.Write(normalized);
            }

            return package.ToArray();
        }

        protected override string ResolveLoadDataFormat(
            ReadOnlySpan<byte> content)
        {
            if(IsXamlPackage(content))
                return System.Windows.DataFormats.XamlPackage;

            string? text = TryDecodeText(content);
            if(text is null)
                return System.Windows.DataFormats.XamlPackage;

            ReadOnlySpan<char> trimmed = text.TrimStart(
                '\uFEFF',
                ' ',
                '\t',
                '\r',
                '\n').AsSpan();

            if(trimmed.StartsWith(
                @"{\rtf",
                StringComparison.OrdinalIgnoreCase))
            {
                return System.Windows.DataFormats.Rtf;
            }

            if(trimmed.StartsWith("<", StringComparison.Ordinal))
                return System.Windows.DataFormats.Xaml;

            return System.Windows.DataFormats.Text;
        }

        private static bool IsXamlPackage(ReadOnlySpan<byte> content) =>
            content.Length >= 4 &&
            content[0] == (byte)'P' &&
            content[1] == (byte)'K' &&
            content[2] is 3 or 5 or 7 &&
            content[3] is 4 or 6 or 8;

        private static string? TryDecodeText(ReadOnlySpan<byte> content)
        {
            try
            {
                string text;
                if(content.Length >= 2 &&
                   content[0] == 0xFF &&
                   content[1] == 0xFE)
                {
                    text = Encoding.Unicode.GetString(content[2..]);
                }
                else if(content.Length >= 2 &&
                        content[0] == 0xFE &&
                        content[1] == 0xFF)
                {
                    text = Encoding.BigEndianUnicode.GetString(content[2..]);
                }
                else
                {
                    text = new UTF8Encoding(
                        encoderShouldEmitUTF8Identifier: false,
                        throwOnInvalidBytes: true).GetString(content);
                }

                foreach(char character in text)
                {
                    if(char.IsControl(character) &&
                       character is not '\t' and not '\r' and not '\n')
                    {
                        return null;
                    }
                }

                return text;
            }
            catch(DecoderFallbackException)
            {
                return null;
            }
        }

        private static string NormalizeApplicationTextElements(string xaml)
        {
            XDocument document = XDocument.Parse(
                xaml,
                LoadOptions.PreserveWhitespace);
            XNamespace application =
                "clr-namespace:CryptoBook.Services;assembly=CryptoBook";
            XNamespace presentation =
                "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

            foreach(XElement element in document
                .Descendants(application + "ParagraphService")
                .ToArray())
            {
                element.Name = presentation + "Paragraph";
            }

            return document.ToString(SaveOptions.DisableFormatting);
        }
    }
}
