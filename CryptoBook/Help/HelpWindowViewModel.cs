using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Services;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Button = System.Windows.Controls.Button;
using FlowDirection = System.Windows.FlowDirection;
using SystemColors = System.Windows.SystemColors;
using Binding = System.Windows.Data.Binding;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace CryptoBook.Help;

public sealed class HelpTreeNode : ViewModelBase
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public IReadOnlyList<HelpTreeNode> Children { get; init; } = [];
    private bool expanded = true, selected;
    public bool IsExpanded { get => expanded; set => SetProperty(ref expanded, value); }
    public bool IsSelected { get => selected; set => SetProperty(ref selected, value); }
}

public sealed record HelpImage(System.Windows.Media.Imaging.BitmapSource Source, string Caption);

public sealed class HelpWindowViewModel : ViewModelBase, IWindowWithId, IDisposable
{
    private readonly HelpContentService content;
    private readonly IHelpPreferences preferences;
    private readonly DispatcherTimer searchTimer;
    private readonly List<(string Id, double Offset)> history = [];
    private int historyIndex = -1;
    private string query = "", currentId = "start.interface", status = "";
    private string culture = LocalizationManager.CurrentCultureName;
    private FlowDocument article = new();
    private double articleWidth = 700;
    private HelpImage? image;
    private bool disposed;

    public HelpWindowViewModel(HelpContentService content, IHelpPreferences preferences)
    {
        this.content = content;
        this.preferences = preferences;
        SavedState = preferences.Load();
        Navigate = new RelayCommand(p => { if (p is string id) OpenTopic(id); });
        Back = new RelayCommand(_ => MoveHistory(-1), _ => historyIndex > 0);
        Forward = new RelayCommand(_ => MoveHistory(1), _ => historyIndex + 1 < history.Count);
        ClearSearch = new RelayCommand(_ => Query = "");
        ShowImage = new RelayCommand(p => { if (p is HelpImage value) { Image = value; } });
        CloseImage = new RelayCommand(_ => Image = null);
        searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        searchTimer.Tick += SearchTick;
        LocalizationManager.CultureChanged += CultureChanged;
        BuildTree();
        OpenTopic(content.IsAvailable(SavedState.Topic) ? SavedState.Topic : "start.interface");
    }

    public Guid WindowId { get; } = Guid.NewGuid();
    public HelpWindowState SavedState { get; }
    public ObservableCollection<HelpTreeNode> Sections { get; } = [];
    public ObservableCollection<HelpSearchResult> Results { get; } = [];
    public HelpSearchResult? SelectedResult { get; set; }
    public string CurrentTopicId => currentId;
    public string Culture => culture;
    public string Status { get => status; private set => SetProperty(ref status, value); }
    public string Title => "CryptoBook — " + LocalizationManager.GetString("Help.Title");
    public FlowDirection FlowDirection => CultureInfo.GetCultureInfo(culture).TextInfo.IsRightToLeft
        ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    public FlowDocument Article { get => article; private set => SetProperty(ref article, value); }
    public double ScrollOffset { get; set; }
    public double ArticleWidth { get => articleWidth; set => SetProperty(ref articleWidth, Math.Max(160, value)); }
    public bool IsSearching => !string.IsNullOrWhiteSpace(query);
    public bool HasNoResults => IsSearching && Results.Count == 0;
    public HelpImage? Image { get => image; private set { if (SetProperty(ref image, value)) OnPropertyChanged(nameof(IsImageOpen)); } }
    public bool IsImageOpen => Image is not null;
    public string Query
    {
        get => query;
        set
        {
            if (!SetProperty(ref query, (value ?? "")[..Math.Min(value?.Length ?? 0, 200)])) return;
            searchTimer.Stop();
            Results.Clear(); SelectedResult = null;
            OnPropertyChanged(nameof(IsSearching), nameof(HasNoResults));
            if (IsSearching) searchTimer.Start();
        }
    }
    public RelayCommand Navigate { get; }
    public RelayCommand Back { get; }
    public RelayCommand Forward { get; }
    public RelayCommand ClearSearch { get; }
    public RelayCommand ShowImage { get; }
    public RelayCommand CloseImage { get; }

