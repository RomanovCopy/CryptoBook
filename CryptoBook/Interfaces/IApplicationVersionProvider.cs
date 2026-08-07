using CryptoBook.DTO;

namespace CryptoBook.Interfaces
{
    public interface IApplicationVersionProvider: IService
    {
        SemanticVersion GetCurrentVersion();
    }
}
