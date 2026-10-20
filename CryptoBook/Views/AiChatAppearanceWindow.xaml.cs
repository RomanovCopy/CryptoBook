using CryptoBook.AI;
using System.Windows;

namespace CryptoBook.Views;

public partial class AiChatAppearanceWindow : Window
{
    public AiChatAppearanceWindow(AiChatAppearanceViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.Saved += OnSaved;
        Closed += (_, _) => viewModel.Saved -= OnSaved;
    }

    private void OnSaved(object? sender, EventArgs e) => Close();
}
