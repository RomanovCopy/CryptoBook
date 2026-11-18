using CryptoBook.Interfaces;

using System.Collections;
using System.Globalization;
using System.Windows.Data;

namespace CryptoBook.Converters;

/// <summary>Keeps window-owned archive folders in the tree without duplicating files in the main list.</summary>
public sealed class FileExplorerTreeChildrenConverter: IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if(values.Length < 3 || values[0] is not IEnumerable directories)
            return Array.Empty<IContainerSystemItem>();
        if(values[1] is not IContainerSystemItem parent ||
           values[2] is not IEnumerable<IContainerSystemItem> archives) return directories;
        IContainerSystemItem[] children = archives.Where(directory => ReferenceEquals(directory.Parent, parent)).ToArray();
        if(children.Length == 0) return directories;
        // CollectionContainer forwards physical folder changes without taking a stale snapshot.
        return new CompositeCollection
        {
            new CollectionContainer { Collection = directories },
            new CollectionContainer { Collection = children }
        };
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
