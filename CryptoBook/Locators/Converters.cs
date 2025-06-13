using Autofac;
using Autofac.Core;

using CryptoBook.Converters;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Windows.Media.Capture.Core;

namespace CryptoBook.Locators
{
    public class Converters
    {

        public static BitmapConverter bitmapConverter => ((IContainerProvider)System.Windows.Application.Current).Container.Resolve<BitmapConverter>();
        public static ColorToColorConverter colorToColorConverter => ((IContainerProvider)System.Windows.Application.Current).Container.Resolve<ColorToColorConverter>();
        public static ColumnsWidthConverter columnsWidthConverter => ((IContainerProvider)System.Windows.Application.Current).Container.Resolve<ColumnsWidthConverter>();
        public static EncodingConverter encodingConverter => ((IContainerProvider)System.Windows.Application.Current).Container.Resolve<EncodingConverter>();
        public static FlowDocumentToXamlConverter flowDocumentToXamlConverter => ((IContainerProvider)System.Windows.Application.Current).Container.Resolve<FlowDocumentToXamlConverter>();
        public static SizeLocationConverter sizeLocationConverter => ((IContainerProvider)System.Windows.Application.Current).Container.Resolve<SizeLocationConverter>();
        public static FontSizeAdjustConverter fontSizeAdjustConverter => ((IContainerProvider)System.Windows.Application.Current).Container.Resolve<FontSizeAdjustConverter>();
        public static MediBrushSerializeConverter mediBrushSerializeConverter => ((IContainerProvider)System.Windows.Application.Current).Container.Resolve<MediBrushSerializeConverter>();
        public static VisibilityConverter visibilityConverter => ((IContainerProvider)System.Windows.Application.Current).Container.Resolve<VisibilityConverter>();
        public static InternalSizeConverter internalSizeConverter => ((IContainerProvider)System.Windows.Application.Current).Container.Resolve<InternalSizeConverter>();


    }
}
