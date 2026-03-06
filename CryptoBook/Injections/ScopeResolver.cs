using Autofac;

using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using System.Xaml;

namespace CryptoBook.Injections
{
    public static class ScopeResolver
    {
        public static ILifetimeScope Resolve(IServiceProvider sp)
        {
            var ambient = AmbientScope.TryGetCurrent();
            if(ambient is not null)
            {
                return ambient;
            }

            // 2) TargetObject (важно для шаблонов/ресурсов)
            if(sp.GetService(typeof(IProvideValueTarget)) is IProvideValueTarget pvt &&
                pvt.TargetObject is DependencyObject d)
            {
                // Сначала пробуем scope прямо на объекте
                var s0 = DiScope.GetScope(d);
                if(s0 is not null)
                    return s0;

                // Потом поднимаемся к окну
                var w = d as Window ?? Window.GetWindow(d);
                if(w is not null)
                {
                    var s1 = DiScope.GetScope(w);
                    if(s1 is not null)
                        return s1;
                }
            }

            // 3) RootObjectProvider (иногда полезно)
            if(sp.GetService(typeof(IRootObjectProvider)) is IRootObjectProvider rop &&
                rop.RootObject is DependencyObject root)
            {
                var s2 = DiScope.GetScope(root);
                if(s2 is not null)
                    return s2;
            }

            throw new InvalidOperationException(
                "No ILifetimeScope found. Ensure the Window is created via WindowManager and " +
                "DiScope is set before Show().");
        }
    }
}
