using CryptoBook.Services;

using System.Globalization;
using System.Windows.Data;

namespace CryptoBook.Converters
{
    public sealed class SystemItemSelectionSnapshotConverter: IValueConverter
    {
        public object Convert(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture) =>
            FileExplorerSelectionPolicy.CreateSnapshot(value);

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture) =>
            System.Windows.Data.Binding.DoNothing;
    }
}
