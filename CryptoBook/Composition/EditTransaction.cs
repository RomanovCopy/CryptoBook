using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Composition
{
    public class EditTransaction:IEditTransaction
    {
        private readonly IRichTextBoxService _rtb;
        public EditTransaction(IRichTextBoxService rtb) => _rtb = rtb?? throw new ArgumentNullException(nameof(rtb));

        public IDisposable Begin()
        {
            _rtb.BeginChange();
            return new Scope(_rtb);
        }

        private sealed class Scope: IDisposable
        {
            private readonly IRichTextBoxService _rtb;
            private bool _disposed;
            public Scope(IRichTextBoxService rtb) => _rtb = rtb;
            public void Dispose()
            {
                if(_disposed)
                    return;
                _disposed = true;
                _rtb.EndChange();
            }
        }
    }
}
