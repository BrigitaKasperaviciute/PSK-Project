using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.Common.Exceptions;
using WorthBoards.Data.Database;
using WorthBoards.IntegrationTests.Infrastructure;
using Xunit;

namespace WorthBoards.IntegrationTests.Controllers;

[Collection(nameof(IntegrationCollection))]
public sealed class CommentControllerTests(IntegrationTestAppFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task CreateAndUpdateComment_WithAuthenticatedUser_PersistsEditedFlag()
    {
        // Arrange
        var user = await Factory.CreateUserAsync("Olive", "Quinn", "comment-user");
        var board = await Factory.SeedBoardAsync(user.Id, "Comments board", "Comment lifecycle");
        var task = await Factory.SeedTaskAsync(board.Id, "Task with comments", "Seeded task", TaskStatusEnum.PENDING);
        var client = Factory.CreateAuthenticatedClient(user);

        var createRequest = new CommentRequest { Content = "Initial comment" };

        // Act
        var createResponse = await client.PostAsJsonAsync($"/api/boards/{board.Id}/tasks/{task.Id}/comments", createRequest);

        // Assert - create
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdComment = await createResponse.Content.ReadFromJsonAsync<CommentResponse>();
        createdComment.Should().NotBeNull();
        createdComment!.Content.Should().Be(createRequest.Content);
        createdComment.Edited.Should().BeFalse();

        var persistedComment = await Factory.WithDbContextAsync(async db =>
            await db.Comments.AsNoTracking().SingleAsync(item => item.Id == createdComment.Id));

        var updateRequest = new CommentUpdateRequest
        {
            Content = "Updated comment",
            Version = persistedComment.Version
        };

        var updateResponse = await client.PutAsJsonAsync($"/api/boards/{board.Id}/tasks/{task.Id}/comments/{createdComment.Id}", updateRequest);

        // Assert - update
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedComment = await updateResponse.Content.ReadFromJsonAsync<CommentResponse>();
        updatedComment.Should().NotBeNull();
        updatedComment!.Content.Should().Be(updateRequest.Content);
        updatedComment.Edited.Should().BeTrue();

        await Factory.WithDbContextAsync(async db =>
        {
            var persisted = await db.Comments.AsNoTracking().SingleAsync(item => item.Id == createdComment.Id);
            persisted.Content.Should().Be(updateRequest.Content);
            persisted.Edited.Should().BeTrue();
        });
    }

    [Fact]
    public async Task GetCommentById_WhenCommentMissing_ReturnsNotFoundAndLeavesDatabaseEmpty()
    {
        // Arrange
        var user = await Factory.CreateUserAsync("Paul", "Reed", "comment-missing");
        var board = await Factory.SeedBoardAsync(user.Id, "Comment lookup board", "Negative flow");
        var task = await Factory.SeedTaskAsync(board.Id, "Seeded task", null, TaskStatusEnum.PENDING);
        var client = Factory.CreateAuthenticatedClient(user);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}/comments/9999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ErrorDetails>();
        error.Should().NotBeNull();
        error!.StatusCode.Should().Be((int)HttpStatusCode.NotFound);

        await Factory.WithDbContextAsync(async db =>
        {
            var comments = await db.Comments.AsNoTracking().ToListAsync();
            comments.Should().BeEmpty();
        });
    }

    [Fact]
    public async Task GetUpdatePatchDelete_WithAuthenticatedUser_ExercisesCommentLifecycle()
    {
        // Arrange
        var user = await Factory.CreateUserAsync("Quinn", "Miles", "comment-lifecycle");
        var board = await Factory.SeedBoardAsync(user.Id, "Comment lifecycle board", "Read update patch delete");
        var task = await Factory.SeedTaskAsync(board.Id, "Lifecycle task", null, TaskStatusEnum.PENDING);
        var client = Factory.CreateAuthenticatedClient(user);

        var createResponse = await client.PostAsJsonAsync($"/api/boards/{board.Id}/tasks/{task.Id}/comments", new CommentRequest
        {
            Content = "Lifecycle comment"
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdComment = await createResponse.Content.ReadFromJsonAsync<CommentResponse>();
        createdComment.Should().NotBeNull();

        // Act - get all
        var getAllResponse = await client.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}/comments");

        // Assert - get all
        getAllResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var getAllBody = await getAllResponse.Content.ReadFromJsonAsync<JsonElement>();
        getAllBody.GetProperty("items").EnumerateArray().Should().ContainSingle(item => item.GetProperty("id").GetInt32() == createdComment!.Id);

        // Act - get by id
        var getByIdResponse = await client.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}/comments/{createdComment.Id}");

        // Assert - get by id
        getByIdResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var persistedComment = await Factory.WithDbContextAsync(async db =>
            await db.Comments.AsNoTracking().SingleAsync(item => item.Id == createdComment.Id));

        // Act - update
        var updateResponse = await client.PutAsJsonAsync($"/api/boards/{board.Id}/tasks/{task.Id}/comments/{createdComment.Id}", new CommentUpdateRequest
        {
            Content = "Lifecycle comment updated",
            Version = persistedComment.Version
        });

        // Assert - update
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedComment = await updateResponse.Content.ReadFromJsonAsync<CommentResponse>();
        updatedComment.Should().NotBeNull();

        // Act - patch
        var patchOperations = new[]
        {
            new Dictionary<string, object?> { ["op"] = "replace", ["path"] = "/content", ["value"] = "Lifecycle comment patched" },
            new Dictionary<string, object?> { ["op"] = "replace", ["path"] = "/version", ["value"] = updatedComment!.Version }
        };
        var patchContent = new StringContent(JsonSerializer.Serialize(patchOperations), Encoding.UTF8, "application/json-patch+json");
        var patchResponse = await client.PatchAsync($"/api/boards/{board.Id}/tasks/{task.Id}/comments/{createdComment.Id}", patchContent);

        // Assert - patch
        patchResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act - delete
        var deleteResponse = await client.DeleteAsync($"/api/boards/{board.Id}/tasks/{task.Id}/comments/{createdComment.Id}");

        // Assert - delete
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await Factory.WithDbContextAsync(async db =>
        {
            var comments = await db.Comments.AsNoTracking().Where(item => item.BoardTaskId == task.Id).ToListAsync();
            comments.Should().BeEmpty();
        });
    }
}