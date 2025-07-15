using Autofac;

using CryptoBook.Infrastructure;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Models
{
    internal class MyRichtextboxModel: ViewModelBase
    {
        private readonly ILifetimeScope scope;

        public MyRichtextboxModel(ILifetimeScope _scope)
        {
            scope = _scope;
        }
    }
}
