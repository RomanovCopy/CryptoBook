using Autofac;

using CryptoBook.Injections;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace CryptoBook.Converters
{
    public sealed class PageKeyToPageConverter: IValueConverter
    {
        private readonly IServiceProvider _sp;

        public PageKeyToPageConverter(IServiceProvider sp)
            => _sp = sp;

        public object? Convert(object value, Type targetType,
                               object parameter, CultureInfo culture)
        {
            if(value is not string key || string.IsNullOrWhiteSpace(key))
                return null;

            var scope = ScopeResolver.Resolve(_sp);

            var registry = scope.Resolve<IPageRegistry>();
            var pageType = registry.Resolve(key);

            using(AmbientScope.Push(scope))
                return scope.Resolve(pageType);
        }

        public object? ConvertBack(object value, Type targetType,
                                   object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
