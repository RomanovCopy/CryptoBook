using Autofac;

using CryptoBook.Infrastructure;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Models
{
    internal class TitleBarRB_Model:ViewModelBase
    {
        private readonly ILifetimeScope scope;
        public TitleBarRB_Model(ILifetimeScope _scope)
        {
            scope = _scope;
        }
    }
}
