using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.IntegrationTests.Experiment1.TestInfrastructure;
using Xunit;

namespace WorthBoards.IntegrationTests.Experiment1;

public class CommentControllerIntegrationTests(IntegrationTestWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    [Fact]
    public async Task CreateComment_AuthenticatedRequest_CreatesCommentAndReturnsCreated()
    {
        // Arrange
        var session = await CreateAuthenticatedUserAsync("comment_happy");
        var boardId = await CreateBoardAsync(session.Client, "Comment Board");
        var taskId = await CreateBoardTaskAsync(session.Client, boardId, "Comment task");

        var request = new CommentRequest
        {
            Content = "This is an integration-test comment"
        };

        // Act
        var response = await session.Client.PostAsJsonAsync($"/api/boards/{boardId}/tasks/{taskId}/comments", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(request.Content);

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        var persistedComment = await dbContext.Comments.SingleAsync(c => c.BoardTaskId == taskId && c.Content == request.Content);
        persistedComment.UserId.Should().Be(session.UserId);
    }

    [Fact]
    public async Task CreateComment_EmptyContent_ReturnsBadRequestAndDoesNotPersistComment()
    {
        // Arrange
        var session = await CreateAuthenticatedUserAsync("comment_negative");
        var boardId = await CreateBoardAsync(session.Client, "Comment Validation Board");
        var taskId = await CreateBoardTaskAsync(session.Client, boardId, "Comment validation task");

        using var arrangeScope = Factory.Services.CreateScope();
        var arrangeDb = arrangeScope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        var commentCountBefore = await arrangeDb.Comments.CountAsync();

        // Act
        var response = await session.Client.PostAsJsonAsync(
            $"/api/boards/{boardId}/tasks/{taskId}/comments",
            new CommentRequest { Content = string.Empty });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Content");

        using var assertScope = Factory.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        var commentCountAfter = await assertDb.Comments.CountAsync();
        commentCountAfter.Should().Be(commentCountBefore);
    }

    [Fact]
    public async Task UpdateComment_ValidVersion_UpdatesContentAndMarksEdited()
    {
        // Arrange
        var session = await CreateAuthenticatedUserAsync("comment_update");
        var boardId = await CreateBoardAsync(session.Client, "Comment Update Board");
        var taskId = await CreateBoardTaskAsync(session.Client, boardId, "Comment update task");
        var commentId = await CreateCommentAsync(session.Client, boardId, taskId, "Before update");
        var existing = await session.Client.GetFromJsonAsync<CommentResponse>($"/api/boards/{boardId}/tasks/{taskId}/comments/{commentId}");

        var request = new CommentUpdateRequest
        {
            Content = "After update",
            Version = existing!.Version
        };

        // Act
        var response = await session.Client.PutAsJsonAsync($"/api/boards/{boardId}/tasks/{taskId}/comments/{commentId}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        var comment = await db.Comments.SingleAsync(c => c.Id == commentId);
        comment.Content.Should().Be("After update");
        comment.Edited.Should().BeTrue();
    }

    [Fact]
    public async Task PatchComment_ValidPatch_UpdatesContent()
    {
        // Arrange
        var session = await CreateAuthenticatedUserAsync("comment_patch");
        var boardId = await CreateBoardAsync(session.Client, "Comment Patch Board");
        var taskId = await CreateBoardTaskAsync(session.Client, boardId, "Comment patch task");
        var commentId = await CreateCommentAsync(session.Client, boardId, taskId, "Before patch");
        var patchBody = "[{\"op\":\"replace\",\"path\":\"/content\",\"value\":\"After patch\"}]";
        using var content = new StringContent(patchBody, Encoding.UTF8, "application/json-patch+json");

        // Act
        var response = await session.Client.PatchAsync($"/api/boards/{boardId}/tasks/{taskId}/comments/{commentId}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        var comment = await db.Comments.SingleAsync(c => c.Id == commentId);
        comment.Content.Should().Be("After patch");
    }

    [Fact]
    public async Task DeleteComment_ExistingComment_RemovesComment()
    {
        // Arrange
        var session = await CreateAuthenticatedUserAsync("comment_delete");
        var boardId = await CreateBoardAsync(session.Client, "Comment Delete Board");
        var taskId = await CreateBoardTaskAsync(session.Client, boardId, "Comment delete task");
        var commentId = await CreateCommentAsync(session.Client, boardId, taskId, "Delete me");

        // Act
        var response = await session.Client.DeleteAsync($"/api/boards/{boardId}/tasks/{taskId}/comments/{commentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        var exists = await db.Comments.AnyAsync(c => c.Id == commentId);
        exists.Should().BeFalse();
    }
}
