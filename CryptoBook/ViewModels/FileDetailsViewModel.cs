using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Services;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows.Data;
using System.Windows.Input;

namespace CryptoBook.ViewModels;

public sealed class FileDetailsViewModel: ViewModelBase, IWindowWithId, IDisposable
{
    public const string TargetContextKey = "fileDetailsTarget";
    private readonly IFileDetailsService service;
    private readonly FileDetailsTarget target;
    private readonly ObservableCollection<FilePropertyEntry> rows = [];
    private CancellationTokenSource? operation;
    private string searchText = "", status = "", detectedType = "", inspectionMessage = "";
    private bool isBusy, hasWarning, disposed;
    private double progress;
    private int generation;

    public FileDetailsViewModel(IFileDetailsService service, IWindowContext context)
    {
        this.service = service;
        target = context.Get<FileDetailsTarget>(TargetContextKey);
        VisibleRows = new ListCollectionView(rows) { Filter = FilterRow };
        RefreshCommand = new RelayCommand(async _ => await LoadAsync(), _ => !IsBusy);
        HashCommand = new RelayCommand(async _ => await ComputeHashesAsync(),
            _ => !IsBusy && !target.IsDirectory && target.LocalPath is not null && !disposed);
        CancelCommand = new RelayCommand(_ => operation?.Cancel(), _ => IsBusy);
    }

    public Guid WindowId { get; } = Guid.NewGuid();
    public string Title => FileDetailsService.L("Title") + " — " + FileTypeInspector.EscapeName(target.Name);
    public string DisplayPath => FileTypeInspector.EscapeName(target.DisplayPath);
    public ICollectionView VisibleRows { get; }
    public IReadOnlyList<FilePropertyEntry> Rows => rows;
    public ICommand RefreshCommand { get; }
    public ICommand HashCommand { get; }
    public ICommand CancelCommand { get; }

