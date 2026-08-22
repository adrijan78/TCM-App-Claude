namespace TCM.Domain.Constants;

/// <summary>
/// The only two roles in the system (SPEC section 5). Referenced by
/// <c>[Authorize(Roles = ...)]</c> attributes, so these must stay string constants.
/// </summary>
public static class Roles
{
    public const string Coach = "Coach";
    public const string Member = "Member";

    public static readonly string[] All = [Coach, Member];
}
