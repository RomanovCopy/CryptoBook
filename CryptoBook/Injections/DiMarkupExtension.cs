using Autofac;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Markup;

namespace CryptoBook.Injections
{
    public abstract class DiMarkupExtension: MarkupExtension
    {
        protected ILifetimeScope GetScope(IServiceProvider sp)
            => ScopeResolver.Resolve(sp);
    }
}
