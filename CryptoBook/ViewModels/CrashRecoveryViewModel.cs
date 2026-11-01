using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Services;
using CryptoBook.Security;
using System.Windows.Input;

namespace CryptoBook.ViewModels;

/// <summary>Recovery requested explicitly from the File menu, independent of the lock snapshot.</summary>
public sealed class CrashRecoveryViewModel : ViewModelBase
{
    private readonly IDocumentRecoveryService recovery;
    private readonly DocumentCloseCoordinator close;
    private readonly IPageNavigationService navigation;
    private readonly IDocumentSession session;
    private string error = "";
    public string Error { get => error; private set => SetProperty(ref error, value); }
    public ICommand Restore { get; }
    public bool CanRestore
    {
        get { try { return recovery.DeferredSnapshotTime is not null; } catch { return false; } }
    }

    public CrashRecoveryViewModel(IDocumentRecoveryService recovery, DocumentCloseCoordinator close,
        IPageNavigationService navigation, IDocumentSession session, IMessageService messages, IKeyResetService? keyResetService = null)
    {
        this.recovery = recovery; this.close = close; this.navigation = navigation; this.session = session;
        Restore = new AsyncRelayCommand(async (_, token) =>
        {
            Error = "";
            try
            {
                if(keyResetService?.State is KeyResetState.Resetting or KeyResetState.Restoring or KeyResetState.Unlocking ||
                   keyResetService?.HasRetainedDocument == true) return;
                using IDisposable? pause = keyResetService?.Pause();
                if(!await this.close.TryApproveDocumentReplacementAsync()) return;
                if(await this.recovery.RestoreDeferredSnapshotAsync(token))
                    this.navigation.Navigate((this.session as IWorkspaceDocumentSession)?.ActivePageKey ?? "Home");
            }
            catch(OperationCanceledException) { }
            catch(Exception)
            {
                Error = LocalizationManager.GetString("Recovery.CrashRestoreFailed");
                await messages.ShowMessage(LocalizationManager.GetString("Recovery.RestoreCrash"), Error);
            }
            finally { CommandManager.InvalidateRequerySuggested(); }
        }, _ => CanRestore);
    }
}
