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
using CryptoBook.Security;
using CryptoBook.Services;
using CryptoBook.ViewModels;
using CryptoBook.Views;


namespace CryptoBook.Injections
{
    public class Startup
    {
        public IContainer ConfigureServices(System.Windows.Application app)
        {

            ContainerBuilder builder = new();
            var dispatcher = app.Dispatcher;

            //App
            builder.RegisterInstance(app).As<System.Windows.Application>().SingleInstance();
            builder.RegisterInstance(dispatcher);



            //Composition
            builder.RegisterType<ParagraphFactory>().As<IParagraphFactory>().SingleInstance();
            builder.RegisterType<ParagraphSession>().As<IParagraphSession>().SingleInstance();
            builder.RegisterType<DocumentSelection>().As<IDocumentSelection>().AsSelf().SingleInstance();
            builder.RegisterType<EditTransaction>().As<IEditTransaction>().AsSelf().SingleInstance();


            //Models
            builder.RegisterType<MenuFileModel>().As<IMenuFileModel>().InstancePerLifetimeScope();
            builder.RegisterType<NewFileDialogModel>().As<INewFileDialogModel>().InstancePerLifetimeScope();
            builder.RegisterType<FileExplorerModel>().As<IFileExplorerModel>().InstancePerLifetimeScope();
            builder.RegisterType<TitleBarModel>().As<ITitleBarModel>().InstancePerLifetimeScope();
            builder.RegisterType<MyFrameModel>().As<IMyFrameModel>().InstancePerLifetimeScope();
            builder.RegisterType<MainWindowModel>().As<IMainWindowModel>().InstancePerLifetimeScope();
            builder.RegisterType<MessageWindowModel>().As<IMessageWindowModel>().InstancePerLifetimeScope();
            builder.RegisterType<KeyInputModel>().As<IKeyInputModel>().InstancePerLifetimeScope();
            builder.RegisterType<EncryptionMode_Model>().As<IEncryptionMode_Model>().InstancePerLifetimeScope();
            builder.RegisterType<MediaPlayerModel>().As<IMediaPlayerModel>().InstancePerLifetimeScope();
            builder.RegisterType<BookmarksModel>().As<IBookmarksModel>().InstancePerLifetimeScope();
            builder.RegisterType<BookmarksEditorModel>().As<IBookmarksEditorModel>().InstancePerLifetimeScope();
            builder.RegisterType<RichtextboxModel>().As<IRichtextboxModel>().InstancePerLifetimeScope();
            builder.RegisterType<SettingsModel>().As<ISettingsModel>().InstancePerLifetimeScope();

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
            builder.RegisterType<RichTextContextMenuViewModel>().As<IRichTextContextMenuViewModel>().InstancePerLifetimeScope();
            builder.RegisterType<FontFormatBar_ViewModel>().As<IFontFormatBar_ViewModel>().InstancePerLifetimeScope();
            builder.RegisterType<TextFormatBarViewModel>().As<ITextFormatBarViewModel>().InstancePerLifetimeScope();
            builder.RegisterType<ListFormatBarViewModel>().As<IListFormatBarViewModel>().InstancePerLifetimeScope();
            builder.RegisterType<BookmarksViewModel>().As<IBookmarksViewModel>().InstancePerLifetimeScope();
            builder.RegisterType<BookmarksEditorViewModel>().As<IBookmarksEditorViewModel>().InstancePerLifetimeScope();
            builder.RegisterType<BookmarkEntryViewModel>().As<IBookmarkEntryViewModel>().AsSelf();
            builder.RegisterType<NewFileDialogViewModel>().As<INewFileDialogViewModel>().InstancePerDependency();
            builder.RegisterType<FileExplorerViewModel>().As<IFileExplorerViewModel>().InstancePerLifetimeScope(); 
            builder.RegisterType<FavoriteDirectoriesViewModel>().As<IFavoriteDirectoriesViewModel>().InstancePerLifetimeScope();
            builder.RegisterType<FilePreviewViewModel>().As<IFilePreviewViewModel>().InstancePerLifetimeScope();
            builder.RegisterType<TextInputDialogViewModel>().InstancePerDependency();
            builder.RegisterType<MyMessageBox_ViewModel>().As<IMyMessageBox_ViewModel>().InstancePerDependency();
            builder.RegisterType<MessageWindowViewModel>().As<IMessageWindowViewModel>().InstancePerLifetimeScope(); 
            builder.RegisterType<KeyInputViewModel>().As<IKeyInputViewModel>().InstancePerLifetimeScope();
            builder.RegisterType<EncryptionMode_ViewModel>().As<IEncryptionMode_ViewModel>().InstancePerLifetimeScope();
            builder.RegisterType<MediaPlayerViewModel>().As<IMediaPlayerViewModel>().InstancePerLifetimeScope();
            builder.RegisterType<SettingsViewModel>().As<ISettingsViewModel>().InstancePerLifetimeScope();

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
            builder.RegisterType<TypeCheckConverter>().AsSelf();
            builder.RegisterType<SecureStringConverter>().As<ISecureStringConverter>().SingleInstance();
            builder.RegisterType<EnumToBoolConverter>().AsSelf();


            //Helpers
            builder.RegisterType<FlowDocumentWalker>().As<IFlowDocumentWalker>().SingleInstance();
            builder.RegisterType<SecureFileValidator>().As<ISecureFileValidator>().SingleInstance();
            builder.RegisterType<SecureFileProcessor>().As<ISecureFileProcessor>().SingleInstance();
            builder.RegisterType<Argon2idKeyDeriver>().As<IPasswordKeyDeriver>().SingleInstance();
            builder.RegisterType<SecureFileV2Codec>().As<ISecureFileV2Codec>().SingleInstance();
            builder.RegisterType<LegacySecureFileCodec>().As<ILegacySecureFileCodec>().SingleInstance();

            //Windows
            builder.RegisterType<MainWindowViewModel>().As<IMainWindowViewModel>().InstancePerLifetimeScope();
            builder.RegisterType<MainWindow>().InstancePerLifetimeScope();
            builder.RegisterType<EncryptionModeWindow>().InstancePerDependency();

            builder.RegisterType<ProgressViewModel>().As<IProgressViewModel>().InstancePerDependency();
            builder.RegisterType<ProgressWindow>().InstancePerDependency();

            builder.RegisterType<MyMessageBox>().InstancePerDependency();
            builder.RegisterType<BookmarksEditor>().InstancePerDependency();
            builder.RegisterType<NewFileDialog>().InstancePerDependency();
            builder.RegisterType<FileExplorer>().InstancePerDependency();
            builder.RegisterType<MessageWindow>().InstancePerDependency();
            builder.RegisterType<KeyInputWindow>().InstancePerDependency();
            builder.RegisterType<DirectoryNameDialog>().InstancePerDependency();
            builder.RegisterType<TextInputDialog>().InstancePerDependency();
            builder.RegisterType<HyperlinkDialog>().InstancePerDependency();
            builder.RegisterType<MediaPlayer>().InstancePerDependency();
            builder.RegisterType<SettingsWindow>().InstancePerDependency();
                                                                                                         
            //FileTemplate
            builder.RegisterType<PlainTextTemplate>().As<IFileTemplate>().SingleInstance();
            builder.RegisterType<ImageFileTemplate>().As<IFileTemplate>().SingleInstance();
            builder.RegisterType<VideoFileTemplate>().As<IFileTemplate>().SingleInstance();
            builder.RegisterType<PdfFileTemplate>().As<IFileTemplate>().SingleInstance();
            builder.RegisterType<SecureFileTemplate>().As<IFileTemplate>().SingleInstance();
            builder.RegisterType<XamlPackageFileTemplate>().As<IFileTemplate>().SingleInstance();
            builder.RegisterType<RichTextFileTemplate>().As<IFileTemplate>().SingleInstance();
            builder.RegisterType<XamlFileTemplate>().As<IFileTemplate>().SingleInstance();


            // реестр собирает их автоматически
            builder.RegisterType<FileTemplateRegistry>().As<IFileTemplateRegistry>().SingleInstance();

            // Форматы FlowDocument
            builder.RegisterType<RtfDocumentFormatHandler>()
                .As<IDocumentFormatHandler>()
                .SingleInstance();
            builder.RegisterType<XamlPackageDocumentFormatHandler>()
                .As<IDocumentFormatHandler>()
                .SingleInstance();
            builder.RegisterType<PlainTextDocumentFormatHandler>()
                .As<IDocumentFormatHandler>()
                .SingleInstance();
            builder.RegisterType<XamlTextDocumentFormatHandler>()
                .As<IDocumentFormatHandler>()
                .SingleInstance();
            builder.RegisterType<DocumentFormatHandlerRegistry>()
                .As<IDocumentFormatHandlerRegistry>()
                .SingleInstance();


            //DTOs
            builder.RegisterType<DirectoryItem>().As<IDirectoryItem>().InstancePerDependency();
            builder.RegisterType<FileItem>().As<IFileItem>().InstancePerDependency();
            builder.RegisterType<DriveItem>().As<IDriveItem>().InstancePerDependency();


            //Services
            builder.RegisterType<WindowManager>().As<IWindowManager>().SingleInstance();
            builder.RegisterType<ThemeManager>().As<IThemeManager>().SingleInstance();
            builder.RegisterType<UserThemePreferenceStore>().As<IThemePreferenceStore>().SingleInstance();
            builder.RegisterType<UserDocumentBackgroundPreferenceStore>()
                .As<IDocumentBackgroundPreferenceStore>()
                .SingleInstance();
            builder.RegisterType<WindowsThemeProvider>().As<IWindowsThemeProvider>().SingleInstance();
            builder.RegisterType<SettingsWindowService>().As<ISettingsWindowService>().SingleInstance();
            builder.RegisterType<WorkspaceService>().As<IWorkspaceService>().SingleInstance();
            builder.RegisterType<RichTextBoxService>().As<IRichTextBoxService>().SingleInstance();
            builder.RegisterType<FontService>().As<IFontService>().SingleInstance();
            builder.RegisterType<TextFormatService>().As<ITextFormatService>().SingleInstance();
            builder.RegisterType<ParagraphService>().As<IParagraphService>().InstancePerDependency();
            builder.RegisterType<InlineService>().As<IInlineService>().SingleInstance();
            builder.RegisterType<ListService>().As<IListService>().SingleInstance();
            builder.RegisterType<BookmarksService>().As<IBookmarkService>().SingleInstance();
            builder.RegisterType<BookmarkValidationService>().As<IBookmarkValidationService>().SingleInstance();
            builder.RegisterType<FileClipboardService>().As<IFileClipboardService>().SingleInstance();
            builder.RegisterType<FileManagerService>().As<IFileManagerService>().SingleInstance();
            builder.RegisterType<FilePreviewService>().As<IFilePreviewService>().SingleInstance();
            builder.RegisterType<FilePreviewContentSource>().As<IFilePreviewContentSource>().SingleInstance();
            builder.RegisterType<JsonFavoriteDirectoryStore>().As<IFavoriteDirectoryStore>().SingleInstance();
            builder.RegisterType<FavoriteDirectoryPathPolicy>().As<IFavoriteDirectoryPathPolicy>().SingleInstance();
            builder.RegisterType<FavoriteDirectoryService>().As<IFavoriteDirectoryService>().SingleInstance();
            builder.RegisterType<TextInputService>().As<ITextInputService>().SingleInstance();
            builder.RegisterType<FileProviderService>().As<IFileProviderService>().SingleInstance();
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
            builder.RegisterType<FileSecurityService>().As<IFileSecurityService>().SingleInstance();
            builder.RegisterType<DirectoryMonitoringService>().As<IDirectoryMonitoringService>().SingleInstance();
            builder.RegisterType<StockIconService>().As<IStockIconService>().SingleInstance();
            builder.RegisterType<PageNavigationService>().As<IPageNavigationService>().InstancePerLifetimeScope();
            builder.RegisterType<WpfDispatcherService>().As<IDispatcherService>().SingleInstance();
            builder.RegisterType<MessageService>().As<IMessageService>().SingleInstance();
            builder.RegisterType<ProgressDialogService>().As<IProgressDialogService>().SingleInstance();
            builder.RegisterType<SystemItemSortService>().As<ISystemItemSortService>().SingleInstance();
            builder.RegisterType<FlowDocumentContentService>().As<IFlowDocumentContentService>().InstancePerDependency();
            builder.RegisterType<FlowDocumentLoadService>().As<IFlowDocumentLoadService>().InstancePerDependency();
            builder.RegisterType<DocumentPreviewService>().As<IDocumentPreviewService>().InstancePerDependency();
            builder.RegisterType<UriNavigationService>().As<IUriNavigationService>().SingleInstance();
            builder.RegisterType<FlowDocumentSaveService>().As<IFlowDocumentSaveService>().InstancePerDependency();
            builder.RegisterType<FileDisplayNameService>()
                .As<IFileDisplayNameService>()
                .SingleInstance();
            builder.RegisterType<DocumentTitleProvider>()
                .As<IDocumentTitleProvider>()
                .InstancePerLifetimeScope();
            builder.RegisterType<DocumentSession>()
                .As<IDocumentSession>()
                .SingleInstance();
            builder.RegisterType<DocumentSaveTargetPicker>()
                .As<IDocumentSaveTargetPicker>()
                .SingleInstance();
            builder.RegisterType<ImageFilePickerService>().As<IImageFilePicker>().SingleInstance();
            builder.RegisterType<ImageContentLoader>().As<IImageContentLoader>().SingleInstance();
            builder.RegisterType<EmbeddedImageEditor>().As<IEmbeddedImageEditor>().SingleInstance();
            builder.RegisterType<EmbeddedImageLayoutService>()
                .As<IEmbeddedImageLayoutService>()
                .SingleInstance();
            builder.RegisterType<RichTextBoxDocumentLayoutMetrics>().As<IDocumentLayoutMetrics>().SingleInstance();
            builder.RegisterType<DocumentImageInserter>().As<IDocumentImageInserter>().SingleInstance();
            builder.RegisterType<ImageService>().As<IImageService>().InstancePerDependency();
            builder.RegisterType<MediaPlayerService>().As<IMediaPlayerService>().InstancePerDependency();


            //Providers
            builder.RegisterInstance(new SecureFileV2Options()).SingleInstance();
            builder.RegisterType<MemoryKeyProvider>().As<IKeyProvider>().SingleInstance();

            //Accessors
            builder.RegisterType<ReflectionPropertyAccessor>().As<IPropertyAccessor>().SingleInstance();


            //Pages
            builder.RegisterType<Home>().InstancePerLifetimeScope();
            builder.RegisterType<PageRegistry>().As<IPageRegistry>().SingleInstance();

            //Controls
            builder.RegisterType<TitleBar>().InstancePerLifetimeScope();
            builder.RegisterType<MyFrame>().InstancePerLifetimeScope();
            builder.RegisterType<SideMenu>().InstancePerLifetimeScope();
            builder.RegisterType<Richtextbox>().InstancePerLifetimeScope();
            builder.RegisterType<RichTextEditorContextMenu>().AsSelf().InstancePerLifetimeScope();
            builder.RegisterType<FontFormatBar>().InstancePerLifetimeScope();
            builder.RegisterType<TextFormatBar>().InstancePerLifetimeScope();
            builder.RegisterType<ListFormatBar>().InstancePerLifetimeScope();
            builder.RegisterType<BookmarksBar>().InstancePerLifetimeScope();
            builder.RegisterType<ImageViewer>().InstancePerDependency();










            //Contexts

            var container = builder.Build();

            return container;
        }

    }
}
