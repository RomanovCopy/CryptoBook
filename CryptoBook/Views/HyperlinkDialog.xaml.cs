using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System.Windows;

namespace CryptoBook.Views
{
    public partial class HyperlinkDialog: Window, IWindowWithId, IDialogResult<HyperlinkDialogResult?>
    {
        public Guid WindowId { get; } = Guid.NewGuid();

        public string Url { get; set; } = string.Empty;
        public string DisplayText { get; set; } = string.Empty;
        public HyperlinkDialogResult? Result { get; private set; }

        public HyperlinkDialog(IWindowContext context)
        {
            InitializeComponent();
            if(context.TryGet<string>("displayText", out var value))
                DisplayText = value ?? string.Empty;

            DataContext = this;
            Loaded += (_, _) => UrlTextBox.Focus();
        }

        private void InsertButton_Click(object sender, RoutedEventArgs e)
        {
            if(!TryNormalizeHttpUrl(Url, out var normalizedUrl))
            {
                ValidationMessage.Text =
                    "Введите корректный адрес HTTP или HTTPS, например https://example.com.";
                UrlTextBox.Focus();
                UrlTextBox.SelectAll();
                return;
            }

            string text = string.IsNullOrWhiteSpace(DisplayText)
                ? normalizedUrl
                : DisplayText.Trim();

            Result = new HyperlinkDialogResult(normalizedUrl, text);
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Result = null;
            DialogResult = false;
        }

        internal static bool TryNormalizeHttpUrl(string? value, out string normalizedUrl)
        {
            normalizedUrl = string.Empty;
            string candidate = value?.Trim() ?? string.Empty;
            if(candidate.Length == 0)
                return false;

            if(!candidate.Contains("://", StringComparison.Ordinal))
                candidate = $"https://{candidate}";

            if(!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) ||
               (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
               string.IsNullOrWhiteSpace(uri.Host))
            {
                return false;
            }

            normalizedUrl = uri.AbsoluteUri;
            return true;
        }
    }
}
