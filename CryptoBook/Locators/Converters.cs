using Autofac;
using Autofac.Core;

using CryptoBook.Converters;
using CryptoBook.Infrastructure;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Locators
{
    public class Converters
    {

        public static BitmapConverter bitmapConverter => App.Container.Resolve<BitmapConverter>();
        public static ColorToColorConverter colorToColorConverter => App.Container.Resolve<ColorToColorConverter>();
        public static ColumnsWidthConverter columnsWidthConverter => App.Container.Resolve<ColumnsWidthConverter>();
        public static EncodingConverter encodingConverter => App.Container.Resolve<EncodingConverter>();
        public static FlowDocumentToXamlConverter flowDocumentToXamlConverter => App.Container.Resolve<FlowDocumentToXamlConverter>();
        public static SizeLocationConverter sizeLocationConverter => App.Container.Resolve<SizeLocationConverter>();
        public static FontSizeAdjustConverter fontSizeAdjustConverter => App.Container.Resolve<FontSizeAdjustConverter>();
        public static MediBrushSerializeConverter mediBrushSerializeConverter => App.Container.Resolve<MediBrushSerializeConverter>();
        public static VisibilityConverter visibilityConverter => App.Container.Resolve<VisibilityConverter>();
        public static InternalSizeConverter internalSizeConverter => App.Container.Resolve<InternalSizeConverter>();


    }
}
