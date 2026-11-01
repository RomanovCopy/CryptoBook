using System.Windows;
using CryptoBook.Infrastructure;
using System.Windows.Controls;
using WpfButton = System.Windows.Controls.Button;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;

namespace CryptoBook.Views;

public enum LockRecoveryChoice
{
    Cancel,
    Open,
    Restore
}

/// Диалог выбора восстановления с явными подписями действий.
public sealed class LockRecoveryChoiceWindow : Window
{
    public LockRecoveryChoice Choice { get; private set; } = LockRecoveryChoice.Cancel;

    public LockRecoveryChoiceWindow(
        string documentName,
        string? originalPath,
        bool originalAvailable)
    {
        Title = LocalizationManager.GetString("Security.RestoreTitle");
        Width = 640;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty, "CurrentWindowBackground");
        SetResourceReference(ForegroundProperty, "CurrentWindowForeground");

        var root = new StackPanel { Margin = new Thickness(20) };
        root.Children.Add(new TextBlock
        {
            Text = originalAvailable
                ? LocalizationManager.Format("Security.OpenLastFile", documentName)
                : LocalizationManager.Format("Security.SourceUnavailableRestore", documentName),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        });

        var fileCard = new Border
        {
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 18),
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1)
        };
        fileCard.SetResourceReference(Border.BackgroundProperty, "CurrentControlBackground");
        fileCard.SetResourceReference(Border.BorderBrushProperty, "CurrentBorderColor");

        var fileInfo = new StackPanel();
        var nameLabel = new TextBlock { Text = LocalizationManager.GetString("Security.DocumentLabel"), FontSize = 11 };
        nameLabel.SetResourceReference(TextBlock.ForegroundProperty, "CurrentMutedForeground");
        fileInfo.Children.Add(nameLabel);
        fileInfo.Children.Add(new TextBlock
        {
            Text = documentName,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = documentName
        });

        var pathLabel = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(originalPath)
                ? LocalizationManager.GetString("Security.RecoveryUnsavedPath")
                : originalPath,
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 36,
            Margin = new Thickness(0, 5, 0, 0),
            ToolTip = originalPath
        };
        pathLabel.SetResourceReference(TextBlock.ForegroundProperty, "CurrentMutedForeground");
        fileInfo.Children.Add(pathLabel);
        fileCard.Child = fileInfo;
        root.Children.Add(fileCard);

        var buttons = new StackPanel
        {
            Orientation = WpfOrientation.Horizontal,
            HorizontalAlignment = WpfHorizontalAlignment.Right
        };
        if(originalAvailable)
            buttons.Children.Add(CreateButton(LocalizationManager.GetString("Common.Open"), LockRecoveryChoice.Open));
        buttons.Children.Add(CreateButton(LocalizationManager.GetString("Security.RestoreSnapshot"), LockRecoveryChoice.Restore));
        buttons.Children.Add(CreateButton(LocalizationManager.GetString("Common.Cancel"), LockRecoveryChoice.Cancel));
        root.Children.Add(buttons);
        Content = root;
    }

    private WpfButton CreateButton(string text, LockRecoveryChoice choice)
    {
        var button = new WpfButton
        {
            Content = text,
            MinWidth = 110,
            Margin = new Thickness(5, 0, 0, 0),
            Padding = new Thickness(10, 5, 10, 5)
        };
        button.Click += (_, _) =>
        {
            Choice = choice;
            if(choice == LockRecoveryChoice.Cancel)
            {
                Close();
                return;
            }
            DialogResult = true;
        };
        return button;
    }
}
