using CryptoBook.Interfaces;
using CryptoBook.Views;
using System.Windows;
using Application = System.Windows.Application;

namespace CryptoBook.Help;

public sealed class HelpWindowService(IWindowManager windows) : IHelpWindowService
{
    private Guid? windowId;
    public void Open(string? topicId = null)
    {
        try { OpenCore(topicId); }
        catch (Exception error) when (error is Autofac.Core.DependencyResolutionException or
            System.IO.IOException or System.Text.Json.JsonException)
        {
            windowId = null;
            System.Windows.MessageBox.Show(Infrastructure.LocalizationManager.GetString("Help.LoadError"),
                Infrastructure.LocalizationManager.GetString("Help.Title"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenCore(string? topicId)
    {
        if (windowId is not Guid id || !windows.IsWindowOpen(id))
        {
            Window? owner = Application.Current?.Windows.OfType<MainWindow>().FirstOrDefault();
            windowId = windows.CreateOwnedWindow<HelpWindow>(owner);
        }
        var host = windows.FindHostWindow(windowId.Value);
        if (topicId is not null && host?.Window.DataContext is HelpWindowViewModel model) model.OpenTopic(topicId);
        windows.ShowWindow(windowId.Value);
        windows.ActivateWindow(windowId.Value);
    }
}
