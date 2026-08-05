using Autofac;

using CryptoBook.Interfaces;
using CryptoBook.Services;
using CryptoBook.ViewModels;

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

using Xunit;

namespace CryptoBook.Tests
{
    public sealed class TitleBarNavigationTests
    {
        [StaFact]
        public void MultiplePageNavigation_ImmediatelyRefreshesTitleBarCommands()
        {
            _ = Application.Current ?? new Application();
            var builder = new ContainerBuilder();
            builder.RegisterType<TestPage>();
            builder.RegisterInstance(new Registry()).As<IPageRegistry>();
            using Autofac.IContainer container = builder.Build();
            using var navigation = new PageNavigationService(container);
            navigation.Navigate("Home");

            var titleBarModel = new TitleBarModelStub(navigation);
            using var viewModel = new TitleBarViewModel(
                titleBarModel,
                new DocumentTitleProviderStub(),
                navigation);
            int backStateChangeCount = 0;
            int forwardStateChangeCount = 0;
            viewModel.ButtonBack_Click.CanExecuteChanged +=
                (_, _) => backStateChangeCount++;
            viewModel.ButtonForward_Click.CanExecuteChanged +=
                (_, _) => forwardStateChangeCount++;

            navigation.Navigate("WorkspaceSearch");
            navigation.Navigate("Library");
            navigation.Navigate("Settings");

            Assert.True(backStateChangeCount >= 2);
            Assert.True(forwardStateChangeCount >= 2);
            Assert.True(viewModel.ButtonBack_Click.CanExecute(null));
            Assert.False(viewModel.ButtonForward_Click.CanExecute(null));

            viewModel.ButtonBack_Click.Execute(null);

            Assert.Equal("Library", navigation.CurrentKey);
            Assert.True(viewModel.ButtonBack_Click.CanExecute(null));
            Assert.True(viewModel.ButtonForward_Click.CanExecute(null));

            viewModel.ButtonBack_Click.Execute(null);
            navigation.Navigate("Home");

            viewModel.ButtonForward_Click.Execute(null);

            Assert.Equal("WorkspaceSearch", navigation.CurrentKey);
        }

        private sealed class Registry: IPageRegistry
        {
            public Type Resolve(string key) => typeof(TestPage);
        }

        private sealed class TestPage: Page
        {
        }

        private sealed class DocumentTitleProviderStub: IDocumentTitleProvider
        {
            public event PropertyChangedEventHandler? PropertyChanged
            {
                add { }
                remove { }
            }
            public string Title => string.Empty;
            public string? Path => null;
            public void Dispose()
            {
            }
        }

        private sealed class TitleBarModelStub(
            IPageNavigationService navigationService): ITitleBarModel
        {
            public event PropertyChangedEventHandler? PropertyChanged
            {
                add { }
                remove { }
            }
            public double MyFontSize { get; set; }
            public bool CanExecute_ButtonBack_Click(object? obj) =>
                navigationService.CanGoBack;
            public void Execute_ButtonBack_Click(object? obj) =>
                navigationService.GoBack();
            public bool CanExecute_ButtonForward_Click(object? obj) =>
                navigationService.CanGoForward;
            public void Execute_ButtonForward_Click(object? obj) =>
                navigationService.GoForward();
            public bool CanExecute_TitleBarDoubleClick(object? obj) => true;
            public void Execute_TitleBarDoubleClick(object? obj)
            {
            }
            public bool CanExecute_MouseLeftButtonDown(object? obj) => true;
            public void Execute_MouseLeftButtonDown(object? obj)
            {
            }
            public bool CanExecute_TitleBarMouseMove(object? obj) => true;
            public void Execute_TitleBarMouseMove(object? obj)
            {
            }
            public bool CanExecute_ToggleMenu_Click(object? obj) => true;
            public void Execute_ToggleMenu_Click(object? obj)
            {
            }
            public bool CanExecute_ButtonSettingsClick(object? obj) => true;
            public void Execute_ButtonSettingsClick(object? obj)
            {
            }
            public bool CanExecute_MinButtonClick(object? obj) => true;
            public void Execute_MinButtonClick(object? obj)
            {
            }
            public bool CanExecute_MaxButtonClick(object? obj) => true;
            public void Execute_MaxButtonClick(object? obj)
            {
            }
            public bool CanExecute_CloseButtonClick(object? obj) => true;
            public void Execute_CloseButtonClick(object? obj)
            {
            }
            public bool CanExecute_GoToWindow(object? obj) => true;
            public void Execute_GoToWindow(object? obj)
            {
            }
            public bool CanExecute_Close(object? obj) => true;
            public void Execute_Close(object? obj)
            {
            }
            public bool CanExecute_Loaded(object? obj) => true;
            public void Execute_Loaded(object? obj)
            {
            }
            public bool CanExecute_Closing(object? obj) => true;
            public void Execute_Closing(object? obj)
            {
            }
            public bool CanExecute_Closed(object? obj) => true;
            public void Execute_Closed(object? obj)
            {
            }
        }
    }
}
