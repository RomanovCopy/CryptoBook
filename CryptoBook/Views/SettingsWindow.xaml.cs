using System.Windows;

using CryptoBook.Interfaces;

namespace CryptoBook.Views
{
    public partial class SettingsWindow: Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
        }

        private void LanguageComboBox_OnLoaded(
            object sender,
            RoutedEventArgs args)
        {
            ApplyLanguageSelection(sender);
        }

        private void LanguageComboBox_OnIsVisibleChanged(
            object sender,
            DependencyPropertyChangedEventArgs args)
        {
            if(args.NewValue is true)
                ApplyLanguageSelection(sender);
        }

        private static void ApplyLanguageSelection(object sender)
        {
            if(sender is System.Windows.Controls.ComboBox comboBox &&
               comboBox.DataContext is ISettingsViewModel viewModel)
            {
                comboBox.SelectedIndex = viewModel.SelectedLanguageIndex;
            }
        }
    }
}
