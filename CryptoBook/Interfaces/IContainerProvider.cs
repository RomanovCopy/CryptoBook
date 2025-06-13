using Autofac;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    internal interface IContainerProvider
    {
        IContainer Container { get;}
    }
}
