namespace FCR.Domain;

/// <summary>Request Status (§6) — lifecycle of the initial submitted request.</summary>
public static class RequestStatuses
{
    public const string Draft = "Draft";
    public const string Submitted = "Submitted";
    public const string UnderAdminReview = "Under Admin Review";
    public const string ReturnedToAdmin = "Returned to Admin";
    public const string UnderReview = "Under Review";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Completed = "Completed";
}

/// <summary>Workflow Status (§6) — state of the active review process.</summary>
public static class WorkflowStatuses
{
    public const string NotStarted = "Not Started";
    public const string InProgress = "In Progress";
    public const string AwaitingReview = "Awaiting Review";
    public const string AwaitingApproval = "Awaiting Approval";
    public const string ReturnedForCorrection = "Returned for Correction";
    public const string Rejected = "Rejected";
    public const string Approved = "Approved";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
}

/// <summary>Review modes the FCR Admin selects per request (§2.4).</summary>
public static class ReviewModes
{
    public const string Sequential = "sequential";
    public const string Parallel = "parallel";
    public static readonly string[] All = [Sequential, Parallel];
}

/// <summary>Review action types recorded in fcr_reviews (§2.5).</summary>
public static class ReviewActions
{
    public const string Approve = "approve";
    public const string Comment = "comment";
    public const string Reject = "reject";
    public const string Return = "return";
    public static readonly string[] All = [Approve, Comment, Reject, Return];
}

/// <summary>Decision rules a stage may use (§2.4B); "count:N" = N approvals suffice.</summary>
public static class DecisionRules
{
    public const string All = "all";
    public const string Any = "any";
    public static readonly string[] Named = [All, Any];

    public static bool IsValid(string rule) =>
        Named.Contains(rule) ||
        (rule.StartsWith("count:", StringComparison.Ordinal) &&
         int.TryParse(rule[6..], out var n) && n >= 1);

    /// <summary>Whether the stage completes once `approvals` of `total` reviewers approved.</summary>
    public static bool IsSatisfied(string rule, int approvals, int total) => rule switch
    {
        All => approvals >= total,
        Any => approvals >= 1,
        _ when rule.StartsWith("count:", StringComparison.Ordinal) &&
               int.TryParse(rule[6..], out var n) => approvals >= n,
        _ => approvals >= total,
    };
}

/// <summary>Authorization policy names combining roles.</summary>
public static class PolicyNames
{
    /// <summary>FCR Admin or System Administrator — admin console access.</summary>
    public const string AdminConsole = "AdminConsole";
    /// <summary>Requester or System Administrator — may submit requests.</summary>
    public const string RequesterAccess = "RequesterAccess";
    /// <summary>Reviewer or System Administrator — may review assigned requests.</summary>
    public const string ReviewerAccess = "ReviewerAccess";
}
