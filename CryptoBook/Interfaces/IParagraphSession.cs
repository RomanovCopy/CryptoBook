using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace CryptoBook.Interfaces
{
    public interface IParagraphSession
    {
        // меняем «текущие» настройки набора (влияют на последующие абзацы)
        IParagraphSession Set(Action<Paragraph> mutate);

        // создаём абзац с текущими настройками (+ опциональные точечные правки)
        Paragraph Next(Action<Paragraph>? mutate = null);
    }
}
