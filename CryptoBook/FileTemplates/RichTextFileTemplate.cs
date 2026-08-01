using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.FileTemplates
{
    public sealed class RichTextFileTemplate:IFileTemplate
    {
        public string Id => "rtf";
        public string DisplayName =>
            CryptoBook.Infrastructure.LocalizationManager.GetString(
                "FileTemplate.RichText");
        public string DefaultExtension => ".rtf";
        public string SuggestedBaseName =>
            CryptoBook.Infrastructure.LocalizationManager.GetString(
                "FileTemplate.NewDocument");
        public Encoding? DefaultEncoding => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true); // UTF-8 BOM
        public Task<byte[]> GetInitialContentAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            string rtf =
                @"{\rtf1\ansi\uc1\deff0 {\fonttbl {\f0 Arial;}} \f0\fs24 " +
                EncodeRtfText(SuggestedBaseName) +
                @" \par }";
            return Task.FromResult(Encoding.ASCII.GetBytes(rtf));
        }

        public IReadOnlyCollection<string> Extensions =>
        [
            ".rtf",
        ];

        private static string EncodeRtfText(string value)
        {
            var result = new StringBuilder(value.Length);
            foreach(char character in value)
            {
                switch(character)
                {
                    case '\\':
                        result.Append(@"\\");
                        break;
                    case '{':
                        result.Append(@"\{");
                        break;
                    case '}':
                        result.Append(@"\}");
                        break;
                    case <= '\u007f':
                        result.Append(character);
                        break;
                    default:
                        result.Append(@"\u")
                            .Append(unchecked((short)character))
                            .Append('?');
                        break;
                }
            }

            return result.ToString();
        }
    }
}
