using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Services;

using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows.Input;

namespace CryptoBook.ViewModels;

public sealed class ArchiveOperationViewModel: ViewModelBase, IViewModel, IWindowWithId
{
    public const string SourcesContextKey = "archiveSources";
    public const string ActionContextKey = "archiveAction";
    private readonly IArchiveService service;
    private readonly IWindowManager windows;
    private readonly IFolderPickerService folders;
    private readonly IProgressDialogService progress;
    private readonly string[] sources;
    private readonly ArchiveAction action;
    private string destination;
    private string outputName;
    private int formatIndex;
    private string status = string.Empty;
    private bool isBusy;
    private bool completed;
    private SecureString? password;

    public ArchiveOperationViewModel(IWindowContext context, IArchiveService service,
        IWindowManager windows, IFolderPickerService folders, IProgressDialogService progress)
    {
        this.service = service;
        this.windows = windows;
        this.folders = folders;
        this.progress = progress;
        sources = context.Get<IReadOnlyList<string>>(SourcesContextKey).ToArray();
        action = context.Get<ArchiveAction>(ActionContextKey);
        destination = Path.GetDirectoryName(sources[0]) ?? string.Empty;
        string stem = Path.GetFileNameWithoutExtension(sources[0]);
        if(Directory.Exists(sources[0])) stem = Path.GetFileName(sources[0]);
        outputName = IsCreate ? stem + ".zip" : stem;
        if(string.IsNullOrWhiteSpace(outputName)) outputName = "Archive" + (IsCreate ? ".zip" : "");
        RunCommand = new AsyncRelayCommand((_, _) => RunAsync(), _ => CanEdit);
        BrowseCommand = new AsyncRelayCommand(async (_, token) =>
        {
            string? selected = await folders.PickFolderAsync(Destination, token);
            if(selected is not null) Destination = selected;
        }, _ => CanEdit);
        Close = new RelayCommand(_ => windows.CloseWindow(WindowId), _ => !IsBusy);
        Closing = new RelayCommand(parameter =>
        {
            if(IsBusy && parameter is CancelEventArgs args) args.Cancel = true;
        });
        Closed = new RelayCommand(_ => Password = null);
        Loaded = new RelayCommand(_ => { });
    }

    public Guid WindowId { get; } = Guid.NewGuid();
    public bool IsCreate => action == ArchiveAction.Create;
    public bool IsExtract => action == ArchiveAction.Extract;
    public bool HasDestination => action != ArchiveAction.Test;
    public string Title => ArchivePathPolicy.Text(action.ToString());
    public string Hint => ArchivePathPolicy.Text(action + "Hint");
    public string SourceSummary => string.Join(Environment.NewLine, sources);
    public string Destination { get => destination; set => SetProperty(ref destination, value); }
    public string OutputName { get => outputName; set => SetProperty(ref outputName, value); }
    public int FormatIndex
    {
        get => formatIndex;
        set
        {
            if(SetProperty(ref formatIndex, value)) OutputName = Path.ChangeExtension(OutputName, value == 1 ? ".7z" : ".zip");
        }
    }
    public string MaximumGigabytes { get; set; } = "20";
    public SecureString? Password
    {
        get => password;
        set
        {
            if(ReferenceEquals(password, value)) return;
            password?.Dispose();
            password = value;
            OnPropertyChanged();
        }
    }
    public string Status { get => status; private set => SetProperty(ref status, value); }
    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            SetProperty(ref isBusy, value);
            OnPropertyChanged(nameof(CanEdit));
            RunCommand.RaiseCanExecuteChanged();
            BrowseCommand.RaiseCanExecuteChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }
    public bool CanEdit => !IsBusy && !completed;
    public AsyncRelayCommand RunCommand { get; }
    public AsyncRelayCommand BrowseCommand { get; }
    public ICommand Loaded { get; }
    public ICommand Close { get; }
    public ICommand Closing { get; }
    public ICommand Closed { get; }

    private async Task RunAsync()
    {
        using SecureString? securePassword = Password?.Copy();
        Password = null;
        string? passwordText = null;
        IsBusy = true;
        Status = string.Empty;
        try
        {
            if(securePassword is not null)
            {
                IntPtr buffer = Marshal.SecureStringToGlobalAllocUnicode(securePassword);
                try { passwordText = Marshal.PtrToStringUni(buffer); }
                finally { Marshal.ZeroFreeGlobalAllocUnicode(buffer); }
            }
            string? output = null;
            ArchiveExtractionLimits? limits = null;
            if(HasDestination)
            {
                string parent = ArchivePathPolicy.LocalPath(Destination);
                if(!Directory.Exists(parent)) throw new DirectoryNotFoundException(parent);
                string name = ArchivePathPolicy.ValidateEntry(OutputName);
                if(name.Contains(Path.DirectorySeparatorChar))
                    throw new ArgumentException(ArchivePathPolicy.Text("NameOnly"));
                output = Path.Combine(parent, name);
            }
            if(IsExtract)
            {
                if(!long.TryParse(MaximumGigabytes, NumberStyles.None, CultureInfo.InvariantCulture, out long gigabytes) ||
                   gigabytes < 1 || gigabytes > long.MaxValue / (1024L * 1024 * 1024))
                    throw new ArgumentException(ArchivePathPolicy.Text("InvalidLimit"));
                limits = new ArchiveExtractionLimits(gigabytes * 1024L * 1024 * 1024);
            }
            ArchiveOperationResult result = await progress.RunAsync(Title, (reporter, token) => action switch
            {
                ArchiveAction.Create => service.CreateAsync(sources, output!, (ArchiveFormat)FormatIndex, passwordText, reporter, token),
                ArchiveAction.Extract => service.ExtractAsync(sources[0], output!, passwordText, limits, reporter, token),
                _ => service.TestAsync(sources[0], passwordText, reporter, token)
            });
            completed = true;
            Status = action == ArchiveAction.Test ? ArchivePathPolicy.Text("TestSuccess") :
                LocalizationManager.Format("Archive.Success", result.OutputPath, result.FileCount);
        }
        catch(OperationCanceledException) { Status = ArchivePathPolicy.Text("Cancelled"); }
        catch(Exception exception) { Status = exception.Message; }
        finally { passwordText = null; IsBusy = false; }
    }
}
