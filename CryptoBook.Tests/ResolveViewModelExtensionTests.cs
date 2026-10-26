using Autofac;
using CryptoBook.Infrastructure;
using CryptoBook.Injections;
using CryptoBook.Markup;
using Xunit;

namespace CryptoBook.Tests;

public sealed class ResolveViewModelExtensionTests
{
    [Fact]
    public void Resolve_UsesRegisteredInstanceWithoutRequiringWindowLifecycleCommands()
    {
        var viewModel = new ViewModelBase();
        var builder = new ContainerBuilder();
        builder.RegisterInstance(viewModel);
        using var container = builder.Build();
        using var scope = AmbientScope.Push(container);
        var extension = new ResolveViewModelExtension { ViewModelType = typeof(ViewModelBase) };

        Assert.Same(viewModel, extension.ProvideValue(new EmptyServiceProvider()));
        Assert.Same(viewModel, extension.ProvideValue(new EmptyServiceProvider()));
    }

    [Fact]
    public void Resolve_RejectsTypesWithoutPropertyNotifications()
    {
        var extension = new ResolveViewModelExtension { ViewModelType = typeof(object) };
        Assert.Throws<InvalidOperationException>(() => extension.ProvideValue(new EmptyServiceProvider()));
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
