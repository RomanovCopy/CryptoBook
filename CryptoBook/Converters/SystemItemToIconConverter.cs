using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System.Globalization;
using System.Windows.Data;

namespace CryptoBook.Converters;

/// <summary>
/// Selects an icon from storage metadata without interpreting an opaque remote
/// locator as a native Windows path.
/// </summary>
public sealed class SystemItemToIconConverter: IValueConverter
{
    private readonly ISystemIconService _icons;
    private readonly IStockIconService _stockIcons;

    public SystemItemToIconConverter(
        ISystemIconService icons,
        IStockIconService stockIcons)
    {
        _icons = icons ?? throw new ArgumentNullException(nameof(icons));
        _stockIcons = stockIcons ?? throw new ArgumentNullException(nameof(stockIcons));
    }

    public SystemIconSize Size { get; set; } = SystemIconSize.Small;

    public object Convert(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        SystemIconSize size = ParseSize(parameter) ?? Size;
        bool small = size == SystemIconSize.Small;

        if(value is IDriveItem drive)
        {
            if(StorageLocation.TryParse(drive.FullPath, out StorageLocation location) &&
               !location.IsLocal)
            {
                SHSTOCKICONID stockIcon = location.ProviderId is "android" or "mtp"
                    ? SHSTOCKICONID.SIID_DEVICECELLPHONE
                    : SHSTOCKICONID.SIID_DRIVEREMOVE;
                return _stockIcons.GetStockIcon(stockIcon, small);
            }
            return _icons.GetIconForPath(drive.FullPath, size);
        }

        if(value is IDirectoryItem)
            return _icons.GetFolderIcon(size);

        if(value is IFileItem file)
            return _icons.GetIconForExtension(file.Extension, size);

        if(value is ISystemItem item)
            return _icons.GetIconForPath(item.FullPath, size);

        return _icons.GetIconForExtension(".bin", size);
    }

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture) => System.Windows.Data.Binding.DoNothing;

    private static SystemIconSize? ParseSize(object parameter)
    {
        if(parameter is SystemIconSize size)
            return size;
        return parameter is string text && Enum.TryParse(
            text,
            ignoreCase: true,
            out SystemIconSize parsed)
                ? parsed
                : null;
    }
}
