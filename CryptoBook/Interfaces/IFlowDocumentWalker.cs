using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace CryptoBook.Interfaces
{
    public interface IFlowDocumentWalker: IService
    {
        IEnumerable<TextElement> Traverse( FlowDocument document);

        IEnumerable<T> Find<T>( FlowDocument document) where T : TextElement;

        TextElement? GetParent( TextElement element);

        bool Remove( TextElement element);

        bool InsertBefore( TextElement target, TextElement newElement);

        bool InsertAfter( TextElement target, TextElement newElement);
    }
}
