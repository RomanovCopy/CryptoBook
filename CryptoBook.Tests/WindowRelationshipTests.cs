using Autofac;

using CryptoBook.Converters;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Services;
using CryptoBook.Validation;
using CryptoBook.ViewModels;
using CryptoBook.Views;

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

using Xunit;

namespace CryptoBook.Tests;

[Collection("Settings UI")]
public sealed class WindowRelationshipTests
{
    [WpfFact]
    public async Task OwnedWindows_StayLinkedAndWaitForProgressBeforeClosing()
    {
        MemorySettingsProvider.Install();
        var app = System.Windows.Application.Current ?? new System.Windows.Application();
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        app.Resources["Res"] = new ResourceWrapper();
        var builder = new ContainerBuilder();
        builder.RegisterType<WindowManager>().As<IWindowManager>().SingleInstance();
        builder.RegisterType<TestWindow>();
        builder.RegisterType<SizeLocationConverter>();
        builder.RegisterType<ProgressViewModel>().As<IProgressViewModel>();
        builder.RegisterType<ProgressWindow>();
        using var container = builder.Build();
        var windows = container.Resolve<IWindowManager>();
        var progress = new ProgressDialogService(windows, Dispatcher.CurrentDispatcher,
            new StoragePathDisplayService(new StorageFacade([new LocalStorageProvider()])));
        Guid mainId = windows.CreateOwnedWindow<TestWindow>(null);
        Window main = windows.FindHostWindow(mainId)!.Window;
        app.MainWindow = main;
        main.ShowActivated = false;
        windows.ShowWindow(mainId);
        Guid explorerId = windows.CreateWindow<TestWindow>();
        Window explorer = windows.FindHostWindow(explorerId)!.Window;
        try
        {
            // Окно-владелец находится даже при отсутствии фокуса у приложения.
            Assert.Same(main, explorer.Owner);
            windows.ShowWindow(explorerId);
            explorer.Activate();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);

            main.WindowState = WindowState.Minimized;
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.False(IsWindowVisible(new WindowInteropHelper(explorer).Handle));
            main.WindowState = WindowState.Normal;
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.True(IsWindowVisible(new WindowInteropHelper(explorer).Handle));

            foreach(string outcome in new[] { "success", "cancel", "error" })
            {
                explorer.Activate();
                var started = new TaskCompletionSource<CancellationToken>();
                var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var failure = new InvalidOperationException("simulated operation failure");
                Task<int> pending = progress.RunAsync(outcome, async (reporter, token) =>
                {
                    started.SetResult(token);
                    await release.Task;
                    if(outcome == "cancel")
                        token.ThrowIfCancellationRequested();
                    if(outcome == "error")
                        throw failure;
                    reporter.Report(1, "finished");
                    return 42;
                });
                ProgressWindow dialog = Assert.Single(app.Windows.OfType<ProgressWindow>());
                var vm = Assert.IsType<ProgressViewModel>(dialog.DataContext);
                try
                {
                    CancellationToken token = await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
                    Assert.Same(explorer, dialog.Owner);
                    Assert.False(dialog.ShowInTaskbar);
                    Assert.False(IsWindowEnabled(new WindowInteropHelper(main).Handle));
                    Assert.False(IsWindowEnabled(new WindowInteropHelper(explorer).Handle));
                    Assert.True(IsWindowEnabled(new WindowInteropHelper(dialog).Handle));

                    windows.CloseWindow(mainId);
                    windows.CloseWindow(explorerId);
                    Assert.True(windows.IsWindowOpen(mainId));
                    Assert.True(windows.IsWindowOpen(explorerId));
                    Assert.True(main.IsVisible);
                    Assert.True(explorer.IsVisible);

                    if(outcome == "cancel")
                    {
                        // Повторное закрытие во время отмены тоже должно ждать завершения.
                        windows.CloseWindow(vm.WindowId);
                        windows.CloseWindow(vm.WindowId);
                        Assert.True(token.IsCancellationRequested);
                        Assert.True(dialog.IsVisible);
                        Assert.True(windows.IsWindowOpen(vm.WindowId));
                        Assert.False(pending.IsCompleted);
                    }
                    else
                        Assert.False(token.IsCancellationRequested);
                }
                finally
                {
                    release.TrySetResult();
                }

                if(outcome == "cancel")
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
                else if(outcome == "error")
                    Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => pending));
                else
                    Assert.Equal(42, await pending);

                Assert.False(dialog.IsVisible);
                Assert.False(windows.IsWindowOpen(vm.WindowId));
                Assert.True(IsWindowEnabled(new WindowInteropHelper(main).Handle));
                Assert.True(IsWindowEnabled(new WindowInteropHelper(explorer).Handle));
                Assert.False(vm.IsOperationRunning);
                Assert.False(vm.Canceled.CanExecute(null));
                Assert.Equal(outcome == "cancel", vm.CancellationToken.IsCancellationRequested);
            }

            // Вспомогательное окно закрывается вместе с основным после окончания работы.
            windows.CloseWindow(mainId);
            Assert.False(windows.IsWindowOpen(mainId));
            Assert.False(windows.IsWindowOpen(explorerId));
        }
        finally
        {
            windows.CloseWindow(explorerId);
            windows.CloseWindow(mainId);
            await Dispatcher.Yield(DispatcherPriority.ContextIdle);
        }
    }

    private sealed class TestWindow : Window
    {
        public TestWindow()
        {
            DataContext = new TestViewModel();
            Width = 400;
            Height = 250;
        }
    }

    private sealed class TestViewModel : IWindowWithId
    {
        public Guid WindowId { get; } = Guid.NewGuid();
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowEnabled(IntPtr handle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr handle);
}
