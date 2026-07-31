using CryptoBook.Services;

using System.IO;

using Xunit;

namespace CryptoBook.Tests
{
    public sealed class DirectoryMonitoringServiceTests
    {
        [Fact]
        public void DuplicateRegistration_RequiresMatchingStops()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "CryptoBook.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);

            try
            {
                using var service = new DirectoryMonitoringService();

                Assert.True(service.StartMonitoring(directory));
                Assert.True(service.StartMonitoring(directory));

                Assert.True(service.StopMonitoring(directory));
                Assert.True(service.StopMonitoring(directory));
                Assert.False(service.StopMonitoring(directory));
            }
            finally
            {
                if(Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
        }
    }
}
