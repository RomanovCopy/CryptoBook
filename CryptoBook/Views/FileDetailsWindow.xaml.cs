using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.ViewModels;

using System.Runtime.InteropServices;
using System.Windows;

namespace CryptoBook.Views;

public partial class FileDetailsWindow: Window
{
    public FileDetailsWindow(FileDetailsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.LoadAsync();
        Closed += (_, _) => viewModel.Dispose();
    }

    private void CopyReport_Click(object sender, RoutedEventArgs e) =>
        Copy(((FileDetailsViewModel)DataContext).CreateReport());

    private void CopySelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = PropertiesGrid.SelectedItems.OfType<FilePropertyEntry>().ToArray();
        if(selected.Length > 0)
            Copy(((FileDetailsViewModel)DataContext).CreateReport(selected));
    }

    private void Copy(string text)
    {
        try { System.Windows.Clipboard.SetText(text); }
        catch(ExternalException exception)
        {
            System.Windows.MessageBox.Show(this, exception.Message,
                LocalizationManager.GetString("FileDetails.Title"));
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
