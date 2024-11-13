using Autofac;
using Autofac.Core;

using CryptoBook.Converters;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Locators
{
    public class Converters
    {

        public static BitmapConverter? bitmapConverter => App.Container?.Resolve<BitmapConverter>();

        public static ColorToColorConverter? colorToColorConverter => App.Container?.Resolve<ColorToColorConverter>();

        public static ColumnsWidthConverter? columnsWidthConverter => App.Container?.Resolve<ColumnsWidthConverter>();

    }
}
