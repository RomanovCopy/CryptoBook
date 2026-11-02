using System.Configuration;
using System.IO;

namespace CryptoBook.Help;

public sealed record HelpWindowState(string Topic = "start.interface", double Left = double.NaN,
    double Top = double.NaN, double Width = 1120, double Height = 780, double NavigationWidth = 280);

public interface IHelpPreferences
{
    HelpWindowState Load();
    void Save(HelpWindowState state);
}

public sealed class HelpPreferences : IHelpPreferences
{
    public HelpWindowState Load()
    {
        var s = Properties.Settings.Default;
        return new(s.HelpLastTopicId, s.HelpWindowLeft, s.HelpWindowTop,
            s.HelpWindowWidth, s.HelpWindowHeight, s.HelpNavigationWidth);
    }

    public void Save(HelpWindowState state)
    {
        try
        {
            var s = Properties.Settings.Default;
            s.HelpLastTopicId = state.Topic;
            s.HelpWindowLeft = state.Left; s.HelpWindowTop = state.Top;
            s.HelpWindowWidth = state.Width; s.HelpWindowHeight = state.Height;
            s.HelpNavigationWidth = state.NavigationWidth;
            s.Save();
        }
        catch (Exception exception) when (exception is ConfigurationErrorsException or IOException or UnauthorizedAccessException)
        { /* Reading help remains available when the user profile is read-only. */ }
    }
}
