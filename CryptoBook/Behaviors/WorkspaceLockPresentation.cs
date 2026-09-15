using CryptoBook.Security;
using System.Windows;
using Button = System.Windows.Controls.Button;

namespace CryptoBook.Behaviors;

internal static class WorkspaceLockPresentation
{
    internal static bool IsLocked(KeyResetState? state, bool hasRetainedDocument) =>
        state == KeyResetState.Resetting || (hasRetainedDocument &&
        state is KeyResetState.KeyReset or KeyResetState.Unlocking);

    internal static void Apply(FrameworkElement workspace, FrameworkElement shield,
        Button? unlock, Button? close, KeyResetState state, bool hasRetainedDocument)
    {
        bool locked = IsLocked(state, hasRetainedDocument);
        workspace.Visibility = locked ? Visibility.Hidden : Visibility.Visible;
        workspace.IsEnabled = !locked;
        shield.Visibility = locked ? Visibility.Visible : Visibility.Collapsed;
        if(unlock is not null)
        {
            unlock.IsEnabled = state == KeyResetState.KeyReset;
            if(unlock.IsEnabled) unlock.Focus();
        }
        if(close is not null)
            close.IsEnabled = state == KeyResetState.KeyReset && !hasRetainedDocument;
    }
}
