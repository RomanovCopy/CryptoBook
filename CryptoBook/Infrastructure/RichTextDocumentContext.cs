using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace CryptoBook.Infrastructure
{
    public class RichTextDocumentContext: IRichTextDocumentContext
    {
        private static bool _instanceCreated = false;

        public RichTextDocumentContext()
        {
            if(_instanceCreated)
                throw new InvalidOperationException("RichTextDocumentContext is designed to be a singleton.");
            _instanceCreated = true;
        }

        public FlowDocument Document => new FlowDocument();
    }
}
