using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace CryptoBook.Converters;

public sealed class RelativeDirectoryConverter: IMultiValueConverter
{
    public object Convert(
        object[] values,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        if(values.Length < 2 ||
           values[0] is not string filePath ||
           values[1] is not string rootPath ||
           string.IsNullOrWhiteSpace(filePath) ||
           string.IsNullOrWhiteSpace(rootPath))
        {
            return string.Empty;
        }

        try
        {
            string? directory = Path.GetDirectoryName(filePath);
            if(string.IsNullOrWhiteSpace(directory))
                return string.Empty;

            string relative = Path.GetRelativePath(rootPath, directory);
            return relative == "." ? string.Empty : relative;
        }
        catch(Exception exception) when(
            exception is ArgumentException or NotSupportedException)
        {
            return string.Empty;
        }
    }

    public object[] ConvertBack(
        object value,
        Type[] targetTypes,
        object parameter,
        CultureInfo culture) =>
        throw new NotSupportedException();
}
