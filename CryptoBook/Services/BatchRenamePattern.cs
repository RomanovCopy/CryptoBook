using CryptoBook.DTO;
using CryptoBook.Infrastructure;

using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CryptoBook.Services;

public static class BatchRenamePattern
{
    public static string BuildName(string fileName, int index, BatchRenameOptions options,
        DateTime? currentTime = null)
    {
        if(options.CounterStart < 0 || options.CounterStep < 1 ||
           options.CounterDigits is < 1 or > 18)
            throw new ArgumentException(Text("CounterInvalid"));

        string name = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName).TrimStart('.');
        string counter = checked(options.CounterStart + index * options.CounterStep)
            .ToString("D" + options.CounterDigits, CultureInfo.InvariantCulture);
        DateTime timestamp = currentTime ?? DateTime.Now;
        string newName = Expand(options.NameMask, name, extension, counter, timestamp);
        string newExtension = Expand(options.ExtensionMask, name, extension, counter, timestamp);
        string result = newName + (newExtension.Length == 0 ? string.Empty : "." + newExtension);
        if(options.Search.Length > 0)
        {
            if(options.UseRegex)
                result = Regex.Replace(result, options.Search, options.Replacement,
                    options.MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase,
                    TimeSpan.FromMilliseconds(100));
            else
                result = result.Replace(options.Search, options.Replacement,
                    options.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
        }
        return options.Case switch
        {
            RenameCase.Lower => result.ToLower(CultureInfo.CurrentCulture),
            RenameCase.Upper => result.ToUpper(CultureInfo.CurrentCulture),
            RenameCase.Title => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(
                result.ToLower(CultureInfo.CurrentCulture)),
            _ => result
        };
    }

    private static string Expand(string mask, string name, string extension, string counter, DateTime timestamp)
    {
        var result = new StringBuilder();
        for(int i = 0; i < mask.Length; i++)
        {
            if(mask[i] != '[')
            {
                result.Append(mask[i]);
                continue;
            }
            int end = mask.IndexOf(']', i + 1);
            if(end < 0)
                throw new ArgumentException(Text("MaskInvalid"));
            string token = mask[(i + 1)..end];
            if(token == "C")
                result.Append(counter);
            else if(token == "DMY")
                result.Append(timestamp.ToString("ddMMyyyy", CultureInfo.InvariantCulture));
            else if(token == "HMS")
                result.Append(timestamp.ToString("HHmmss", CultureInfo.InvariantCulture));
            else if(token is "N" or "E")
                result.Append(token == "N" ? name : extension);
            else if(token.Length > 1 && token[0] is 'N' or 'E')
                result.Append(Slice(token[0] == 'N' ? name : extension, token[1..]));
            else
                throw new ArgumentException(Text("MaskInvalid"));
            i = end;
        }
        return result.ToString();
    }

    private static string Slice(string value, string range)
    {
        Match match = Regex.Match(range, @"^(\d+)(?:-(\d*))?$", RegexOptions.CultureInvariant);
        if(!match.Success || !int.TryParse(match.Groups[1].Value, out int start) || start < 1)
            throw new ArgumentException(Text("MaskInvalid"));
        int end = start;
        if(range.Contains('-'))
        {
            if(match.Groups[2].Value.Length == 0)
                end = int.MaxValue;
            else if(!int.TryParse(match.Groups[2].Value, out end) || end < start)
                throw new ArgumentException(Text("MaskInvalid"));
        }
        // Text elements keep surrogate pairs and combining marks together.
        int[] positions = StringInfo.ParseCombiningCharacters(value);
        if(start > positions.Length)
            return string.Empty;
        int first = positions[start - 1];
        int last = end >= positions.Length ? value.Length : positions[end];
        return value[first..last];
    }

    public static string? ValidateName(string name)
    {
        if(string.IsNullOrWhiteSpace(name) || name.Length > 255 || name is "." or ".." ||
           name.EndsWith('.') || name.EndsWith(' ') ||
           name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return Text("NameInvalid");
        string stem = name.Split('.')[0].TrimEnd(' ');
        if(Regex.IsMatch(stem, @"^(CON|PRN|AUX|NUL|COM[1-9¹²³]|LPT[1-9¹²³])$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return Text("NameInvalid");
        return null;
    }

    internal static string Text(string key) => LocalizationManager.GetString("BatchRename." + key);
}
