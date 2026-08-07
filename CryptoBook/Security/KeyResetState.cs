namespace CryptoBook.Security;

/// <summary>Состояние автоматического сброса ключа.</summary>
public enum KeyResetState
{
    Inactive,
    Active,
    Resetting,
    KeyReset,
    Unlocking,
    Restoring
}
