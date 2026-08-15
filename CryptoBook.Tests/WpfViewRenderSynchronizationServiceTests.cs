using CryptoBook.Services;

using System.Windows.Controls;
using System.Windows.Threading;

using Xunit;

namespace CryptoBook.Tests;

public sealed class WpfViewRenderSynchronizationServiceTests
{
    [WpfFact]
    public async Task WaitForRenderAsync_DrainsBindingAndRenderWork()
    {
        var view = new Border();
        var order = new List<string>();
        Dispatcher dispatcher = view.Dispatcher;

        _ = dispatcher.BeginInvoke(
            () => order.Add("binding"),
            DispatcherPriority.DataBind);
        _ = dispatcher.BeginInvoke(
            () => order.Add("render"),
            DispatcherPriority.Render);

        var service = new WpfViewRenderSynchronizationService();
        await service.WaitForRenderAsync(view);
        order.Add("completed");

        Assert.Equal(["binding", "render", "completed"], order);
    }
}
