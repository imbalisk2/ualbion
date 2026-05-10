using UAlbion.Api.Settings;
using UAlbion.Game.Settings;

namespace UAlbion.Game;

// QoL feature flags — stored in user settings, all default to enabled (1).
// Read via: ReadVar(V.User.Qol.HoverExamine) != 0
public static class QoLFeature
{
    public static IntVar HoverExamine        => UserVars.Instance.Qol.HoverExamine;
    public static IntVar MerchantDirectBuy   => UserVars.Instance.Qol.MerchantDirectBuy;
    public static IntVar MerchantCtrlSell    => UserVars.Instance.Qol.MerchantCtrlSell;
    public static IntVar MerchantAltMultiSell=> UserVars.Instance.Qol.MerchantAltMultiSell;
}
