using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace CryptoBook.Services
{
    public class ParagraphService: Paragraph, IParagraphService
    {
        /// <summary>
        /// Копирование форматирования (поддерживает ваш сценарий «как у предыдущего»)
        /// </summary>
        /// <param name="other"></param>
        /// <param name="copyOnlyLocal"></param>
        public void CopyFormattingFrom(IParagraphService state, bool copyOnlyLocal)
        {

        }

    }
}
