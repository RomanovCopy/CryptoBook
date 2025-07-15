using Autofac;

using CryptoBook.Infrastructure;

namespace CryptoBook.Models
{
    internal class TitleBarRB_Model: ViewModelBase
    {
        private readonly ILifetimeScope scope;
        public TitleBarRB_Model(ILifetimeScope _scope)
        {
            scope = _scope;
        }
    }
}
