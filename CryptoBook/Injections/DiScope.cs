using Autofac;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CryptoBook.Injections
{
    public static class DiScope
    {
        public static readonly DependencyProperty ScopeProperty =
            DependencyProperty.RegisterAttached(
                "Scope",
                typeof(ILifetimeScope),
                typeof(DiScope),
                new PropertyMetadata(null));

        public static void SetScope(DependencyObject obj, ILifetimeScope? value)
            => obj.SetValue(ScopeProperty, value);

        public static ILifetimeScope? GetScope(DependencyObject obj)
            => (ILifetimeScope?)obj.GetValue(ScopeProperty);
    }
}
