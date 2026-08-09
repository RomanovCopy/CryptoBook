using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface IHomeViewModel: IPageViewModel
    {
        Action<object> BehaviorReady { get; set; }
        IRichtextboxViewModel DocumentView { get; }
        IRecentDocumentsViewModel RecentDocuments { get; }
        IPinnedDocumentsViewModel PinnedDocuments { get; }
        bool HasDocument { get; }
        string WorkspaceDirectoryDisplay { get; }

        ICommand NewDocument { get; }
        ICommand OpenDocument { get; }
        ICommand ChooseWorkspace { get; }
    }
}
