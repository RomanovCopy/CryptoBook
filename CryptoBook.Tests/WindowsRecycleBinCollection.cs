using Xunit;

namespace CryptoBook.Tests;

// Shell.Application и его COM-обёртки разделяются между проверками восстановления.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class WindowsRecycleBinCollection
{
    public const string Name = "Windows Recycle Bin";
}
