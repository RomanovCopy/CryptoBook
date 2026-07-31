using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Navigation;

namespace CryptoBook.Behaviors
{
    public static class HyperlinkCommandBehavior
    {
        private static readonly RequestNavigateEventHandler NavigateHandlerDelegate =
            NavigateHandler;

        public static readonly DependencyProperty CommandProperty =
            DependencyProperty.RegisterAttached(
                "Command",
                typeof(ICommand),
                typeof(HyperlinkCommandBehavior),
                new PropertyMetadata(null, OnCommandChanged));

        public static void SetCommand(DependencyObject element, ICommand? value) =>
            element.SetValue(CommandProperty, value);

        public static ICommand? GetCommand(DependencyObject element) =>
            (ICommand?)element.GetValue(CommandProperty);

        private static void OnCommandChanged(
            DependencyObject dependencyObject,
            DependencyPropertyChangedEventArgs e)
        {
            if(dependencyObject is not FlowDocumentPageViewer viewer)
                return;

            if(e.OldValue != null)
                viewer.RemoveHandler(
                    Hyperlink.RequestNavigateEvent,
                    NavigateHandlerDelegate);

            if(e.NewValue != null)
                viewer.AddHandler(
                    Hyperlink.RequestNavigateEvent,
                    NavigateHandlerDelegate);
        }

        private static void NavigateHandler(object sender, RequestNavigateEventArgs e)
        {
            if(sender is not DependencyObject source || e.Uri == null)
                return;

            var command = GetCommand(source);
            if(command?.CanExecute(e.Uri) != true)
                return;

            command.Execute(e.Uri);
            e.Handled = true;
        }
    }
}
