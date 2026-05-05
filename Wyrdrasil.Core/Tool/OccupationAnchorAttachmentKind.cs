namespace Wyrdrasil.Core.Tool;

/// <summary>
/// Describes the concrete attachment mechanism used by an anchored occupation.
///
/// This is intentionally small and data-oriented: higher level modules decide why an
/// actor wants an anchor, while runtime actors only need to know how the anchor is
/// engaged once navigation has brought them close enough.
/// </summary>
public enum OccupationAnchorAttachmentKind
{
    None = 0,
    Seat = 1,
    Bed = 2,

    /// <summary>
    /// Non-native anchored standing position controlled by Wyrdrasil.
    /// Useful for counters, guard posts, work spots, shrines and similar future occupations.
    /// </summary>
    StandingPoint = 3,

    /// <summary>
    /// Non-native anchored work position controlled by Wyrdrasil, usually paired with
    /// a work animation or an interactable craft station.
    /// </summary>
    WorkPoint = 4
}
