using UAlbion.Formats.Ids;

namespace UAlbion.Game.Combat;

// Stores one party member's pre-planned action for the upcoming round.
public record PlannedCombatAction(CombatActionType ActionType, int TargetTileIndex = -1, SpellId SpellId = default);
