using CryptoBook.Converters;
using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System.Globalization;
using System.Windows.Media;

using Xunit;

namespace CryptoBook.Tests;

public sealed class SystemItemToIconConverterTests
{
    [StaFact]
    public void RemoteAndroidRoot_UsesCellPhoneStockIcon()
    {
        var icons = new SystemIconsStub();
        var stockIcons = new StockIconsStub();
        var converter = new SystemItemToIconConverter(icons, stockIcons);
        var drive = new DriveItem(null!, null!, null!, null!)
        {
            FullPath = "mtp://opaque-locator"
        };

        object result = converter.Convert(
            drive,
            typeof(ImageSource),
            "Large",
            CultureInfo.InvariantCulture);

        Assert.Same(stockIcons.Icon, result);
        Assert.Equal(SHSTOCKICONID.SIID_DEVICECELLPHONE, stockIcons.LastId);
        Assert.False(stockIcons.LastSmall);
        Assert.Null(icons.LastPath);
    }

    [StaFact]
    public void RemoteDirectory_UsesFolderIcon()
    {
        var icons = new SystemIconsStub();
        var converter = new SystemItemToIconConverter(icons, new StockIconsStub());
        var directory = new DirectoryItem(null!, null!, null!, null!)
        {
            FullPath = "mtp://opaque-locator"
        };

        object result = converter.Convert(
            directory,
            typeof(ImageSource),
            null!,
            CultureInfo.InvariantCulture);

        Assert.Same(icons.FolderIcon, result);
        Assert.Equal(SystemIconSize.Small, icons.LastSize);
    }

    [StaFact]
    public void RemoteFile_UsesMetadataExtension_NotOpaqueLocator()
    {
        var icons = new SystemIconsStub();
        var converter = new SystemItemToIconConverter(icons, new StockIconsStub());
        var file = new FileItem
        {
            FullPath = "mtp://opaque-locator.with-fake-extension",
            Extension = ".jpg"
        };

        object result = converter.Convert(
            file,
            typeof(ImageSource),
            null!,
            CultureInfo.InvariantCulture);

        Assert.Same(icons.ExtensionIcon, result);
        Assert.Equal(".jpg", icons.LastExtension);
    }

    private sealed class SystemIconsStub: ISystemIconService
    {
        public ImageSource FolderIcon { get; } = new DrawingImage();
        public ImageSource ExtensionIcon { get; } = new DrawingImage();
        public ImageSource PathIcon { get; } = new DrawingImage();
        public string? LastPath { get; private set; }
        public string? LastExtension { get; private set; }
        public SystemIconSize LastSize { get; private set; }

        public ImageSource GetFolderIcon(
            SystemIconSize size = SystemIconSize.Small,
            bool open = false)
        {
            LastSize = size;
            return FolderIcon;
        }

        public ImageSource GetIconForPath(
            string path,
            SystemIconSize size = SystemIconSize.Small)
        {
            LastPath = path;
            LastSize = size;
            return PathIcon;
        }

        public ImageSource GetIconForExtension(
            string extension,
            SystemIconSize size = SystemIconSize.Small)
        {
            LastExtension = extension;
            LastSize = size;
            return ExtensionIcon;
        }
    }

    private sealed class StockIconsStub: IStockIconService
    {
        public ImageSource Icon { get; } = new DrawingImage();
        public SHSTOCKICONID? LastId { get; private set; }
        public bool LastSmall { get; private set; }

        public ImageSource GetStockIcon(SHSTOCKICONID id, bool small = true)
        {
            LastId = id;
            LastSmall = small;
            return Icon;
        }

        public void ClearCache()
        {
        }
    }
}
