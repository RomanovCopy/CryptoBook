using CryptoBook.Infrastructure;
using System.Windows.Input;

namespace CryptoBook.AI;

public sealed class AiChatAppearanceViewModel : ViewModelBase
{
    private readonly AiSettingsStore store;
    private AiChatAppearance appearance;
    private string status = "";
    public event EventHandler? Saved;

    public AiChatAppearanceViewModel(AiSettingsStore store)
    {
        this.store = store;
        appearance = store.Current.ChatAppearance;
        Save = new RelayCommand(_ => SaveSettings());
        Reset = new RelayCommand(_ =>
        {
            appearance = new();
            OnPropertyChanged(nameof(FontFamily), nameof(FontSize), nameof(UserColor), nameof(AssistantColor),
                nameof(CodeFontFamily), nameof(CodeColor), nameof(ShowMessageBorders), nameof(RenderMarkdown));
        });
        ChooseUserColor = new RelayCommand(_ => ChooseColor(UserColor, color => UserColor = color));
        ChooseAssistantColor = new RelayCommand(_ => ChooseColor(AssistantColor, color => AssistantColor = color));
        ChooseCodeColor = new RelayCommand(_ => ChooseColor(CodeColor, color => CodeColor = color));
        CodeFonts = Fonts.Prepend(AiChatAppearance.DefaultCodeFontFamily).Distinct(StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    public IReadOnlyList<string> Fonts { get; } = System.Windows.Media.Fonts.SystemFontFamilies
        .Select(font => font.Source).Order(StringComparer.CurrentCultureIgnoreCase).ToArray();
    public IReadOnlyList<double> FontSizes { get; } = [8, 10, 11, 12, 13, 14, 16, 18, 20, 24, 28, 32, 36, 40];
    public IReadOnlyList<string> CodeFonts { get; }
    public string FontFamily { get => appearance.FontFamily; set { appearance = appearance with { FontFamily = value }; OnPropertyChanged(); } }
    public double FontSize { get => appearance.FontSize; set { appearance = appearance with { FontSize = value }; OnPropertyChanged(); } }
    public string UserColor { get => appearance.UserColor; set { appearance = appearance with { UserColor = value?.Trim() ?? "" }; OnPropertyChanged(); } }
    public string AssistantColor { get => appearance.AssistantColor; set { appearance = appearance with { AssistantColor = value?.Trim() ?? "" }; OnPropertyChanged(); } }
    public string CodeFontFamily { get => appearance.CodeFontFamily; set { appearance = appearance with { CodeFontFamily = value }; OnPropertyChanged(); } }
    public string CodeColor { get => appearance.CodeColor; set { appearance = appearance with { CodeColor = value?.Trim() ?? "" }; OnPropertyChanged(); } }
    public bool ShowMessageBorders { get => appearance.ShowMessageBorders; set { appearance = appearance with { ShowMessageBorders = value }; OnPropertyChanged(); } }
    public bool RenderMarkdown { get => appearance.RenderMarkdown; set { appearance = appearance with { RenderMarkdown = value }; OnPropertyChanged(); } }
    public string Status { get => status; private set => SetProperty(ref status, value); }
    public ICommand Save { get; }
    public ICommand Reset { get; }
    public ICommand ChooseUserColor { get; }
    public ICommand ChooseAssistantColor { get; }
    public ICommand ChooseCodeColor { get; }

    private static void ChooseColor(string value, Action<string> apply)
    {
        using var picker = new System.Windows.Forms.ColorDialog { FullOpen = true };
        if(AiChatAppearance.ValidColor(value)) picker.Color = System.Drawing.ColorTranslator.FromHtml(value);
        if(picker.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        string color = $"#{picker.Color.R:X2}{picker.Color.G:X2}{picker.Color.B:X2}";
        apply(color);
    }

    private void SaveSettings()
    {
        if(!double.IsFinite(FontSize) || FontSize is < 8 or > 40 || string.IsNullOrWhiteSpace(FontFamily) ||
           FontFamily.Length > 200 || (UserColor.Length > 0 && !AiChatAppearance.ValidColor(UserColor)) ||
           (AssistantColor.Length > 0 && !AiChatAppearance.ValidColor(AssistantColor)) ||
           string.IsNullOrWhiteSpace(CodeFontFamily) || CodeFontFamily.Length > 200 ||
           (CodeColor.Length > 0 && !AiChatAppearance.ValidColor(CodeColor)))
        {
            Status = LocalizationManager.GetString("AI.AppearanceInvalid");
            return;
        }
        try
        {
            store.Save(store.Current with { ChatAppearance = appearance });
            Saved?.Invoke(this, EventArgs.Empty);
        }
        catch(Exception) { Status = LocalizationManager.GetString("AI.SaveFailed"); }
    }
}
