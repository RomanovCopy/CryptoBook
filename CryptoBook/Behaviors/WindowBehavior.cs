using Microsoft.Xaml.Behaviors;

using System.Windows;
using System.Windows.Input;

namespace CryptoBook.Behaviors
{
    public class WindowBehavior: Behavior<Window>
    {
        public static readonly DependencyProperty CloseSidePanelCommandProperty =
            DependencyProperty.Register(
                nameof(CloseSidePanelCommand),
                typeof(ICommand),
                typeof(WindowBehavior),
                new PropertyMetadata(null));

        public ICommand CloseSidePanelCommand
        {
            get => (ICommand)GetValue(CloseSidePanelCommandProperty);
            set => SetValue(CloseSidePanelCommandProperty, value);
        }

        public static readonly DependencyProperty SidePanelProperty =
            DependencyProperty.Register(
                nameof(SidePanel),
                typeof(FrameworkElement),
                typeof(WindowBehavior),
                new PropertyMetadata(null));

        public FrameworkElement SidePanel
        {
            get => (FrameworkElement)GetValue(SidePanelProperty);
            set => SetValue(SidePanelProperty, value);
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.PreviewMouseDown += OnPreviewMouseDown;
        }


        protected override void OnDetaching()
        {
            base.OnDetaching();
            AssociatedObject.PreviewMouseDown -= OnPreviewMouseDown;
        }

        private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if(SidePanel != null && CloseSidePanelCommand?.CanExecute(e) == true)
            {
                var point = e.GetPosition(SidePanel);
                if(point.X > SidePanel.ActualWidth)
                {
                    CloseSidePanelCommand.Execute(e);
                }
            }
        }
    }
}
