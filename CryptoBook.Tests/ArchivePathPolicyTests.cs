using CryptoBook.Services;

using Xunit;

namespace CryptoBook.Tests;

public sealed class ArchivePathPolicyTests
{
    [Theory]
    [InlineData(@"C:\", @"C:\Папка", true)]
    [InlineData(@"D:\", @"D:\Папка\файл.txt", true)]
    [InlineData(@"D:\", @"D:\", true)]
    [InlineData(@"D:\", @"C:\Папка", false)]
    [InlineData(@"D:\Папка", @"D:\Папка\файл.txt", true)]
    [InlineData(@"D:\Папка\", @"D:\Папка", true)]
    [InlineData(@"D:\Папка", @"D:\Папка-другая\файл.txt", false)]
    [InlineData(@"D:\Папка", @"E:\Папка\файл.txt", false)]
    public void IsWithin_RespectsDriveRootAndDirectoryBoundary(string parent, string candidate, bool expected)
    {
        Assert.Equal(expected, ArchivePathPolicy.IsWithin(parent, candidate));
    }
}
