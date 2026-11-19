using Microsoft.Xaml.Behaviors;

using System.Security;
using System.Windows;
using System.Windows.Controls;

namespace CryptoBook.Behaviors;

/// <summary>Transfers a short-lived secure copy to the dialog state; clearing it clears the editor.</summary>
public sealed class SecurePasswordValueBehavior: Behavior<PasswordBox>
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value),
        typeof(SecureString), typeof(SecurePasswordValueBehavior),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

    public SecureString? Value
    {
        get => (SecureString?)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.PasswordChanged += OnPasswordChanged;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.PasswordChanged -= OnPasswordChanged;
        SetCurrentValue(ValueProperty, null);
        base.OnDetaching();
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs args) =>
        SetCurrentValue(ValueProperty, AssociatedObject.SecurePassword);

    private static void OnValueChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var behavior = (SecurePasswordValueBehavior)sender;
        if(args.NewValue is null && behavior.AssociatedObject is PasswordBox box)
        {
            using SecureString current = box.SecurePassword;
            if(current.Length > 0) box.Clear();
        }
    }
}
