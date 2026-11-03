namespace FCR.Application;

public record CreateRequestInput(string? Subject, string? Justification, string? Description,
    string? Title = null);
public record RequestListItemDto(
    string Id, string RequestNumber, long RequesterUserId, string RequesterUsername,
    string Subject, string SubmissionStatus, string? SubmittedAt, string CreatedAt,
    string UpdatedAt, long? LinkedFcrId, string FcrCode, string Category,
    string Contractor, string Subcontractor, bool IsArchived, string? ArchivedAt,
    int AttachmentCount, int CommentCount)
{
    /// <summary>ERMS-facing alias for the legacy Subject field.</summary>
    public string Title => Subject;
}
public record RequestDetailDto(
    string Id, string RequestNumber, long RequesterUserId, string RequesterUsername,
    string Subject, string Justification, string Description, string SubmissionStatus,
    string? SubmittedAt, string CreatedAt, string UpdatedAt, long? LinkedFcrId,
    string FcrCode, string Category, string Contractor, string Subcontractor,
    bool IsArchived, string? ArchivedAt,
    List<CommentDto> Comments, List<AttachmentDto> Attachments, WorkflowDto? Workflow)
{
    /// <summary>ERMS-facing alias for the legacy Subject field.</summary>
    public string Title => Subject;
}
public record CommentDto(long Id, long UserId, string Username, string CommentText,
    string CommentType, string CreatedAt);
public record CommentInput(string CommentText);
public record AttachmentDto(long Id, long UploadedByUserId, string UploadedByUsername,
    string OriginalFileName, string ContentType, long FileSize, string UploadedAt);
public record WorkflowDto(long Id, string WorkflowStatus, string ReviewMode,
    int CurrentStage, int Cycle, List<StageDto> Stages, string? StartedAt);
public record StageDto(string StageName, int StageOrder, string DecisionRule,
    string StageStatus, List<ReviewerAssignmentDto> Reviewers);
public record ReviewerAssignmentDto(long UserId, string Username, int Order, string Status);
public record AdminUpdateInput(string? FcrCode, string? Category, long? LinkedFcrId,
    string? Contractor = null, string? Subcontractor = null);
public record ArchiveInfoDto(string RequestId, string RequestNumber, string FcrCode,
    string ArchivePath, string FormFileName, string ArchivedAt, int AttachmentCount);
public record WorkflowPrepInput(string ReviewMode, List<long>? ReviewerUserIds,
    string? DecisionRule, List<long>? ApproverUserIds = null,
    long? WorkflowDefinitionId = null);
public record ReviewDecisionInput(string Action, string? Comment);

/// <summary>A review task on a reviewer's dashboard (an Active assignment).</summary>
public record ReviewTaskDto(long AssignmentId, string RequestId, string RequestNumber,
    string Subject, string StageName, int Cycle, string RequestStatus,
    string AssignedAt, string? LastReviewComment);

/// <summary>One row of the immutable review history (fcr_reviews).</summary>
public record ReviewActionDto(long Id, long ReviewerUserId, string ReviewerName,
    string StageName, int ReviewCycle, string ActionType, string? Comment,
    string PreviousStatus, string ResultingStatus, string CreatedAt);

public record NotificationDto(long Id, string NotificationType, string Message,
    string? FcrRequestId, bool IsRead, string CreatedAt);
public record NotificationListDto(int Unread, List<NotificationDto> Items);

public record WorkflowDefinitionDto(long Id, string WorkflowName, string ReviewMode,
    string Configuration, bool IsActive, string CreatedAt);
public record WorkflowDefinitionInput(string WorkflowName, string ReviewMode,
    string? Configuration, bool IsActive = true);

public interface IRequestService
{
    Task<RequestDetailDto> CreateAsync(long requesterUserId, CreateRequestInput input, CancellationToken ct = default);
    Task<List<RequestListItemDto>> ListAsync(long? onlyUserId, string? status, CancellationToken ct = default);
    Task<RequestDetailDto?> GetAsync(string id, CancellationToken ct = default);
    Task<long?> OwnerIdAsync(string id, CancellationToken ct = default);
    Task<CommentDto?> AddCommentAsync(string id, long userId, string text, string commentType, CancellationToken ct = default);
    Task<AttachmentDto> AddAttachmentAsync(string id, long userId, string fileName, string contentType, Stream content, CancellationToken ct = default);
    Task<FcrAttachmentInfo?> GetAttachmentAsync(string id, long attachmentId, CancellationToken ct = default);
    Task<bool> DeleteAttachmentAsync(string id, long attachmentId, long userId, bool isAdmin, CancellationToken ct = default);
    Task<RequestDetailDto?> AdminUpdateAsync(string id, AdminUpdateInput input, CancellationToken ct = default);
    Task<RequestDetailDto?> CreateLinkedFcrAsync(string id, CancellationToken ct = default);
    Task<ArchiveInfoDto?> ArchiveAsync(string id, CancellationToken ct = default);
    Task<RequestDetailDto?> PrepareWorkflowAsync(string id, WorkflowPrepInput input, CancellationToken ct = default);
    Task<RequestDetailDto?> SubmitForReviewAsync(string id, CancellationToken ct = default);

    // Phase 6 — review/approval workflow execution
    Task<List<ReviewTaskDto>> ListMyReviewsAsync(long userId, CancellationToken ct = default);
    Task<RequestDetailDto> SubmitDecisionAsync(string requestId, long userId,
        ReviewDecisionInput input, CancellationToken ct = default);
    Task<RequestDetailDto?> ResubmitAsync(string requestId, CancellationToken ct = default);
    Task<List<ReviewActionDto>> GetReviewHistoryAsync(string requestId, CancellationToken ct = default);
    Task<List<WorkflowDefinitionDto>> ListWorkflowDefinitionsAsync(CancellationToken ct = default);
    Task<WorkflowDefinitionDto> CreateWorkflowDefinitionAsync(WorkflowDefinitionInput input, CancellationToken ct = default);
    Task<NotificationListDto> ListNotificationsAsync(long userId, CancellationToken ct = default);
    Task<bool> MarkNotificationReadAsync(long userId, long notificationId, CancellationToken ct = default);
}

public sealed record FcrAttachmentInfo(
    AttachmentDto Meta, string PhysicalPath, string DownloadName, string ContentType);
