namespace WorthBoards.IntegrationTests.Infrastructure;

public sealed record SeedState(
    int OwnerUserId,
    int EditorUserId,
    int ViewerUserId,
    int CandidateUserId,
    int OutsiderUserId,
    int BoardId,
    int TaskId,
    int CommentId,
    int InvitationNotificationId);