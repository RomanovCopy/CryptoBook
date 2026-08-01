using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace CryptoBook.Behaviors
{
    /// <summary>
    /// Добавляет ожидаемую клавиатурную навигацию по страницам и возвращает фокус
    /// просмотрщику после его появления.
    /// </summary>
    public static class DocumentPageKeyboardNavigationBehavior
    {
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled",
                typeof(bool),
                typeof(DocumentPageKeyboardNavigationBehavior),
                new PropertyMetadata(false, OnIsEnabledChanged));

        public static void SetIsEnabled(DependencyObject element, bool value) =>
            element.SetValue(IsEnabledProperty, value);

        public static bool GetIsEnabled(DependencyObject element) =>
            (bool)element.GetValue(IsEnabledProperty);

        private static void OnIsEnabledChanged(
            DependencyObject dependencyObject,
            DependencyPropertyChangedEventArgs e)
        {
            if(dependencyObject is not FlowDocumentPageViewer viewer)
                return;

            if((bool)e.OldValue)
            {
                viewer.PreviewKeyDown -= Viewer_PreviewKeyDown;
                viewer.IsVisibleChanged -= Viewer_IsVisibleChanged;
            }

            if((bool)e.NewValue)
            {
                viewer.PreviewKeyDown += Viewer_PreviewKeyDown;
                viewer.IsVisibleChanged += Viewer_IsVisibleChanged;
                FocusWhenVisible(viewer);
            }
        }

        private static void Viewer_IsVisibleChanged(
            object sender,
            DependencyPropertyChangedEventArgs e)
        {
            if(sender is FlowDocumentPageViewer viewer && e.NewValue is true)
                FocusWhenVisible(viewer);
        }

        private static void FocusWhenVisible(FlowDocumentPageViewer viewer)
        {
            if(!viewer.IsVisible)
                return;

            // Фокус запрашивается после обработки IsVisibleChanged, когда элемент
            // уже участвует в визуальном дереве и способен принять ввод.
            viewer.Dispatcher.BeginInvoke(
                DispatcherPriority.Input,
                new Action(() =>
                {
                    viewer.Focus();
                    Keyboard.Focus(viewer);
                }));
        }

        private static void Viewer_PreviewKeyDown(
            object sender,
            System.Windows.Input.KeyEventArgs e)
        {
            if(sender is not FlowDocumentPageViewer viewer)
                return;

            switch(e.Key)
            {
                case Key.Left:
                case Key.PageUp:
                    if(viewer.CanGoToPreviousPage)
                        viewer.PreviousPage();
                    e.Handled = true;
                    break;

                case Key.Right:
                case Key.PageDown:
                    if(viewer.CanGoToNextPage)
                        viewer.NextPage();
                    e.Handled = true;
                    break;

                case Key.Home:
                    viewer.FirstPage();
                    e.Handled = true;
                    break;

                case Key.End:
                    viewer.LastPage();
                    e.Handled = true;
                    break;
            }
        }
    }
}
