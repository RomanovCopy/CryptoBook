using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.MyControls;
using CryptoBook.MyPages;
using CryptoBook.Views;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Controls;

namespace CryptoBook.Models
{
    public class MyFrameModel: ViewModelBase, IMyFrameModel
    {

        private readonly IPageNavigationService pageNavigationService;
        private readonly IMarkdownDocumentState? markdownDocument;
        private readonly IWorkspaceDocumentSession? workspaceDocuments;
        private bool synchronizing;

        public string? CurrentPageKey => pageNavigationService.CurrentKey;
        public Page? CurrentPage => pageNavigationService.CurrentPage;


        public MyFrameModel(
            IPageNavigationService pageNavigationService,
            IMarkdownDocumentState? markdownDocument = null,
            IDocumentSession? documentSession = null)
        {
            this.pageNavigationService = pageNavigationService ?? throw new ArgumentNullException(nameof(pageNavigationService));
            this.markdownDocument = markdownDocument;
            workspaceDocuments = documentSession as IWorkspaceDocumentSession;
            pageNavigationService.PropertyChanged += (_, args) =>
            {
                if(args.PropertyName is nameof(IPageNavigationService.CurrentPage) or
                   nameof(IPageNavigationService.CurrentKey))
                {
                    if(!synchronizing && workspaceDocuments is not null)
                    {
                        string? key = pageNavigationService.CurrentKey;
                        if(key is "Home" or "MarkdownEditor" &&
                           !workspaceDocuments.SelectPage(key))
                        {
                            pageNavigationService.Navigate(workspaceDocuments.ActivePageKey);
                            if((key == "Home" && !workspaceDocuments.HasHomeDocument) ||
                               (key == "MarkdownEditor" && !workspaceDocuments.HasMarkdownDocument))
                                pageNavigationService.Remove(key);
                        }
                    }
                    OnPropertyChanged(
                        nameof(CurrentPage),
                        nameof(CurrentPageKey));
                }
            };
            synchronizing = true;
            pageNavigationService.Navigate("Home");
            synchronizing = false;
            if(workspaceDocuments is not null)
            {
                workspaceDocuments.PropertyChanged += OnWorkspaceDocumentsChanged;
                SynchronizeWorkspacePages();
            }
            else if(markdownDocument is not null)
            {
                markdownDocument.PropertyChanged += OnMarkdownDocumentChanged;
                if(markdownDocument.IsActive)
                    pageNavigationService.Navigate("MarkdownEditor");
            }
        }

        private void OnWorkspaceDocumentsChanged(object? sender, PropertyChangedEventArgs args)
        {
            if(args.PropertyName == nameof(IWorkspaceDocumentSession.ActivePageKey))
                SynchronizeWorkspacePages();
        }

        private void SynchronizeWorkspacePages()
        {
            if(synchronizing || workspaceDocuments is null)
                return;
            synchronizing = true;
            try
            {
                // Register both occupied surfaces, then focus the active document.
                if(workspaceDocuments.HasHomeDocument)
                    pageNavigationService.Navigate("Home");
                if(workspaceDocuments.HasMarkdownDocument)
                    pageNavigationService.Navigate("MarkdownEditor");
                pageNavigationService.Navigate(workspaceDocuments.ActivePageKey);
                if(!workspaceDocuments.HasMarkdownDocument)
                {
                    pageNavigationService.Remove("MarkdownEditor");
                    pageNavigationService.Remove("MarkdownSyntaxHelp");
                }
                if(!workspaceDocuments.HasHomeDocument && workspaceDocuments.HasMarkdownDocument)
                    pageNavigationService.Remove("Home");
            }
            finally
            {
                synchronizing = false;
            }
        }

        private void OnMarkdownDocumentChanged(
            object? sender,
            PropertyChangedEventArgs args)
        {
            if(args.PropertyName != nameof(IMarkdownDocumentState.IsActive) ||
               markdownDocument is null)
            {
                return;
            }

            if(markdownDocument.IsActive)
            {
                pageNavigationService.Navigate("MarkdownEditor");
            }
            else if(string.Equals(
                pageNavigationService.CurrentKey,
                "MarkdownEditor",
                StringComparison.Ordinal))
            {
                pageNavigationService.Navigate("Home");
            }
        }

        public bool CanExecute_Navigate(object? obj)
        {
            return obj is string key &&
                !string.Equals(
                    pageNavigationService.CurrentKey,
                    key,
                    StringComparison.Ordinal);
        }
        public void Execute_Navigate(object? obj)
        {
            if(obj is string key)
            {
                pageNavigationService.Navigate(key);
            }   
        }

        public bool CanExecute_GoForward(object? obj)
        {
            return pageNavigationService.CanGoForward;
        }
        public void Execute_GoForward(object? obj)
        {
            pageNavigationService.GoForward();
        }

        public bool CanExecute_GoBack(object? obj)
        {
            return pageNavigationService.CanGoBack;
        }
        public void Execute_GoBack(object? obj)
        {
            pageNavigationService.GoBack();
        }

        public bool CanExecute_RemovePage(object? obj)
        {
            if(obj is string key)
            {
                return pageNavigationService.Keys != null && pageNavigationService.Keys.Contains(key);
            } 
            return false;
        }
        public void Execute_RemovePage(object? obj)
        {
            pageNavigationService.Remove(obj as string);
        }

        public bool CanExecute_Loaded(object? obj)
        {
            return true;
        }
        public void Execute_Loaded(object? obj)
        {
        }

        public bool CanExecute_Closing(object? obj)
        {
            return true;
        }
        public void Execute_Closing(object? obj)
        {
        }

        public bool CanExecute_Close(object? obj)
        {
            return true;
        }
        public void Execute_Close(object? obj)
        {
        }

        public bool CanExecute_Closed(object? obj)
        {
            return true;
        }
        public void Execute_Closed(object? obj)
        {
        }
    }
}
