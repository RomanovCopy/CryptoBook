using CryptoBook.Behaviors;
using CryptoBook.Security;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;

namespace CryptoBook.Tests;

public sealed class WorkspaceLockPresentationTests
{
    [WpfFact]
    public void LockedStatesReplaceRenderedWorkspaceAndDisableInteraction()
    {
        var workspace = new Border { Background = Brushes.Red };
        var shield = new Border { Background = Brushes.Blue };
        var unlock = new Button();
        var close = new Button();
        var root = new Grid { Width = 200, Height = 120 };
        root.Children.Add(workspace);
        root.Children.Add(shield);
        foreach(KeyResetState state in new[] { KeyResetState.Active, KeyResetState.Resetting,
                     KeyResetState.KeyReset, KeyResetState.Unlocking, KeyResetState.KeyReset, KeyResetState.Active })
        {
            WorkspaceLockPresentation.Apply(workspace, shield, unlock, close, state, hasRetainedDocument: true);
            root.Measure(new Size(200, 120));
            root.Arrange(new Rect(0, 0, 200, 120));
            root.UpdateLayout();
            var bitmap = new RenderTargetBitmap(200, 120, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(root);
            byte[] pixel = new byte[4];
            bitmap.CopyPixels(new Int32Rect(100, 60, 1, 1), pixel, 4, 0);
            bool locked = state != KeyResetState.Active;
            Assert.Equal(locked ? new byte[] { 255, 0, 0, 255 } : new byte[] { 0, 0, 255, 255 }, pixel);
            Assert.Equal(!locked, workspace.IsEnabled);
            Assert.Equal(state == KeyResetState.KeyReset, unlock.IsEnabled);
            Assert.False(close.IsEnabled);
        }
        WorkspaceLockPresentation.Apply(workspace, shield, unlock, close, KeyResetState.KeyReset, false);
        Assert.True(close.IsEnabled);
        Assert.True(workspace.IsEnabled);
        Assert.Equal(Visibility.Visible, workspace.Visibility);
        Assert.Equal(Visibility.Collapsed, shield.Visibility);
    }

    [WpfFact]
    public void NoRetainedEncryptedDocument_DoesNotRequireApplicationUnlock()
    {
        var workspace = new Border();
        var shield = new Border();
        foreach(var state in new[] { KeyResetState.Inactive, KeyResetState.Active,
                     KeyResetState.KeyReset, KeyResetState.Unlocking, KeyResetState.Restoring })
        {
            WorkspaceLockPresentation.Apply(workspace, shield, null, null, state, false);
            Assert.True(workspace.IsEnabled);
            Assert.Equal(Visibility.Visible, workspace.Visibility);
            Assert.Equal(Visibility.Collapsed, shield.Visibility);
        }
    }
}
