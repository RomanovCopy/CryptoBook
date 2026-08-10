using CryptoBook.DTO;

namespace CryptoBook.Interfaces
{
    public interface IFilePropertiesService: IService
    {
        LaunchResult Show(string path);
    }
}
