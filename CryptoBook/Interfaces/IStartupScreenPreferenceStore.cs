using CryptoBook.DTO;

namespace CryptoBook.Interfaces;

public interface IStartupScreenPreferenceStore: IService
{
    StartupScreen Load();
    void Save(StartupScreen screen);
}
