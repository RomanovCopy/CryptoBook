using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Security;

using System.IO;
using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public sealed class DecryptionOptionsViewModel:
        ViewModelBase,
        IViewModel,
        IWindowWithId,
        IDialogResult<DecryptionOptions>,
        IConditionalDialogResult
    {
        private readonly IWindowManager windowManager;
        private DecryptionOutputFormat selectedOutputFormat;
        private EncryptionTargetMode selectedTargetMode;

        public DecryptionOptionsViewModel(
            IWindowContext context,
            IWindowManager windowManager)
        {
            ArgumentNullException.ThrowIfNull(context);
            this.windowManager = windowManager ??
                throw new ArgumentNullException(nameof(windowManager));
            WindowId = Guid.NewGuid();
            OriginalExtension = context.Get<string>("originalExtension");
            ProcessedPath = context.Get<string>("sourcePath");
            IReadOnlyList<DecryptionOutputFormat> availableFormats =
                context.Get<IReadOnlyList<DecryptionOutputFormat>>(
                    "availableFormats");
            HasConvertibleFormats = availableFormats.Contains(
                DecryptionOutputFormat.Rtf);
            selectedOutputFormat = context.Get<DecryptionOutputFormat>(
                "defaultFormat");
            selectedTargetMode = EncryptionTargetMode.SaveAs;

            Title = LocalizationManager.GetString(
                "DecryptionOptions.Title");
            FormatSectionTitle = LocalizationManager.GetString(
                "DecryptionOptions.FormatSectionTitle");
            TargetSectionTitle = LocalizationManager.GetString(
                "DecryptionOptions.TargetSectionTitle");
            RtfLabel = LocalizationManager.GetString(
                "DecryptionOptions.FormatRtf");
            PlainTextLabel = LocalizationManager.GetString(
                "DecryptionOptions.FormatPlainText");
            OriginalLabel = ResolveOriginalLabel();
            SaveCopyLabel = LocalizationManager.GetString(
                "EncryptionMode.SaveDecryptedCopy");
            ReplaceSourceLabel = LocalizationManager.GetString(
                "EncryptionMode.ReplaceEncryptedSource");
        }

        public Guid WindowId { get; }
        public string Title { get; }
        public string FormatSectionTitle { get; }
        public string TargetSectionTitle { get; }
        public string RtfLabel { get; }
        public string PlainTextLabel { get; }
        public string OriginalLabel { get; }
        public string SaveCopyLabel { get; }
        public string ReplaceSourceLabel { get; }
        public string ProcessedPath { get; }
        public string OriginalExtension { get; }
        public bool HasConvertibleFormats { get; }
        public bool HasResult { get; private set; }

        public DecryptionOutputFormat SelectedOutputFormat
        {
            get => selectedOutputFormat;
            set
            {
                if(SetProperty(ref selectedOutputFormat, value))
                    OnPropertyChanged(nameof(WarningMessage));
            }
        }

        public EncryptionTargetMode SelectedTargetMode
        {
            get => selectedTargetMode;
            set
            {
                if(SetProperty(ref selectedTargetMode, value))
                    OnPropertyChanged(nameof(WarningMessage));
            }
        }

        public string WarningMessage
        {
            get
            {
                var parts = new List<string>
                {
                    LocalizationManager.GetString(
                        "EncryptionMode.DecryptionWarning")
                };
                if(SelectedOutputFormat == DecryptionOutputFormat.Rtf)
                {
                    parts.Add(LocalizationManager.GetString(
                        "DecryptionOptions.RtfWarning"));
                }
                else if(SelectedOutputFormat ==
                    DecryptionOutputFormat.PlainText)
                {
                    parts.Add(LocalizationManager.GetString(
                        "DecryptionOptions.PlainTextWarning"));
                    if(SelectedTargetMode == EncryptionTargetMode.ReplaceSource)
                    {
                        parts.Add(LocalizationManager.GetString(
                            "DecryptionOptions.PlainTextReplaceWarning"));
                    }
                }
                return string.Join(Environment.NewLine + Environment.NewLine, parts);
            }
        }

        public DecryptionOptions? Result => HasResult
            ? new DecryptionOptions(
                SelectedTargetMode,
                SelectedOutputFormat)
            : null;

        public ICommand ButtonOk => ok ??= new RelayCommand(_ => Accept());
        private RelayCommand? ok;
        public ICommand ButtonCancel => cancel ??=
            new RelayCommand(_ => Cancel());
        private RelayCommand? cancel;
        public ICommand Loaded => loaded ??= new RelayCommand(_ => { });
        private RelayCommand? loaded;
        public ICommand Close => close ??= new RelayCommand(_ => Cancel());
        private RelayCommand? close;
        public ICommand Closing => closing ??= new RelayCommand(_ => { });
        private RelayCommand? closing;
        public ICommand Closed => closed ??= new RelayCommand(_ => { });
        private RelayCommand? closed;

        private void Accept()
        {
            HasResult = true;
            windowManager.CloseWindow(WindowId);
        }

        private void Cancel()
        {
            HasResult = false;
            windowManager.CloseWindow(WindowId);
        }

        private string ResolveOriginalLabel()
        {
            if(!HasConvertibleFormats)
            {
                return LocalizationManager.GetString(
                    "DecryptionOptions.FormatOriginalOnly");
            }
            if(OriginalExtension.Equals(
                ".XamlPackage",
                StringComparison.OrdinalIgnoreCase))
            {
                return LocalizationManager.GetString(
                    "DecryptionOptions.FormatOriginalXamlPackage");
            }
            return LocalizationManager.Format(
                "DecryptionOptions.FormatOriginal",
                OriginalExtension.TrimStart('.').ToUpperInvariant());
        }
    }
}