    public void RunSearch()
    {
        searchTimer.Stop();
        Results.Clear();
        if (IsSearching)
            foreach (var result in content.Search(culture, Query)) Results.Add(result);
        SelectedResult = Results.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedResult), nameof(HasNoResults));
    }

    public bool OpenTopic(string id)
    {
        if (!content.IsAvailable(id))
        {
            Status = LocalizationManager.GetString(content.IsKnownTopic(id) ? "Help.Planned" : "Help.LoadError");
            return false;
        }
        if (historyIndex >= 0 && currentId == id) { Status = ""; return true; }
        SaveOffset();
        if (historyIndex + 1 < history.Count) history.RemoveRange(historyIndex + 1, history.Count - historyIndex - 1);
        history.Add((id, 0)); historyIndex = history.Count - 1;
        Display(id, 0);
        return true;
    }

    public void SaveWindowState(Rect bounds, double navigationWidth)
        => preferences.Save(new(currentId, bounds.Left, bounds.Top, bounds.Width, bounds.Height, navigationWidth));

    private void Display(string id, double offset)
    {
        currentId = id; ScrollOffset = offset; Status = ""; Image = null;
        var topic = content.Find(culture, id);
        if (topic is null)
        {
            Article = new FlowDocument(new Paragraph(new Run(LocalizationManager.GetString("Help.LoadError"))));
        }
        else
        {
            Article = new MarkdownFlowDocumentRenderer().RenderHelp(topic.Markdown,
                new(CreateLink, (path, alt, caption) => CreateImage(topic, path, alt, caption)));
            Article.PagePadding = new Thickness(20);
            Article.TextAlignment = TextAlignment.Left;
            Article.PageWidth = double.NaN;
            Article.PageHeight = double.NaN;
            Article.MinPageWidth = 0;
            Article.MaxPageWidth = double.PositiveInfinity;
            Article.ColumnWidth = double.PositiveInfinity;
        }
        foreach (var section in Sections)
            foreach (var node in section.Children)
            {
                node.IsSelected = node.Id == id;
                if (node.IsSelected) section.IsExpanded = true;
            }
        Back.RaiseCanExecuteChanged(); Forward.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(CurrentTopicId));
    }

    private Span? CreateLink(string url)
    {
        string? id = content.ResolveTopicLink(url);
        if (id is null) return null;
        if (!content.IsAvailable(id))
            return new Span { ToolTip = LocalizationManager.GetString("Help.Planned"), Foreground = SystemColors.GrayTextBrush };
        var link = new Hyperlink { Command = Navigate, CommandParameter = id };
        link.SetResourceReference(TextElement.ForegroundProperty, "CurrentAccent");
        return link;
    }

    private FrameworkElement CreateImage(HelpTopic topic, string path, string alt, string caption)
    {
        var bitmap = content.LoadImage(culture, topic, path);
        var panel = new StackPanel { Margin = new Thickness(0, 10, 0, 12), MaxWidth = bitmap?.Width ?? 900 };
        panel.SetBinding(FrameworkElement.WidthProperty, new Binding(nameof(ArticleWidth)) { Source = this });
        if (bitmap is not null)
        {
            var button = new Button
            {
                Padding = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Content = new System.Windows.Controls.Image { Source = bitmap, Stretch = Stretch.Uniform },
                Command = ShowImage, CommandParameter = new HelpImage(bitmap, caption), ToolTip = alt
            };
            AutomationProperties.SetName(button, alt);
            panel.Children.Add(button);
        }
        else panel.Children.Add(new TextBlock { Text = LocalizationManager.GetString("Help.ImageError") + " " + alt, TextWrapping = TextWrapping.Wrap });
        var text = new TextBlock { Text = caption, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0), FontSize = 13 };
        text.SetResourceReference(TextBlock.ForegroundProperty, "CurrentMutedForeground");
        panel.Children.Add(text);
        return panel;
    }

    private void SaveOffset() { if (historyIndex >= 0) history[historyIndex] = (currentId, ScrollOffset); }
    private void MoveHistory(int delta)
    {
        int index = historyIndex + delta;
        if (index < 0 || index >= history.Count) return;
        SaveOffset(); historyIndex = index;
        Display(history[index].Id, history[index].Offset);
    }
    private void SearchTick(object? sender, EventArgs e) => RunSearch();
    private void BuildTree()
    {
        var expanded = Sections.ToDictionary(s => s.Id, s => s.IsExpanded);
        Sections.Clear();
        var locale = content.GetLocale(culture);
        foreach (string section in content.SectionOrder)
        {
            var children = locale.Topics.Where(t => t.Section == section)
                .Select(t => new HelpTreeNode { Id = t.Id, Title = t.Title }).ToArray();
            if (children.Length > 0)
                Sections.Add(new HelpTreeNode { Id = section, Title = locale.Sections[section], Children = children,
                    IsExpanded = !expanded.TryGetValue(section, out bool open) || open });
        }
    }
    private void CultureChanged(object? sender, EventArgs e)
    {
        Query = ""; searchTimer.Stop();
        culture = LocalizationManager.CurrentCultureName;
        for (int i = 0; i < history.Count; i++) history[i] = (history[i].Id, 0);
        BuildTree(); Display(currentId, 0);
        OnPropertyChanged(nameof(Title), nameof(Culture), nameof(FlowDirection));
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        searchTimer.Stop(); searchTimer.Tick -= SearchTick;
        LocalizationManager.CultureChanged -= CultureChanged;
    }
}
