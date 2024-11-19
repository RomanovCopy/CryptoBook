using Autofac;

using CryptoBook.Helpers;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Locators
{
    public class Helpers
    {
        public static BehaviorComboBox BehaviorComboBox => App.Container.Resolve<BehaviorComboBox>();

    }
}
