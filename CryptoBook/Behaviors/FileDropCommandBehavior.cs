using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Input;
using WpfDragEventArgs = System.Windows.DragEventArgs;
using WpfDragDropEffects = System.Windows.DragDropEffects;
using WpfDataFormats = System.Windows.DataFormats;

namespace CryptoBook.Behaviors;

public sealed class FileDropCommandBehavior : Behavior<UIElement>
{
    public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(
        nameof(Command), typeof(ICommand), typeof(FileDropCommandBehavior));
    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.PreviewDragOver += OnDragOver;
        AssociatedObject.PreviewDrop += OnDrop;
    }
    protected override void OnDetaching()
    {
        AssociatedObject.PreviewDragOver -= OnDragOver;
        AssociatedObject.PreviewDrop -= OnDrop;
        base.OnDetaching();
    }
    private string[]? AcceptFiles(WpfDragEventArgs e)
    {
        if(!e.Data.GetDataPresent(WpfDataFormats.FileDrop)) return null;
        e.Handled = true;
        var paths = e.Data.GetData(WpfDataFormats.FileDrop) as string[];
        bool accepted = paths is { Length: > 0 } &&
            (e.AllowedEffects & WpfDragDropEffects.Copy) != 0 && Command?.CanExecute(paths) == true;
        e.Effects = accepted ? WpfDragDropEffects.Copy : WpfDragDropEffects.None;
        return accepted ? paths : null;
    }
    private void OnDragOver(object sender, WpfDragEventArgs e) => AcceptFiles(e);
    private void OnDrop(object sender, WpfDragEventArgs e)
    {
        if(AcceptFiles(e) is { } paths) Command!.Execute(paths);
    }
}
