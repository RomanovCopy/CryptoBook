using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace CryptoBook.Composition
{
    /// <summary>
    /// Держит «эталон» текущего форматирования (_state) и опционально «предыдущий» абзац (_prev).
    /// </summary>
    public class ParagraphSession: IParagraphSession
    {
        private readonly Interfaces.IParagraphFactory _factory;
        private readonly Interfaces.IParagraphService _state;   // эталон текущих настроек
        private Interfaces.IParagraphService? _prev;            // последний выданный абзац (для NextLikePrevious)

        public ParagraphSession(IParagraphFactory factory)
        {
            _factory = factory;
            _state = factory.Create(); // унаследует базу от FlowDocument, когда будет вставлен — это ок
        }

        public IParagraphSession Set(Action<IParagraphService>? mutate)
        {
                mutate?.Invoke(_state);
            return this;
        }

        public IParagraphService Next(Action<IParagraphService>? mutate = null)
        {
            var p = _factory.Create();
            ((IParagraphService)p).CopyFormattingFrom(_state, copyOnlyLocal: false); // применяем ТЕКУЩЕЕ состояние полностью
            mutate?.Invoke(p);
            _prev = p;
            return p;
        }

        public IParagraphService NextLikePrevious(Action<IParagraphService>? mutate = null)
        {
            var p = _factory.Create();
            if(_prev != null)
                p.CopyFormattingFrom(_prev, copyOnlyLocal: true); // копируем только локально изменённые у _prev
            else
                p.CopyFormattingFrom(_state, copyOnlyLocal: false);
            mutate?.Invoke(p);
            _prev = p;
            return p;
        }


    }
}
