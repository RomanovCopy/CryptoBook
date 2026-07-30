using Autofac;

using CryptoBook.Injections;
using CryptoBook.Interfaces;

using System.Windows.Markup;

namespace CryptoBook.Markup
{
    /// <summary>
    /// MarkupExtension для разрешения UserControl через DI-контейнер Autofac с кэшированием.
    /// </summary>
    public class ResolveUserControlExtension: DiMarkupExtension
    {
        /// <summary>
        /// Тип UserControl, который требуется разрешить через DI.
        /// </summary>
        public Type UserControlType { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if(UserControlType == null)
                throw new InvalidOperationException("UserControlType must be set.");

            var scope = GetScope(serviceProvider);
            return scope.Resolve(UserControlType);
        }

        //private static IContainer GetContainer()
        //{
        //    if(System.Windows.Application.Current is not IContainerProvider containerProvider ||
        //        containerProvider.Container is not IContainer container)
        //    {
        //        throw new InvalidOperationException("Autofac container not found in Application.Current. " +
        //            "Ensure your Application implements IContainerProvider and Container is properly initialized.");
        //    }
        //    return container;
        //}
    }
}
