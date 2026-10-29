using CryptoBook.Interfaces;
using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Input;

namespace CryptoBook.Help;

/// <summary>Handle F1 before nested editor controls can consume its key gesture.</summary>
public sealed class HelpShortcutBehavior : Behavior<Window>
{
    protected override void OnAttached() => AssociatedObject.PreviewKeyDown += OnKey;
    protected override void OnDetaching() => AssociatedObject.PreviewKeyDown -= OnKey;
    private void OnKey(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.F1 && Keyboard.Modifiers == ModifierKeys.None &&
            AssociatedObject.DataContext is IMainWindowViewModel model && model.OpenHelp.CanExecute(null))
        { model.OpenHelp.Execute(null); e.Handled = true; }
    }
}
