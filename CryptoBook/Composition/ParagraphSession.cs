using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace CryptoBook.Composition
{
    public sealed class ParagraphSession:IParagraphSession
    {
        public IParagraphSession Set(Action<Paragraph> mutate)
        {
            throw new NotImplementedException();
        }

        public Paragraph Next(Action<Paragraph>? mutate = null)
        {
            throw new NotImplementedException();
        }
    }
}