    public string SearchText
    {
        get => searchText;
        set { if(SetProperty(ref searchText, value)) VisibleRows.Refresh(); }
    }
    public string Status { get => status; private set => SetProperty(ref status, value); }
    public string DetectedType { get => detectedType; private set => SetProperty(ref detectedType, value); }
    public string InspectionMessage { get => inspectionMessage; private set => SetProperty(ref inspectionMessage, value); }
    public bool HasWarning { get => hasWarning; private set => SetProperty(ref hasWarning, value); }
    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            SetProperty(ref isBusy, value);
            CommandManager.InvalidateRequerySuggested();
        }
    }
    public double Progress { get => progress; private set => SetProperty(ref progress, value); }

    private bool FilterRow(object item) => item is FilePropertyEntry row &&
        (string.IsNullOrWhiteSpace(SearchText) || new[] { row.Category, row.Name, row.Value, row.Key }
            .Any(value => value.Contains(SearchText.Trim(), StringComparison.CurrentCultureIgnoreCase)));

    public async Task LoadAsync()
    {
        if(disposed || IsBusy)
            return;
        using var source = new CancellationTokenSource();
        operation = source;
        int current = ++generation;
        IsBusy = true;
        Progress = 0;
        Status = FileDetailsService.L("Loading");
        DetectedType = FileDetailsService.L("UnknownType");
        InspectionMessage = "";
        HasWarning = false;
        rows.Clear();
        try
        {
            if(!target.IsDirectory && target.LocalPath is string path)
            {
                try
                {
                    var inspection = await FileTypeInspector.InspectAsync(path, source.Token);
                    if(disposed) return;
                    DetectedType = inspection.DetectedType;
                    HasWarning = inspection.HasWarning;
                    InspectionMessage = string.Join(Environment.NewLine, inspection.Messages);
                    var rowsList = new List<FilePropertyEntry>();
                    FileDetailsService.Add(rowsList, "Inspection", "DetectedType", DetectedType);
                    FileDetailsService.Add(rowsList, "Inspection", "Header", inspection.Header);
                    foreach(string message in inspection.Messages)
                        FileDetailsService.Add(rowsList, "Inspection", "Status", message);
                    foreach(var row in rowsList) rows.Add(row);
                    if(DetectedType.StartsWith("Windows PE", StringComparison.Ordinal))
                    {
                        var peRows = await Task.Run(() =>
                        {
                            var result = new List<FilePropertyEntry>();
                            FileDetailsService.TrySection(result, "PE", () => FileDetailsService.ReadPeHeaders(path, result));
                            return result;
                        }, source.Token);
                        source.Token.ThrowIfCancellationRequested();
                        if(disposed) return;
                        foreach(var row in peRows) rows.Add(row);
                    }
                }
                catch(Exception exception) when(exception is not OperationCanceledException)
                {
                    HasWarning = true;
                    InspectionMessage = FileDetailsService.L("InspectionFailed") + Environment.NewLine + exception.Message;
                    rows.Add(new(FileDetailsService.L("Inspection"), FileDetailsService.L("Unavailable"), InspectionMessage, "Error"));
                }
            }
            else
            {
                DetectedType = FileDetailsService.L(target.IsDirectory ? "Directory" : "UnknownType");
                InspectionMessage = FileDetailsService.L(target.IsDirectory ? "DirectoryLimit" : "RemoteLimit");
            }

            var details = await service.ReadAsync(target, source.Token);
            source.Token.ThrowIfCancellationRequested();
            if(disposed) return;
            foreach(var row in details) rows.Add(row);
            if(!target.IsDirectory && target.LocalPath is string signaturePath)
            {
                Status = FileDetailsService.L("CheckingSignature");
                var signature = await service.ReadSignatureAsync(signaturePath, source.Token);
                source.Token.ThrowIfCancellationRequested();
                if(disposed) return;
                foreach(var row in signature) rows.Add(row);
            }
            Status = LocalizationManager.Format("FileDetails.Loaded", rows.Count);
        }
        catch(OperationCanceledException) { if(!disposed) Status = FileDetailsService.L("Cancelled"); }
        catch(Exception exception) { if(!disposed) Status = exception.Message; }
        finally
        {
            if(current == generation && !disposed)
            {
                IsBusy = false;
                operation = null;
            }
        }
    }

    public async Task ComputeHashesAsync()
    {
        if(disposed || IsBusy || target.IsDirectory || target.LocalPath is not string path)
            return;
        using var source = new CancellationTokenSource();
        operation = source;
        IsBusy = true;
        Progress = 0;
        Status = FileDetailsService.L("Hashing");
        foreach(var row in rows.Where(row => row.Key is "SHA256" or "SHA512").ToArray()) rows.Remove(row);
        try
        {
            var reporter = new Progress<double>(value =>
            {
                if(!disposed && ReferenceEquals(operation, source)) Progress = value;
            });
            var hashes = await service.ComputeHashesAsync(path, reporter, source.Token);
            source.Token.ThrowIfCancellationRequested();
            if(disposed) return;
            foreach(var row in hashes) rows.Add(row);
            Status = LocalizationManager.Format("FileDetails.Loaded", rows.Count);
        }
        catch(OperationCanceledException) { if(!disposed) Status = FileDetailsService.L("Cancelled"); }
        catch(Exception exception) { if(!disposed) Status = exception.Message; }
        finally
        {
            if(!disposed) { operation = null; IsBusy = false; }
        }
    }

    public string CreateReport(IEnumerable<FilePropertyEntry>? selection = null)
    {
        var report = new StringBuilder().AppendLine(Title).AppendLine(DisplayPath)
            .AppendLine(DetectedType).AppendLine(InspectionMessage).AppendLine();
        foreach(var row in selection ?? rows)
            report.Append('[').Append(row.Category).Append("] ").Append(row.Name)
                .Append(" (").Append(row.Key).AppendLine(")").AppendLine(row.Value).AppendLine();
        return report.ToString();
    }

    public void Dispose()
    {
        if(disposed) return;
        disposed = true;
        generation++;
        operation?.Cancel();
        operation = null;
    }
}
