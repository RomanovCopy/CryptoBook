using Autofac;

using CryptoBook.Infrastructure;

namespace CryptoBook.Models
{
    public class HomeModel: ViewModelBase
    {

        private readonly ILifetimeScope scope;

        public HomeModel(ILifetimeScope scope)
        {
            this.scope = scope;

        }


        internal bool CanExecute_PageLoded(object? obj)
        {
            return true;
        }
        internal void Execute_PageLoaded(object? obj)
        {
        }


        internal bool CanExecute_PageClear(object? obj)
        {
            return true;
        }
        internal void Execute_PageClear(object? obj)
        {
        }



        internal bool CanExecute_PageClose(object? obj)
        {
            return true;
        }
        internal void Execute_PageClose(object? obj)
        {
        }

    }
}
