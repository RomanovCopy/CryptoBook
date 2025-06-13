using Autofac;

using CryptoBook.Helpers;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Locators
{
    public class Helpers
    {
        public static BehaviorComboBox BehaviorComboBox => ((IContainerProvider)System.Windows.Application.Current).Container.Resolve<BehaviorComboBox>();

    }
}
