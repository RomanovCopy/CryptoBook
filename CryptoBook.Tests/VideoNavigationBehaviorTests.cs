using CryptoBook.Behaviors;
using CryptoBook.Interfaces;

using System.Reflection;
using System.Windows.Input;

using Xunit;

namespace CryptoBook.Tests;

public sealed class VideoNavigationBehaviorTests
{
    [WpfFact]
    public void Shortcuts_RouteSeekAndClipNavigationToCryptoBookViewModel()
    {
        IMediaPlayerService player =
            DispatchProxy.Create<IMediaPlayerService, PlayerProxy>();
        var playerProxy = (PlayerProxy)(object)player;
        IMediaPlayerViewModel viewModel =
            DispatchProxy.Create<IMediaPlayerViewModel, ViewModelProxy>();
        var viewModelProxy = (ViewModelProxy)(object)viewModel;
        viewModelProxy.Player = player;

        var behavior = new VideoNavigationBehavior { ViewModel = viewModel };

        Assert.True(behavior.HandleShortcut(Key.Right, ModifierKeys.None));
        Assert.True(behavior.HandleShortcut(Key.Left, ModifierKeys.None));
        Assert.True(behavior.HandleShortcut(Key.Right, ModifierKeys.Alt));
        Assert.True(behavior.HandleShortcut(Key.Left, ModifierKeys.Alt));
        Assert.False(behavior.HandleShortcut(Key.Right, ModifierKeys.Control));

        Assert.Equal(1, playerProxy.ForwardCount);
        Assert.Equal(1, playerProxy.BackwardCount);
        Assert.Equal(1, viewModelProxy.NextCount);
        Assert.Equal(1, viewModelProxy.PreviousCount);
    }

    public class PlayerProxy: DispatchProxy
    {
        public int ForwardCount { get; private set; }
        public int BackwardCount { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                "get_IsMediaLoaded" => true,
                "FrameForward" => CountForward(),
                "FrameBackward" => CountBackward(),
                _ => DefaultValue(targetMethod?.ReturnType)
            };
        }

        private object? CountForward()
        {
            ForwardCount++;
            return null;
        }

        private object? CountBackward()
        {
            BackwardCount++;
            return null;
        }
    }

    public class ViewModelProxy: DispatchProxy
    {
        private readonly ICommand nextCommand;
        private readonly ICommand previousCommand;

        public ViewModelProxy()
        {
            nextCommand = new TestCommand(() => NextCount++);
            previousCommand = new TestCommand(() => PreviousCount++);
        }

        public IMediaPlayerService Player { get; set; } = null!;
        public int NextCount { get; private set; }
        public int PreviousCount { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                "get_VideoService" => Player,
                "get_NextVideoCommand" => nextCommand,
                "get_PreviousVideoCommand" => previousCommand,
                _ => DefaultValue(targetMethod?.ReturnType)
            };
        }
    }

    private sealed class TestCommand(Action execute): ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => execute();
    }

    private static object? DefaultValue(Type? type) =>
        type is not null && type.IsValueType ? Activator.CreateInstance(type) : null;
}
