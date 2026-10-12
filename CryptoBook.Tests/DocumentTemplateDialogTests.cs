using Autofac;
using CryptoBook.DTO;
using CryptoBook.FileTemplates;
using CryptoBook.Infrastructure;
using CryptoBook.Injections;
using CryptoBook.Interfaces;
using CryptoBook.Models;
using CryptoBook.Services;
using CryptoBook.Views;

using System.IO;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Markup;
using System.Windows.Threading;
using System.Xml.Linq;

using Xunit;

namespace CryptoBook.Tests;

[CollectionDefinition("Settings UI", DisableParallelization = true)]
public sealed class SettingsUiCollection;

[Collection("Settings UI")]
public sealed class DocumentTemplateDialogTests
{
    [WpfFact]
    public async Task NewDialog_SelectsDefaultAndCreatesChosenTemplateUsingBindings()
    {
        Application app = Application.Current ?? new Application();
        ResourceDictionary previousResources = app.Resources;
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            app.Resources = CreateResources();
            var store = new TemplatePreferenceStore("notes");
            using IContainer container = new Startup().ConfigureServices(app, builder =>
            {
                builder.RegisterInstance(store).As<IDocumentTemplatePreferenceStore>();
                builder.RegisterInstance(new ImmediateProgress()).As<IProgressDialogService>();
                builder.RegisterInstance(new WpfDispatcherService(Dispatcher.CurrentDispatcher)).As<IDispatcherService>();
                builder.Register(context => new TestOpener(context.Resolve<IWorkspaceInternalFileOpenService>()))
                    .As<IWorkspaceFileOpenService>();
            });
            using ILifetimeScope scope = container.BeginLifetimeScope();
            var model = scope.Resolve<INewFileDialogModel>();
            var viewModel = scope.Resolve<INewFileDialogViewModel>();
            UserControl dialog = LoadView("NewFileDialog", viewModel);
            var selector = Assert.IsType<ComboBox>(dialog.FindName("DocumentTemplateSelector"));
            Assert.Equal("notes", Assert.IsType<DocumentTemplate>(selector.SelectedItem).Id);

            model.TargetDirectory = directory;
            model.CanWrite = true;
            model.SelectedTemplate = model.Templates.OfType<XamlPackageFileTemplate>().Single();
            model.FileName = "report.XamlPackage";
            selector.SelectedItem = viewModel.DocumentTemplates.Single(template => template.Id == "report");
            Assert.Equal("report", model.SelectedDocumentTemplate.Id);
            Assert.Equal("notes", store.Load()); // Выбор одного документа не меняет настройку.
            model.PaperSize = DocumentPaperSize.A2;
            model.IsLandscape = true;

            Render(dialog, 640, 680, "document-template-dialog.png");
            Assert.IsAssignableFrom<IAsyncCommand>(viewModel.Create);
            // WpfFact использует отдельный Dispatcher для каждого теста;
            // отсоединяем команды визуального дерева перед проверкой прикладной операции.
            dialog.DataContext = null;
            dialog.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
            await model.Execute_CreateAsync();
            Assert.Null(model.ErrorMessage);
            Assert.False(model.IsBusy);
            string filePath = Path.Combine(directory, "report.XamlPackage");
            Assert.True(File.Exists(filePath));
            IDocumentSession session = scope.Resolve<IDocumentSession>();
            Assert.Equal(filePath, session.FilePath);
            Assert.False(session.IsDirty);
            FlowDocumentAssertions(scope.Resolve<IRichTextBoxService>().Document, viewModel.SelectedDocumentTemplate);
            Assert.Equal(DocumentPageLayout.GetWidth(DocumentPaperSize.A2, true),
                scope.Resolve<IRichTextBoxService>().Document.PageWidth);
        }
        finally
        {
            app.Resources = previousResources;
            // Удаляется только созданный этим тестом каталог с уникальным именем.
            Directory.Delete(directory, recursive: true);
        }
    }

    [WpfFact]
    public void SettingsSelector_PersistsTemplateAndReflectsItInNextNewDialog()
    {
        Application app = Application.Current ?? new Application();
        ResourceDictionary previousResources = app.Resources;
        try
        {
            app.Resources = CreateResources();
            var store = new TemplatePreferenceStore("meeting");
            using IContainer container = new Startup().ConfigureServices(app,
                builder => builder.RegisterInstance(store).As<IDocumentTemplatePreferenceStore>());
            using ILifetimeScope scope = container.BeginLifetimeScope();
            var viewModel = scope.Resolve<ISettingsViewModel>();
            UserControl settings = LoadView("SettingsWindow", viewModel);
            var selector = Assert.IsType<ComboBox>(settings.FindName("DefaultDocumentTemplateSelector"));
            Assert.Equal("meeting", Assert.IsType<DocumentTemplate>(selector.SelectedItem).Id);
            selector.SelectedItem = viewModel.DocumentTemplates.First();
            Assert.Equal(DocumentTemplate.EmptyId, store.Load());
            using ILifetimeScope nextScope = container.BeginLifetimeScope();
            UserControl dialog = LoadView("NewFileDialog", nextScope.Resolve<INewFileDialogViewModel>());
            Assert.Equal(DocumentTemplate.EmptyId,
                Assert.IsAssignableFrom<INewFileDialogViewModel>(dialog.DataContext).SelectedDocumentTemplate.Id);
        }
        finally
        {
            app.Resources = previousResources;
        }
    }

    private static void FlowDocumentAssertions(System.Windows.Documents.FlowDocument document, DocumentTemplate template)
    {
        string text = new System.Windows.Documents.TextRange(document.ContentStart, document.ContentEnd).Text;
        Assert.Contains(template.DisplayName, text);
        Assert.DoesNotContain("# ", text);
    }

    [WpfTheory]
    [InlineData(StartupScreen.Start, DocumentTemplate.EmptyId)]
    [InlineData(StartupScreen.Editor, "meeting")]
    public void SettingsLanguageChange_UpdatesSelectedLabelsWithoutChangingPreferences(StartupScreen screen, string templateId)
    {
        Application app = Application.Current ?? new Application();
        ResourceDictionary previousResources = app.Resources;
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo? previousDefaultCulture = CultureInfo.DefaultThreadCurrentCulture;
        CultureInfo? previousDefaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;
        string previousSetting = CryptoBook.Properties.Settings.Default.CultureInfo;
        UserControl? settings = null;
        try
        {
            app.Resources = CreateResources();
            LocalizationManager.SelectCulture("en-US");
            var templateStore = new TemplatePreferenceStore(templateId);
            var startupStore = new StartupPreferenceStore(screen);
            using IContainer container = new Startup().ConfigureServices(app, builder =>
            {
                builder.RegisterInstance(templateStore).As<IDocumentTemplatePreferenceStore>();
                builder.RegisterInstance(startupStore).As<IStartupScreenPreferenceStore>();
            });
            using ILifetimeScope scope = container.BeginLifetimeScope();
            var viewModel = scope.Resolve<ISettingsViewModel>();
            settings = LoadView("SettingsWindow", viewModel, bindCommands: false);
            // Закрытый контекст не должен оставлять обработчики глобальной смены языка.
            using(ILifetimeScope closedScope = container.BeginLifetimeScope())
                closedScope.Resolve<ISideMenuViewModel>();
            var startup = Assert.IsType<ComboBox>(settings.FindName("StartupScreenSelector"));
            var template = Assert.IsType<ComboBox>(settings.FindName("DefaultDocumentTemplateSelector"));
            Render(settings, 760, 780, $"settings-language-initial-{templateId}.png");
            var themeSelector = Assert.Single(FindVisualChildren<ListBox>(settings),
                list => list.Items.OfType<ApplicationThemeOption>().Any());
            ApplicationThemeOption selectedTheme = viewModel.SelectedTheme;
            foreach(string culture in new[] { "en-US", "ru-RU", "de-DE", "uk-UA", "en-US", "ru-RU" })
            {
                viewModel.SelectedCultureName = culture;
                settings.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
                Render(settings, 760, 780, $"settings-language-{culture}-{templateId}.png");
                Assert.Equal(screen, Assert.IsType<StartupScreenOption>(startup.SelectedItem).Screen);
                Assert.Equal(templateId, Assert.IsType<DocumentTemplate>(template.SelectedItem).Id);
                Assert.Same(selectedTheme, themeSelector.SelectedItem);
                Assert.Equal(LocalizationManager.GetString(screen == StartupScreen.Editor ? "Editor.Editor" : "Home.StartTitle"),
                    GetSelectedLabel(startup));
                Assert.Equal(LocalizationManager.GetString($"DocumentTemplate.{templateId}.Name"), GetSelectedLabel(template));
                Assert.Equal(screen, startupStore.Load());
                Assert.Equal(templateId, templateStore.Load());
                foreach(ApplicationThemeOption theme in viewModel.Themes)
                {
                    Assert.Equal(LocalizationManager.GetString($"Theme.{theme.Theme}.Name"), theme.DisplayName);
                    Assert.Equal(LocalizationManager.GetString($"Theme.{theme.Theme}.Description"), theme.Description);
                }
            }
            Assert.Equal(0, startupStore.SaveCount);
            Assert.Equal(0, templateStore.SaveCount);
            StartupScreen nextScreen = screen == StartupScreen.Editor ? StartupScreen.Start : StartupScreen.Editor;
            string nextTemplateId = templateId == DocumentTemplate.EmptyId ? "report" : DocumentTemplate.EmptyId;
            startup.SelectedItem = viewModel.StartupScreens.Single(option => option.Screen == nextScreen);
            template.SelectedItem = viewModel.DocumentTemplates.Single(option => option.Id == nextTemplateId);
            Assert.Equal(nextScreen, startupStore.Load());
            Assert.Equal(nextTemplateId, templateStore.Load());
            viewModel.SelectedCultureName = "de-DE";
            settings.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
            Render(settings, 760, 780, $"settings-language-new-selection-{templateId}.png");
            Assert.Equal(nextScreen, Assert.IsType<StartupScreenOption>(startup.SelectedItem).Screen);
            Assert.Equal(nextTemplateId, Assert.IsType<DocumentTemplate>(template.SelectedItem).Id);
            Assert.Equal(LocalizationManager.GetString(nextScreen == StartupScreen.Editor ? "Editor.Editor" : "Home.StartTitle"),
                GetSelectedLabel(startup));
            Assert.Equal(LocalizationManager.GetString($"DocumentTemplate.{nextTemplateId}.Name"), GetSelectedLabel(template));
            Assert.Equal(1, startupStore.SaveCount);
            Assert.Equal(1, templateStore.SaveCount);
            using ILifetimeScope reopenedScope = container.BeginLifetimeScope();
            var reopened = reopenedScope.Resolve<ISettingsViewModel>();
            Assert.Equal(nextScreen, reopened.SelectedStartupScreen.Screen);
            Assert.Equal(nextTemplateId, reopened.SelectedDefaultDocumentTemplate.Id);
        }
        finally
        {
            if(settings is not null)
                settings.DataContext = null;
            app.Resources = previousResources;
            LocalizationManager.SelectCulture(previousSetting);
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
            CultureInfo.DefaultThreadCurrentCulture = previousDefaultCulture;
            CultureInfo.DefaultThreadCurrentUICulture = previousDefaultUiCulture;
        }
    }

    private static string GetSelectedLabel(ComboBox selector)
    {
        var toggle = Assert.IsType<System.Windows.Controls.Primitives.ToggleButton>(
            selector.Template.FindName("ToggleButton", selector));
        return string.Join("", FindVisualChildren<TextBlock>(toggle).Select(text => text.Text));
    }

    private static UserControl LoadView(string name, object viewModel, bool bindCommands = true)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while(directory is not null && !File.Exists(Path.Combine(directory.FullName, "CryptoBook", "Views", name + ".xaml")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        XElement source = XDocument.Load(Path.Combine(directory.FullName, "CryptoBook", "Views", name + ".xaml")).Root!;
        XNamespace ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        // Используем настоящую разметку содержимого и ресурсов окна, не создавая
        // системное окно или второй Application на отдельных потоках WpfFact.
        var root = new XElement(ns + "UserControl", source.Attributes().Where(attribute => attribute.IsNamespaceDeclaration));
        root.Add(new XElement(ns + "UserControl.Resources", source.Element(ns + "Window.Resources")!.Nodes()));
        root.Add(source.Element(ns + "Grid")!);
        XNamespace behaviors = "http://schemas.microsoft.com/xaml/behaviors";
        XNamespace runtimeBehaviors = "clr-namespace:Microsoft.Xaml.Behaviors;assembly=Microsoft.Xaml.Behaviors";
        root.SetAttributeValue(XNamespace.Xmlns + "i", runtimeBehaviors.NamespaceName);
        foreach(XElement element in root.Descendants().Where(element => element.Name.Namespace == behaviors))
            element.Name = runtimeBehaviors + element.Name.LocalName;
        // Команды других разделов требуют единственного Application.Dispatcher;
        // для проверки переводов на потоках WpfTheory они не нужны.
        if(!bindCommands)
            foreach(XAttribute command in root.Descendants().Attributes().Where(attribute => attribute.Name.LocalName == "Command").ToArray())
                command.Remove();
        var view = (UserControl)XamlReader.Parse(root.ToString());
        view.DataContext = viewModel;
        view.Background = System.Windows.Media.Brushes.White;
        view.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
        foreach(ComboBox selector in FindVisualChildren<ComboBox>(view))
        {
            selector.GetBindingExpression(ItemsControl.ItemsSourceProperty)?.UpdateTarget();
            selector.GetBindingExpression(System.Windows.Controls.Primitives.Selector.SelectedItemProperty)?.UpdateTarget();
        }
        return view;
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T: DependencyObject
    {
        for(int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            if(child is T matched)
                yield return matched;
            foreach(T descendant in FindVisualChildren<T>(child))
                yield return descendant;
        }
    }

    private static ResourceDictionary CreateResources()
    {
        var resources = new ResourceDictionary { ["Res"] = new ResourceWrapper() };
        foreach(string source in new[] { "Themes/LightTheme.xaml", "Styles/WindowStyles.xaml",
            "Styles/ControlStyles.xaml", "Styles/ImageStyles.xaml", "Styles/ListStyles.xaml",
            "Styles/ButtonStyles.xaml", "Styles/EditorToolbarIcons.xaml", "Styles/TreeViewStyles.xaml",
            "Styles/ComboBoxStyles.xaml", "Styles/ThemedControls.xaml" })
            resources.MergedDictionaries.Add(new ResourceDictionary
            { Source = new Uri($"/CryptoBook;component/{source}", UriKind.Relative) });
        return resources;
    }

    private static void Render(FrameworkElement element, int width, int height, string name)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
        Assert.True(element.ActualHeight > 0);
        if(Environment.GetEnvironmentVariable("CRYPTOBOOK_UI_QA_DIR") is not { Length: > 0 } output)
            return;
        Directory.CreateDirectory(output);
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(output, name));
        encoder.Save(stream);
    }

    private sealed class TemplatePreferenceStore(string id): IDocumentTemplatePreferenceStore
    {
        private string selectedId = id;
        public string Load() => selectedId;
        public int SaveCount { get; private set; }
        public void Save(string templateId) { selectedId = templateId; SaveCount++; }
    }

    private sealed class StartupPreferenceStore(StartupScreen screen): IStartupScreenPreferenceStore
    {
        private StartupScreen selectedScreen = screen;
        public int SaveCount { get; private set; }
        public StartupScreen Load() => selectedScreen;
        public void Save(StartupScreen value) { selectedScreen = value; SaveCount++; }
    }

    private sealed class ImmediateProgress: IProgressDialogService, IProgressReporter
    {
        public Task<T> RunAsync<T>(string operationName, Func<IProgressReporter, CancellationToken, Task<T>> operation) =>
            operation(this, CancellationToken.None);
        public void Report(double? value, string? currentInfo = null) { }
    }

    private sealed class TestOpener(IWorkspaceInternalFileOpenService opener): IWorkspaceFileOpenService
    {
        public async Task<WorkspaceFileOpenResult> OpenAsync(string filePath, CancellationToken cancellationToken = default)
        {
            string nativePath = filePath.StartsWith("local://") ? filePath[8..] : filePath;
            await opener.OpenDocumentAsync(nativePath, nativePath, new XamlPackageFileTemplate(), false, cancellationToken);
            return WorkspaceFileOpenResult.InternalSuccess();
        }
    }
}
