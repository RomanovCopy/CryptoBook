using Autofac;

using CryptoBook.Accessors;
using CryptoBook.Composition;
using CryptoBook.Converters;
using CryptoBook.DTO;
using CryptoBook.FileTemplates;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Models;
using CryptoBook.MyControls;
using CryptoBook.MyPages;
using CryptoBook.Services;
using CryptoBook.ViewModels;
using CryptoBook.Views;

using TimeWebAI.ViewModels;

namespace CryptoBook.Injections
{
    public class Startup
    {
        public IContainer ConfigureServices(System.Windows.Application app)
        {

            ContainerBuilder builder = new();
            var dispatcher = System.Windows.Application.Current.Dispatcher;

            //App
            builder.RegisterInstance(app).As<System.Windows.Application>().SingleInstance();
            builder.RegisterInstance(dispatcher);



            //Composition
            builder.RegisterType<ParagraphFactory>().As<IParagraphFactory>().SingleInstance();
            builder.RegisterType<ParagraphSession>().As<IParagraphSession>().SingleInstance();
            builder.RegisterType<DocumentSelection>().As<IDocumentSelection>().SingleInstance();
            builder.RegisterType<EditTransaction>().As<IEditTransaction>().SingleInstance();


            //Models
            builder.RegisterType<TimeWebAIControlModel>().As<ITimeWebAIControlModel>().InstancePerLifetimeScope();
            builder.RegisterType<TimeWebAIPageModel>().As<ITimeWebAIPageModel>().InstancePerLifetimeScope();
            builder.RegisterType<TitleBarTimeWebAIControlModel>().As<ITitleBarTimeWebAIControlModel>().InstancePerLifetimeScope();
            builder.RegisterType<MenuFileModel>().As<IMenuFileModel>().InstancePerLifetimeScope();
            builder.RegisterType<NewFileDialogModel>().As<INewFileDialogModel>().InstancePerLifetimeScope();
            builder.RegisterType<FileExplorerModel>().As<IFileExplorerModel>().InstancePerLifetimeScope();
            builder.RegisterType<TitleBarModel>().As<ITitleBarModel>().InstancePerLifetimeScope();
            builder.RegisterType<MyFrameModel>().As<IMyFrameModel>().InstancePerLifetimeScope();
            builder.RegisterType<MainWindowModel>().As<IMainWindowModel>().InstancePerLifetimeScope();
            builder.RegisterType<MessageWindowModel>().As<IMessageWindowModel>().InstancePerLifetimeScope();

            //ViewModels
            builder.RegisterType<HomeViewModel>().As<IHomeViewModel>().InstancePerLifetimeScope();
            builder.RegisterType<TitleBarViewModel>().As<ITitleBarViewModel>().InstancePerLifetimeScope();
            builder.RegisterType<MyFrameViewModel>().As<IMyFrameViewModel>().InstancePerLifetimeScope();
            builder.RegisterType<MenuFileViewModel>().As<IMenuFileViewModel>().InstancePerLifetimeScope();
            builder.RegisterType<SideMenuViewModel>().As<ISideMenuViewModel>().InstancePerLifetimeScope();
            builder.RegisterType<MenuSettingsViewModel>().As<IMenuSettingsViewModel>().InstancePerLifetimeScope();
            builder.RegisterType<MenuEncryptionViewModel>().As<IMenuEncryptionViewModel>().InstancePerLifetimeScope();
            builder.RegisterType<MenuContentViewModel>().As<IMenuContentViewModel>().InstancePerLifetimeScope();
            builder.RegisterType<RichtextboxViewModel>().As<IRichtextboxViewModel>().InstancePerLifetimeScope();
            builder.RegisterType<FontFormatBar_ViewModel>().As<IFontFormatBar_ViewModel>().InstancePerLifetimeScope();
            builder.RegisterType<TextFormatBarViewModel>().As<ITextFormatBarViewModel>().InstancePerLifetimeScope();
            builder.RegisterType<ListFormatBarViewModel>().As<IListFormatBarViewModel>().InstancePerLifetimeScope();
            builder.RegisterType<BookmarksViewModel>().As<IBookmarksViewModel>().InstancePerLifetimeScope();
            builder.RegisterType<BookmarksEditorViewModel>().As<IBookmarksEditorViewModel>().InstancePerLifetimeScope();
            builder.RegisterType<BookmarkEntryViewModel>().As<IBookmarkEntryViewModel>().AsSelf();
            builder.RegisterType<TimeWebAIControlViewModel>().As<ITimeWebAIControlViewModel>().InstancePerLifetimeScope();
            builder.RegisterType<TimeWebAIPageViewModel>().As<ITimeWebAIPageViewModel>().InstancePerLifetimeScope();
            builder.RegisterType<TitleBarTimeWebAIControlViewModel>().As<ITitleBarTimeWebAIControlViewModel>().InstancePerLifetimeScope();
            builder.RegisterType<NewFileDialogViewModel>().As<INewFileDialogViewModel>().InstancePerDependency();
            builder.RegisterType<FileExplorerViewModel>().As<IFileExplorerViewModel>().InstancePerLifetimeScope(); 
            builder.RegisterType<MessageWindowViewModel>().As<IMessageWindowViewModel>().InstancePerLifetimeScope();

            //Converters
            builder.RegisterType<BitmapConverter>().AsSelf();
            builder.RegisterType<ColorToColorConverter>().InstancePerDependency();
            builder.RegisterType<SizeLocationConverter>().AsSelf();
            builder.RegisterType<FontSizeAdjustConverter>().AsSelf();
            builder.RegisterType<MediBrushSerializeConverter>().AsSelf();
            builder.RegisterType<VisibilityConverter>().AsSelf();
            builder.RegisterType<InternalSizeConverter>().AsSelf();
            builder.RegisterType<BytesToGbConverter>().AsSelf();
            builder.RegisterType<ExtensionToIconConverter>().AsSelf();
            builder.RegisterType<PathToIconConverter>().AsSelf();
            builder.RegisterType<PercentToGridLengthConverter>().AsSelf();
            builder.RegisterType<StockIconIdToImageSourceConverter>().AsSelf();

            //Helpers
            builder.RegisterType<EditTransaction>().As<IEditTransaction>().AsSelf();
            builder.RegisterType<DocumentSelection>().As<IDocumentSelection>().AsSelf();

            //Windows
            builder.RegisterType<MainWindowViewModel>().As<IMainWindowViewModel>().InstancePerLifetimeScope();
            builder.RegisterType<MainWindow>().SingleInstance();

            builder.RegisterType<ProgressViewModel>().As<IProgressViewModel>().InstancePerDependency();
            builder.RegisterType<ProgressWindow>().InstancePerDependency();

            builder.RegisterType<MyMessageBox_ViewModel>().As<IMyMessageBox_ViewModel>().InstancePerDependency();
            builder.RegisterType<MyMessageBox>().InstancePerDependency();
            builder.RegisterType<BookmarksEditor>().InstancePerDependency();
            builder.RegisterType<NewFileDialog>().InstancePerDependency();
            builder.RegisterType<FileExplorer>().InstancePerDependency();
            builder.RegisterType<MessageWindow>().InstancePerDependency();
                                                                                                         
            //FileTemplate
            builder.RegisterType<TextFileTemplate>().As<IFileTemplate>().SingleInstance();
            builder.RegisterType<MarkdownFileTemplate>().As<IFileTemplate>().SingleInstance();
            builder.RegisterType<JsonFileTemplate>().As<IFileTemplate>().SingleInstance();
            builder.RegisterType<RichTextFileTemplate>().As<IFileTemplate>().SingleInstance();
            builder.RegisterType<SourceCodeTemplate>().As<IFileTemplate>().SingleInstance();

            // реестр собирает их автоматически
            builder.RegisterType<FileTemplateRegistry>().As<IFileTemplateRegistry>().SingleInstance();


            //DTOs
            builder.RegisterType<DirectoryItem>().As<IDirectoryItem>().InstancePerDependency();
            builder.RegisterType<FileItem>().As<IFileItem>().InstancePerDependency();
            builder.RegisterType<DriveItem>().As<IDriveItem>().InstancePerDependency();


            //Services
            builder.RegisterType<WindowManager>().As<IWindowManager>().SingleInstance();
            builder.RegisterType<ThemeManager>().As<IThemeManager>().SingleInstance();
            builder.RegisterType<RichTextBoxService>().As<IRichTextBoxService>().SingleInstance();
            builder.RegisterType<FontService>().As<IFontService>().SingleInstance();
            builder.RegisterType<TextFormatService>().As<ITextFormatService>().SingleInstance();
            builder.RegisterType<ParagraphService>().As<IParagraphService>().SingleInstance();
            builder.RegisterType<InlineService>().As<IInlineService>().SingleInstance();
            builder.RegisterType<ListService>().As<IListService>().SingleInstance();
            builder.RegisterType<BookmarksService>().As<IBookmarkService>().SingleInstance();
            builder.RegisterType<BookmarkValidationService>().As<IBookmarkValidationService>().SingleInstance();
            builder.RegisterType<WebViewService>().As<IWebViewService>().SingleInstance();
            builder.RegisterType<FileClipboardService>().As<IFileClipboardService>().SingleInstance();
            builder.RegisterType<FileManagerService>().As<IFileManagerService>().SingleInstance();
            builder.RegisterType<FileProviderService>().As<IFileProviderService>().SingleInstance();
            builder.RegisterType<CommandService>().As<ICommandService>().SingleInstance();
            builder.RegisterType<CommandService>().As<ICommandService>().SingleInstance();
            builder.RegisterType<FileCreationService>().As<IFileCreationService>().SingleInstance();
            builder.RegisterType<FolderPickerService>().As<IFolderPickerService>().SingleInstance();
            builder.RegisterType<FilePickerService>().As<IFilePickerService>().SingleInstance();
            builder.RegisterType<DriveMonitoringService>().As<IDriveMonitoringService>().SingleInstance();
            builder.RegisterType<DriveManagerService>().As<IDriveManagerService>().SingleInstance();
            builder.RegisterType<SystemItemCreateService>().As<ISystemItemCreateService>().SingleInstance();
            builder.RegisterType<SystemIconService>().As<ISystemIconService>().SingleInstance();
            builder.RegisterType<ColumnLayoutStoreService>().As<IColumnLayoutStore>().SingleInstance();
            builder.RegisterType<FileLauncherService>().As<IFileLauncherService>().SingleInstance();
            builder.RegisterType<DirectoryMonitoringService>().As<IDirectoryMonitoringService>().SingleInstance();
            builder.RegisterType<StockIconService>().As<IStockIconService>().SingleInstance();
            builder.RegisterType<PageNavigationService>().As<IPageNavigationService>().SingleInstance();
            builder.RegisterType<WpfDispatcherService>().As<IDispatcherService>().SingleInstance();
            builder.RegisterType<MessageService>().As<IMessageService>().SingleInstance();
            builder.RegisterType<SystemItemSortService>().As<ISystemItemSortService>().SingleInstance();


            //Factory
            builder.RegisterType<ParagraphFactory>().As<IParagraphFactory>().SingleInstance();


            //Accessors
            builder.RegisterType<ReflectionPropertyAccessor>().As<IPropertyAccessor>().SingleInstance();


            //Pages
            builder.RegisterType<Home>().SingleInstance();
            builder.RegisterType<TimeWebAIPage>().SingleInstance();

            builder.RegisterType<PageRegistry>().As<IPageRegistry>().SingleInstance();

            //Controls
            builder.RegisterType<TitleBar>().SingleInstance();
            builder.RegisterType<MyFrame>().SingleInstance();
            builder.RegisterType<SideMenu>().SingleInstance();
            builder.RegisterType<Richtextbox>().SingleInstance();
            builder.RegisterType<FontFormatBar>().SingleInstance();
            builder.RegisterType<TextFormatBar>().SingleInstance();
            builder.RegisterType<ListFormatBar>().SingleInstance();
            builder.RegisterType<BookmarksBar>().SingleInstance();
            builder.RegisterType<TimeWebAIControl>().SingleInstance();
            builder.RegisterType<TitleBarTimeWebAIControl>().SingleInstance();

            //Contexts

            var container = builder.Build();

            return container;
        }

    }
}
