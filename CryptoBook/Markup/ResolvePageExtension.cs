using Autofac;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Markup;

using CryptoBook.Interfaces;
using CryptoBook.Injections;
using System.Windows.Data;
using CryptoBook.Converters;

namespace CryptoBook.Markup
{
    /// <summary>
    /// MarkupExtension для разрешения Page через DI-контейнер Autofac с кэшированием.
    /// </summary>
    public class ResolvePageExtension:DiMarkupExtension
    {

        public BindingBase? PageKey { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if(PageKey is null)
                throw new InvalidOperationException("PageKey binding is null.");

            var binding = new System.Windows.Data.Binding
            {
                Path = ((System.Windows.Data.Binding)PageKey).Path,
                Mode = BindingMode.OneWay,
                Converter = new PageKeyToPageConverter(serviceProvider)
            };

            return binding.ProvideValue(serviceProvider);
        }
    }
}
