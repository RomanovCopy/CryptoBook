using Autofac;

using CryptoBook.Interfaces;
using CryptoBook.Services;

using System.Windows.Controls;

using Xunit;

namespace CryptoBook.Tests
{
    public sealed class PageNavigationServiceTests
    {
        [StaFact]
        public void Navigate_ToExistingKey_ReactivatesPageWithoutDuplicate()
        {
            var builder = new ContainerBuilder();
            builder.RegisterType<TestPage>();
            builder.RegisterInstance(new Registry()).As<IPageRegistry>();
            using IContainer container = builder.Build();
            using var service = new PageNavigationService(container);

            service.Navigate("First");
            Page firstPage = Assert.IsType<TestPage>(service.CurrentPage);
            service.Navigate("Second");
            service.Navigate("First");

            Assert.Same(firstPage, service.CurrentPage);
            Assert.Equal("First", service.CurrentKey);
            Assert.Equal(["First", "Second"], service.Keys);
            Assert.False(service.CanGoBack);
            Assert.True(service.CanGoForward);
        }

        [StaFact]
        public void Navigate_PageReceivesNavigationServiceOfOwningWindow()
        {
            var builder = new ContainerBuilder();
            builder.RegisterType<NavigationAwarePage>();
            builder.RegisterInstance(new NavigationAwareRegistry())
                .As<IPageRegistry>();
            builder.RegisterType<PageNavigationService>()
                .As<IPageNavigationService>()
                .InstancePerLifetimeScope();
            using IContainer container = builder.Build();
            using ILifetimeScope windowScope = container.BeginLifetimeScope();
            var service = new PageNavigationService(windowScope);

            service.Navigate("Search");

            var page = Assert.IsType<NavigationAwarePage>(service.CurrentPage);
            Assert.Same(service, page.OwningNavigationService);
        }

        private sealed class Registry: IPageRegistry
        {
            public Type Resolve(string key) => typeof(TestPage);
        }

        private sealed class TestPage: Page
        {
        }

        private sealed class NavigationAwareRegistry: IPageRegistry
        {
            public Type Resolve(string key) => typeof(NavigationAwarePage);
        }

        private sealed class NavigationAwarePage(
            IPageNavigationService navigationService): Page
        {
            public IPageNavigationService OwningNavigationService { get; } =
                navigationService;
        }
    }
}
