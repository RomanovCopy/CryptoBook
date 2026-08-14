using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;

using CryptoBook.DTO;

namespace CryptoBook.Interfaces
{
    public interface IFlowDocumentContentService: IService
    {
        Paragraph CreateParagraph(string text = "");

        bool CanInsertParagraph(
            FrameworkContentElement target,
            DocumentStructureDropPosition position);

        Paragraph InsertParagraph(
            FrameworkContentElement target,
            DocumentStructureDropPosition position,
            string text = "");

        Paragraph AddParagraph( FlowDocument document, string text = "");

        Paragraph AddParagraphAfter( TextElement target, string text = "");

        Paragraph AddParagraphBefore( TextElement target, string text = "");

        Run AddRun( Paragraph paragraph, string text);

        Span AddSpan( Paragraph paragraph, string text = "");

        InlineUIContainer AddInlineObject( Paragraph paragraph, UIElement element);

        BlockUIContainer AddBlockObject( FlowDocument document, UIElement element);

        Section AddSection( FlowDocument document);

        List AddList( FlowDocument document);

        ListItem AddListItem( List list, string text = "");

        Table AddTable( FlowDocument document, int rows, int columns);

        bool Remove( TextElement element);

        void Clear( FlowDocument document);
    }
}
