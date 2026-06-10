using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace CryptoBook.Interfaces
{
    /// <summary>
    /// Stateful-сессия на время сборки ОДНОГО документа.
    /// Хранит «текущие настройки» и выдаёт следующие параграфы
    /// с этими настройками (или «как у предыдущего»).
    /// </summary>
    public interface IParagraphSession
    {
        /// <summary>
        /// Изменить текущие настройки набора (влияет на следующие параграфы).
        /// </summary>
        /// <param name="mutate"></param>
        /// <returns></returns>
        IParagraphSession Set(Action<IParagraphService>? mutate);
        /// <summary>
        /// Создать следующий параграф, применив текущие настройки (+необязательные точечные правки).
        /// </summary>
        /// <param name="mutate"></param>
        /// <returns></returns>
        IParagraphService Next(Action<IParagraphService>? mutate = null);
        /// <summary>
        /// Создать следующий параграф «как у предыдущего», копируя только его локальные изменения.
        /// Удобно для кратких серий, когда правили предыдущий параграф и хотите повторить эти правки.
        /// </summary>
        /// <param name="mutate"></param>
        /// <returns></returns>
        IParagraphService NextLikePrevious(Action<IParagraphService>? mutate = null);
    }
}
