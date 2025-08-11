using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace CryptoBook.Interfaces
{
    public interface IParagraphFactory
    {
        Paragraph Create(Inline? inline = null);
    }
}
