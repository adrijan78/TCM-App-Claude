namespace TCM.Domain.Enums;

/// <summary>
/// SPEC section 4: Notes.Priority. Determines display order in a member's profile —
/// High first (section 6.8) — so the numeric values are ordered deliberately.
/// </summary>
public enum NotePriority
{
    Low = 0,
    Medium = 1,
    High = 2
}
