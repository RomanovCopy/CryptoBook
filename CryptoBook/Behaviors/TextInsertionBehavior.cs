using System.Windows;
using ButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using TextBox = System.Windows.Controls.TextBox;

namespace CryptoBook.Behaviors;

public static class TextInsertionBehavior
{
    public static readonly DependencyProperty TargetProperty =
        DependencyProperty.RegisterAttached("Target", typeof(TextBox), typeof(TextInsertionBehavior),
            new PropertyMetadata(null, OnTargetChanged));

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.RegisterAttached("Text", typeof(string), typeof(TextInsertionBehavior),
            new PropertyMetadata(string.Empty));

    public static void SetTarget(DependencyObject element, TextBox? value) => element.SetValue(TargetProperty, value);
    public static TextBox? GetTarget(DependencyObject element) => (TextBox?)element.GetValue(TargetProperty);
    public static void SetText(DependencyObject element, string value) => element.SetValue(TextProperty, value);
    public static string GetText(DependencyObject element) => (string)element.GetValue(TextProperty);

    private static void OnTargetChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if(element is not ButtonBase button) return;
        if(e.OldValue is not null) button.Click -= InsertText;
        if(e.NewValue is not null) button.Click += InsertText;
    }

    private static void InsertText(object sender, RoutedEventArgs e)
    {
        if(sender is not ButtonBase button || !button.IsEnabled ||
           GetTarget(button) is not { IsEnabled: true, IsReadOnly: false } target ||
           string.IsNullOrEmpty(GetText(button))) return;

        string text = GetText(button);
        int insertionStart = target.SelectionStart;
        target.SelectedText = text;
        target.Focus();
        target.Select(insertionStart + text.Length, 0);
        target.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
    }
}
