using Autofac;

using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace CryptoBook.Composition
{
    // <summary>
    /// Фабрика создаёт Paragraph подтягивая зависимости через Autofac.
    /// </summary>
    public sealed class ParagraphFactory: IParagraphFactory
    {
        private readonly ILifetimeScope scope;
        public ParagraphFactory(ILifetimeScope ctx) => scope = ctx;

        public IParagraphService Create(Inline? inline = null)
        {
            return inline is null
                ? scope.Resolve<IParagraphService>() 
                : scope.Resolve<IParagraphService>(new TypedParameter(typeof(Inline), inline));
        }
    }
}
