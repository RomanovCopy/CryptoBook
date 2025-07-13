using Autofac;

using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.ViewModels
{
    public class TitleBarRB_ViewModel: ITitleBarRB_ViewModel
    {
        private readonly ILifetimeScope scope;

        public TitleBarRB_ViewModel(ILifetimeScope scope)
        {
            this.scope = scope;
        }
    }
}
