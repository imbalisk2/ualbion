using System.Collections.Generic;
using System.Text;
using UAlbion.Api.Eventing;
using UAlbion.Base;
using UAlbion.Core;
using UAlbion.Formats;
using UAlbion.Formats.Ids;
using UAlbion.Game.Events;
using UAlbion.Game.Gui.Controls;
using UAlbion.Game.Gui.Text;
using UAlbion.Game.Text;

namespace UAlbion.Game.Gui.Combat;

/// <summary>
/// Post-combat victory screen showing XP gained, gold, food, and found items.
/// </summary>
public class ApresCombatDialog : ModalDialog
{
    readonly AlbionTaskCore _source = new(nameof(ApresCombatDialog));
    readonly int _xpShare;
    readonly int _apresGold;
    readonly int _apresFood;
    readonly IReadOnlyList<(ItemId Item, int Amount)> _apresItems;

    public AlbionTask Task => _source.UntypedTask;

    public ApresCombatDialog(
        int xpShare,
        int apresGold,
        int apresFood,
        IReadOnlyList<(ItemId Item, int Amount)> apresItems) : base(DialogPositioning.Top)
    {
        On<DismissMessageEvent>(_ => Close());
        On<CloseWindowEvent>(_ => Close());
        On<UiLeftClickEvent>(e => { Close(); e.Propagating = false; });
        On<UiRightClickEvent>(e => { Close(); e.Propagating = false; });

        _xpShare = xpShare;
        _apresGold = apresGold;
        _apresFood = apresFood;
        _apresItems = apresItems;
    }

    protected override void Subscribed()
    {
        // Auto-dismiss in agent mode (no mouse clicks available)
        if (RaiseQuery(new IsAgentModeEvent()))
        {
            Close();
            return;
        }

        var tf = Resolve<ITextFormatter>();
        var assets = Resolve<IAssetManager>();
        var sb = new StringBuilder();

        // DEVIATION: SystemText IDs for post-combat screen not confirmed in original game.
        sb.AppendLine(assets.LoadStringSafe(TextId.From(UAlbionString.CombatMsg_Victory)));

        if (_xpShare > 0)
        {
            var fmt = assets.LoadStringSafe(TextId.From(UAlbionString.CombatMsg_XpGained));
            sb.AppendLine(fmt.Replace("%s", _xpShare.ToString()));
        }

        if (_apresGold > 0)
        {
            var fmt = assets.LoadStringSafe(TextId.From(UAlbionString.CombatMsg_GoldFound));
            sb.AppendLine(fmt.Replace("%s", _apresGold.ToString()));
        }

        if (_apresFood > 0)
        {
            var fmt = assets.LoadStringSafe(TextId.From(UAlbionString.CombatMsg_FoodFound));
            sb.AppendLine(fmt.Replace("%s", _apresFood.ToString()));
        }

        // DEVIATION: item names not localised here; full item distribution UI not implemented.
        foreach (var (itemId, amount) in _apresItems)
        {
            var label = amount > 1 ? $"{itemId} x{amount}" : itemId.ToString();
            sb.AppendLine(label);
        }

        IText text = tf.Center().Format(sb.ToString().TrimEnd());
        var textSection = new UiText(text);
        var padding = new Padding(textSection, 3, 7);
        var stack = new FixedWidth(320, padding);
        AttachChild(new DialogFrame(stack) { Background = DialogFrameBackgroundStyle.DarkTint });
    }

    void Close()
    {
        Remove();
        _source.Complete();
    }
}
