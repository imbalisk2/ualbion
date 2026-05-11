namespace UAlbion.Base;

public enum UAlbionString
{
    LanguageLabel = 0,
    TakeAll = 1,
    SellAll = 2,
    QolHoverExamine = 3,
    QolQuickMerchantBuy = 4,
    QolCtrlSell = 5,
    QolAltMultiSell = 6,
    QolSection = 7,

    CombatMsg_XAttacksY = 8,
    CombatMsg_XAttacksYUnarmed = 9,
    CombatMsg_XMissesY = 10,
    CombatMsg_XCannotHurtY = 11,
    CombatMsg_XCriticalHit = 12,
    CombatMsg_XMoves = 13,
    CombatMsg_XFlees = 14,
    CombatMsg_XCastsSpell = 15,
    CombatMsg_XCastsSpellOnY = 16,
    CombatMsg_XTriesToFlee = 17,

    /// <summary>DEVIATION: message text not confirmed in original game — added for playability.</summary>
    CombatMsg_XHasNoTriifalaiSeed = 18,
    CombatMsg_UnknownSpell = 19,

    // DEVIATION: SystemText IDs for post-combat screen not confirmed in original game.
    CombatMsg_Victory = 20,
    CombatMsg_XpGained = 21,
    CombatMsg_GoldFound = 22,
    CombatMsg_FoodFound = 23,
}