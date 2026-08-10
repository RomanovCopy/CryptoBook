using System.Windows.Documents;

namespace CryptoBook.Interfaces
{
    public interface IDocumentLineSpacingService: IService
    {
        double Normalize(double ratio);
        double Adjust(double ratio, int direction);
        void Apply(FlowDocument document, double ratio);
        void Apply(Paragraph paragraph, double ratio);
    }
}
