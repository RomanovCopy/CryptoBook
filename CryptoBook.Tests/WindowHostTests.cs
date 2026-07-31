using Autofac;

using CryptoBook.DTO;

using System.ComponentModel;
using System.Windows;

using Xunit;

namespace CryptoBook.Tests
{
    public sealed class WindowHostTests
    {
        [WpfFact]
        public void CanceledClose_DoesNotLeaveHostInClosingState()
        {
            using Autofac.IContainer container =
                new ContainerBuilder().Build();
            using ILifetimeScope scope =
                container.BeginLifetimeScope();
            var window = new Window();
            CancelEventHandler cancel = (_, args) =>
                args.Cancel = true;
            window.Closing += cancel;
            using var host =
                new WindowHost(Guid.NewGuid(), scope, window);

            window.Close();

            Assert.False(host.IsClosing);
            Assert.False(host.IsClosed);

            window.Closing -= cancel;
            window.Close();

            Assert.True(host.IsClosing);
            Assert.True(host.IsClosed);
        }
    }
}
