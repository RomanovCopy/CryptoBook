using Autofac;

namespace CryptoBook.Interfaces
{
    internal interface IContainerProvider
    {
        IContainer Container { get; }
    }
}
