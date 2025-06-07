using Autofac;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Markup;

namespace CryptoBook.Markup
{
    public class ResolveConverterExtension: MarkupExtension
    {
        private static readonly ConcurrentDictionary<(Type, string), IValueConverter> converterCashe = new();

        public Type ConverterType { get; set; }
        public string Parametr { get; set; }

        public ResolveConverterExtension(ILifetimeScope scope)
        {
        }

        public override IValueConverter ProvideValue(IServiceProvider serviceProvider)
        {
            throw new NotImplementedException();
        }
    }
}
