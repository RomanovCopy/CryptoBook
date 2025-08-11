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
        public ParagraphService()
        {
        }

        public ParagraphService(Inline inline) : base(inline)
        {
        }
    }
}
