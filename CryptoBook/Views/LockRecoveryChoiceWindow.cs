using System.Windows;
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
        Title = "Восстановление документа";
        Width = 470;
        Height = originalAvailable ? 250 : 235;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty, "CurrentWindowBackground");
        SetResourceReference(ForegroundProperty, "CurrentWindowForeground");

        var root = new StackPanel { Margin = new Thickness(20) };
        root.Children.Add(new TextBlock
        {
            Text = originalAvailable
                ? $"Открыть последний файл «{documentName}»?"
                : $"Исходный файл «{documentName}» недоступен. Восстановить снимок?",
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
        var nameLabel = new TextBlock { Text = "Документ", FontSize = 11 };
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
                ? "Исходный путь не сохранён"
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
            buttons.Children.Add(CreateButton("Открыть", LockRecoveryChoice.Open));
        buttons.Children.Add(CreateButton("Восстановить снимок", LockRecoveryChoice.Restore));
        buttons.Children.Add(CreateButton("Отмена", LockRecoveryChoice.Cancel));
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
