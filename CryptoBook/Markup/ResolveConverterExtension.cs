using Autofac;

using CryptoBook.Interfaces;

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
        private static readonly ConcurrentDictionary<Type, IValueConverter> converterCashe = new();

        public Type ConverterType { get; set; }

        public override IValueConverter ProvideValue(IServiceProvider serviceProvider)
        {
            //ConverterType равен null или не реализует интерфейс IValueConverter
            if(ConverterType == null || !typeof(IValueConverter).IsAssignableFrom(ConverterType))
            {
                throw new InvalidOperationException("ConverterType must be specified and implement IValueConverter.");
            }
            
            var key = ConverterType;//ключ для поиска экземпляра в кэш (converterCashe)

            //экземпляр найден в кэш
            if(converterCashe.TryGetValue(key, out var cashedConverter))
            {
                return cashedConverter;
            }
            //экземпляр не найден в кэш 

            //получаем котейнер , а при неудаче вызываем исключение
            IContainer container = ((IContainerProvider)App.Current).Container ?? throw new InvalidOperationException("Autofac container not found.");

            //получаем конвертер с учетом параметра
            var converter = (IValueConverter)container.Resolve(ConverterType);

            //добавляем в кэш
            converterCashe.TryAdd(key, converter);

            return converter;

        }
    }
}
