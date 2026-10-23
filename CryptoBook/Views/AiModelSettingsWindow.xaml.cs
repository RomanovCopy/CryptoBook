using CryptoBook.AI;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CryptoBook.Views;

public partial class AiModelSettingsWindow : Window
{
    public AiModelSettingsWindow(AiModelSettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.Saved += (_, _) => Close();
    }
    private void SaveWindow(object sender, RoutedEventArgs e)
    {
        if(CommitInputs() && DataContext is AiModelSettingsViewModel viewModel && viewModel.Save.CanExecute(null))
            viewModel.Save.Execute(null);
    }
    private void TestWindow(object sender, RoutedEventArgs e)
    {
        if(CommitInputs() && DataContext is AiModelSettingsViewModel viewModel && viewModel.Test.CanExecute(null))
            viewModel.Test.Execute(null);
    }
    private void RetuneWindow(object sender, RoutedEventArgs e)
    {
        if(CommitInputs() && DataContext is AiModelSettingsViewModel viewModel && viewModel.Retune.CanExecute(null))
            viewModel.Retune.Execute(null);
    }
    private bool CommitInputs()
    {
        bool updated = BindingGroup.UpdateSources();
        if(updated && !HasValidationError(this)) return true;
        if(DataContext is AiModelSettingsViewModel viewModel) viewModel.ReportInvalidInput();
        return false;
    }
    private void PerformanceTestWindow(object sender, RoutedEventArgs e)
    {
        if(CommitInputs() && DataContext is AiModelSettingsViewModel viewModel && viewModel.PerformanceTest.CanExecute(null))
            viewModel.PerformanceTest.Execute(null);
    }
    private static bool HasValidationError(DependencyObject element)
    {
        if(Validation.GetHasError(element)) return true;
        for(int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            if(HasValidationError(VisualTreeHelper.GetChild(element, i))) return true;
        return false;
    }
    private void CloseWindow(object sender, RoutedEventArgs e) => Close();
}
