using CryptoBook.Interfaces;

using System.Globalization;

using Drawing = System.Drawing;

namespace CryptoBook.Services
{
    public sealed class UserDocumentBackgroundPreferenceStore:
        IDocumentBackgroundPreferenceStore
    {
        public Drawing.Color? Load()
        {
            string stored =
                Properties.Settings.Default.DocumentBackgroundColor;
            if(string.IsNullOrWhiteSpace(stored))
                return null;

            ReadOnlySpan<char> value = stored.AsSpan().Trim();
            if(value.StartsWith("#", StringComparison.Ordinal))
                value = value[1..];

            if(value.Length != 8 ||
               !uint.TryParse(
                   value,
                   NumberStyles.HexNumber,
                   CultureInfo.InvariantCulture,
                   out uint argb))
            {
                return null;
            }

            return Drawing.Color.FromArgb(unchecked((int)argb));
        }

        public void Save(Drawing.Color color)
        {
            Properties.Settings.Default.DocumentBackgroundColor =
                $"#{unchecked((uint)color.ToArgb()):X8}";
            Properties.Settings.Default.Save();
        }
    }
}
