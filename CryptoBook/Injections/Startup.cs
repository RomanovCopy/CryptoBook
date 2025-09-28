using Autofac;

using CryptoBook.Accessors;
using CryptoBook.Composition;
using CryptoBook.Converters;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.MyControls;
using CryptoBook.MyPages;
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

            //App
            builder.RegisterInstance(app).As<System.Windows.Application>().SingleInstance();


            //Composition
            builder.RegisterType<ParagraphFactory>().As<IParagraphFactory>().SingleInstance();
            builder.RegisterType<ParagraphSession>().As<IParagraphSession>().SingleInstance();
            builder.RegisterType<DocumentSelection>().As<IDocumentSelection>().SingleInstance();
            builder.RegisterType<EditTransaction>().As<IEditTransaction>().SingleInstance();


            //ViewModels
            builder.RegisterType<HomeViewModel>().As<IHomeViewModel>().SingleInstance();
            builder.RegisterType<TitleBarViewModel>().As<ITitleBarViewModel>().SingleInstance();
            builder.RegisterType<MyFrameViewModel>().As<IMyFrameViewModel>().SingleInstance();
            builder.RegisterType<MenuFileViewModel>().As<IMenuFileViewModel>().SingleInstance();
            builder.RegisterType<SideMenuViewModel>().As<ISideMenuViewModel>().SingleInstance();
            builder.RegisterType<MenuSettingsViewModel>().As<IMenuSettingsViewModel>().SingleInstance();
            builder.RegisterType<MenuEncryptionViewModel>().As<IMenuEncryptionViewModel>().SingleInstance();
            builder.RegisterType<MenuContentViewModel>().As<IMenuContentViewModel>().SingleInstance();
            builder.RegisterType<RichtextboxViewModel>().As<IRichtextboxViewModel>().SingleInstance();
            builder.RegisterType<FontFormatBar_ViewModel>().As<IFontFormatBar_ViewModel>().SingleInstance();
            builder.RegisterType<TextFormatBarViewModel>().As<ITextFormatBarViewModel>().SingleInstance();
            builder.RegisterType<ListFormatBarViewModel>().As<IListFormatBarViewModel>().SingleInstance();
            builder.RegisterType<BookmarksViewModel>().As<IBookmarksViewModel>().SingleInstance();
            builder.RegisterType<BookmarksEditorViewModel>().As<IBookmarksEditorViewModel>().SingleInstance();
            builder.RegisterType<BookmarkEntryViewModel>().As<IBookmarkEntryViewModel>().AsSelf();

            //Converters
            builder.RegisterType<BitmapConverter>().AsSelf();
            builder.RegisterType<ColorToColorConverter>().InstancePerDependency();
            builder.RegisterType<ColumnsWidthConverter>().AsSelf();
            builder.RegisterType<SizeLocationConverter>().AsSelf();
            builder.RegisterType<FontSizeAdjustConverter>().AsSelf();
            builder.RegisterType<MediBrushSerializeConverter>().AsSelf();
            builder.RegisterType<VisibilityConverter>().AsSelf();
            builder.RegisterType<InternalSizeConverter>().AsSelf();


            //Helpers
            builder.RegisterType<EditTransaction>().As<IEditTransaction>().AsSelf();
            builder.RegisterType<DocumentSelection>().As<IDocumentSelection>().AsSelf();

            //Windows
            builder.RegisterType<MainWindowViewModel>().As<IMainWindowViewModel>().SingleInstance();
            builder.RegisterType<MainWindow>().SingleInstance();

            builder.RegisterType<ProgressViewModel>().As<IProgressViewModel>().InstancePerDependency();
            builder.RegisterType<ProgressWindow>().InstancePerDependency();

            builder.RegisterType<MyMessageBox_ViewModel>().As<IMyMessageBox_ViewModel>().InstancePerDependency();
            builder.RegisterType<MyMessageBox>().InstancePerDependency();
            builder.RegisterType<BookmarksEditor>().InstancePerDependency();


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

            //Factory
            builder.RegisterType<ParagraphFactory>().As<IParagraphFactory>().SingleInstance();


            //Accessors
            builder.RegisterType<ReflectionPropertyAccessor>().As<IPropertyAccessor>().SingleInstance();


            //Pages
            builder.RegisterType<Home>().SingleInstance();

            //Controls
            builder.RegisterType<TitleBar>().SingleInstance();
            builder.RegisterType<MyFrame>().SingleInstance();
            builder.RegisterType<SideMenu>().SingleInstance();
            builder.RegisterType<Richtextbox>().SingleInstance();
            builder.RegisterType<FontFormatBar>().SingleInstance();
            builder.RegisterType<TextFormatBar>().SingleInstance();
            builder.RegisterType<ListFormatBar>().SingleInstance();
            builder.RegisterType<BookmarksBar>().SingleInstance();

            //Contexts

            var container = builder.Build();

            return container;
        }

    }
}
