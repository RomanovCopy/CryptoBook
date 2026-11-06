using CryptoBook.Adorners;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Navigation;
using System.Windows.Threading;

namespace CryptoBook.Behaviors;

/// <summary>Синхронизирует переходы по закладкам с постраничной копией документа.</summary>
public sealed class BookmarkPreviewNavigationBehavior : Behavior<FlowDocumentPageViewer>
{
    public static readonly DependencyProperty BookmarksProperty = DependencyProperty.Register(
        nameof(Bookmarks), typeof(IBookmarkService), typeof(BookmarkPreviewNavigationBehavior),
        new PropertyMetadata(null, (target, _) =>
        {
            var behavior = (BookmarkPreviewNavigationBehavior)target;
            behavior.Unsubscribe();
            if(behavior.AssociatedObject?.IsLoaded == true)
                behavior.Subscribe();
        }));

    private IBookmarkService? subscribedBookmarks;
    private DispatcherOperation? pendingHighlight;

    public IBookmarkService? Bookmarks
    {
        get => (IBookmarkService?)GetValue(BookmarksProperty);
        set => SetValue(BookmarksProperty, value);
    }

    protected override void OnAttached()
    {
        AssociatedObject.Loaded += OnLoaded;
        AssociatedObject.Unloaded += OnUnloaded;
        AssociatedObject.AddHandler(Hyperlink.RequestNavigateEvent,
            new RequestNavigateEventHandler(OnRequestNavigate));
        if(AssociatedObject.IsLoaded)
            Subscribe();
    }

    protected override void OnDetaching()
    {
        Unsubscribe();
        AssociatedObject.Loaded -= OnLoaded;
        AssociatedObject.Unloaded -= OnUnloaded;
        AssociatedObject.RemoveHandler(Hyperlink.RequestNavigateEvent,
            new RequestNavigateEventHandler(OnRequestNavigate));
    }

    private void OnLoaded(object sender, RoutedEventArgs args) => Subscribe();
    private void OnUnloaded(object sender, RoutedEventArgs args) => Unsubscribe();

    private void Subscribe()
    {
        Unsubscribe();
        subscribedBookmarks = Bookmarks;
        if(subscribedBookmarks != null)
            subscribedBookmarks.Navigated += OnNavigated;
    }

    private void Unsubscribe()
    {
        pendingHighlight?.Abort();
        pendingHighlight = null;
        if(AssociatedObject != null)
            BookmarkPreviewNavigationAdorner.Clear(AssociatedObject);
        if(subscribedBookmarks != null)
            subscribedBookmarks.Navigated -= OnNavigated;
        subscribedBookmarks = null;
    }

    private void OnRequestNavigate(object sender, RequestNavigateEventArgs args)
    {
        if(AssociatedObject.IsVisible && args.Uri != null &&
           Bookmarks?.NavigateToUri(args.Uri) == true)
        {
            args.Handled = true;
        }
    }

    private void OnNavigated(object? sender, Uri uri)
    {
        if(!AssociatedObject.IsVisible || AssociatedObject.Document is not FlowDocument document)
            return;

        string anchorId = Uri.UnescapeDataString(uri.OriginalString.TrimStart('#'));
        var anchor = new FlowDocumentWalker().Find<Span>(document)
            .FirstOrDefault(span => string.Equals(span.Name, anchorId, StringComparison.Ordinal));
        if(anchor == null)
            return;

        anchor.BringIntoView();
        AssociatedObject.Focus();
        pendingHighlight?.Abort();
        BookmarkPreviewNavigationAdorner.Clear(AssociatedObject);
        pendingHighlight = AssociatedObject.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
        {
            pendingHighlight = null;
            if(AssociatedObject?.IsVisible != true || !ReferenceEquals(AssociatedObject.Document, document))
                return;
            AssociatedObject.UpdateLayout();
            BookmarkPreviewNavigationAdorner.Show(AssociatedObject,
                anchor.ContentStart.GetInsertionPosition(LogicalDirection.Forward));
        }));
    }
}
