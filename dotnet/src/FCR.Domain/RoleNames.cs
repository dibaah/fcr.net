namespace FCR.Domain;

/// <summary>
/// The FCR module and workflow roles. Names are the API-visible
/// values and are also used as claim values in the auth cookie.
/// </summary>
public static class RoleNames
{
    public const string FcrUser = "FCR User";
    public const string Requester = "Requester";
    public const string FcrAdmin = "FCR Admin";
    public const string Reviewer = "Reviewer";
    public const string Approver = "Approver";
    public const string SystemAdministrator = "System Administrator";

    public static readonly string[] All =
    [
        FcrUser, Requester, FcrAdmin, Reviewer, Approver, SystemAdministrator,
    ];
}
