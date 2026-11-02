using System.Configuration;

namespace CryptoBook.Properties;

public sealed partial class Settings
{
    [UserScopedSetting, DefaultSettingValue("start.interface")]
    public string HelpLastTopicId { get => (string)this[nameof(HelpLastTopicId)]; set => this[nameof(HelpLastTopicId)] = value; }
    [UserScopedSetting, DefaultSettingValue("NaN")]
    public double HelpWindowLeft { get => (double)this[nameof(HelpWindowLeft)]; set => this[nameof(HelpWindowLeft)] = value; }
    [UserScopedSetting, DefaultSettingValue("NaN")]
    public double HelpWindowTop { get => (double)this[nameof(HelpWindowTop)]; set => this[nameof(HelpWindowTop)] = value; }
    [UserScopedSetting, DefaultSettingValue("1120")]
    public double HelpWindowWidth { get => (double)this[nameof(HelpWindowWidth)]; set => this[nameof(HelpWindowWidth)] = value; }
    [UserScopedSetting, DefaultSettingValue("780")]
    public double HelpWindowHeight { get => (double)this[nameof(HelpWindowHeight)]; set => this[nameof(HelpWindowHeight)] = value; }
    [UserScopedSetting, DefaultSettingValue("280")]
    public double HelpNavigationWidth { get => (double)this[nameof(HelpNavigationWidth)]; set => this[nameof(HelpNavigationWidth)] = value; }
}
