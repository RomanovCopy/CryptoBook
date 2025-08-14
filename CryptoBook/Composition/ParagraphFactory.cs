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
            var paragraph= scope.Resolve<IParagraphService>();
            paragraph.TextIndent = 20; // Устанавливаем отступ для нового параграфа
            if (inline != null)
            {
                // Если передан Inline, то обернём его в Paragraph
                paragraph.Inlines.Add(inline);
            }
            return paragraph;
        }
    }
}
