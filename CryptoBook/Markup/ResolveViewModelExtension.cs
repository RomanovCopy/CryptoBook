using Autofac;

using CryptoBook.Interfaces;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;

namespace CryptoBook.Markup
{
    public class ResolveViewModelExtension: MarkupExtension
    {


        public Type ViewModelType { get; set; }


        private readonly ILifetimeScope scope;




        public ResolveViewModelExtension()
        {
        }


        public ResolveViewModelExtension(Type viewModelType)
        {
            ViewModelType = viewModelType ?? throw new InvalidOperationException("Autofac container not found.");
        }


        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            var container = ((IContainerProvider)App.Current).Container ?? throw new InvalidOperationException("Autofac container not found.");

            if(ViewModelType != null)
            {
                return container.Resolve(ViewModelType);
            }
            throw new InvalidOperationException("ViewModelType must be specified.");
        }

    }
}
