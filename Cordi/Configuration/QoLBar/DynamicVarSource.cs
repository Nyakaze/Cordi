namespace Cordi.Configuration.QoLBar;

public enum DynamicVarSource
{
    // ── Job / Role ─────────────────────────────────────────────────────────
    /// <summary>3-letter job abbreviation, e.g. "WAR"</summary>
    JobAbbr,

    /// <summary>Full job name, e.g. "Warrior"</summary>
    JobName,

    /// <summary>Role group: "Tank", "Healer", "Melee", "Ranged", "Caster", "Crafter", "Gatherer"</summary>
    JobRole,

    /// <summary>Character level as string, e.g. "100"</summary>
    Level,

    // ── World / Zone ───────────────────────────────────────────────────────
    /// <summary>Territory name from sheets, e.g. "Limsa Lominsa Upper Decks"</summary>
    ZoneName,

    /// <summary>Numeric territory ID as string, e.g. "128"</summary>
    ZoneId,

    // ── Resources ─────────────────────────────────────────────────────────
    /// <summary>HP percentage 0-100, e.g. "72"</summary>
    HpPct,

    /// <summary>MP percentage 0-100, e.g. "100"</summary>
    MpPct,

    // ── Player State ──────────────────────────────────────────────────────
    /// <summary>"Online", "AFK", "Busy", etc.</summary>
    OnlineStatus,

    // ── Game Conditions (bool → "true"/"false") ────────────────────────────
    /// <summary>Whether the player is in combat</summary>
    InCombat,

    /// <summary>Whether the player is in a duty/instance</summary>
    InDuty,

    /// <summary>Whether the player is mounted</summary>
    Mounted,

    /// <summary>Whether the player is flying</summary>
    Flying,

    /// <summary>Whether the player is swimming / diving</summary>
    Swimming,

    /// <summary>Whether the player is crafting</summary>
    Crafting,

    /// <summary>Whether the player is gathering</summary>
    Gathering,

    /// <summary>Whether the player's weapon is drawn</summary>
    WeaponDrawn,

    /// <summary>Whether the player is performing (Bard performance mode)</summary>
    Performing,
}
