using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Text.Json;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Exceptions;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests;

public sealed class CommentControllerTests(WorthBoardsApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task CreateComment_WithAuthorizedUser_CoversCommentLifecycle()
    {
        // Arrange
        var client = await CreateAuthenticatedClientAsync("viewer");

        // Act
        var listResponse = await client.GetAsync($"/api/boards/{Seed.BoardId}/tasks/{Seed.TaskId}/comments");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var listBody = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        listBody.Should().NotBeNull();

        var createResponse = await client.PostAsJsonAsync($"/api/boards/{Seed.BoardId}/tasks/{Seed.TaskId}/comments", new CommentRequest
        {
            Content = "Integration comment"
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdComment = await ReadJsonAsync<CommentResponse>(createResponse);
        createdComment.Should().NotBeNull();
        createdComment!.Content.Should().Be("Integration comment");

        var updateResponse = await client.PutAsJsonAsync($"/api/boards/{Seed.BoardId}/tasks/{Seed.TaskId}/comments/{createdComment.Id}", new CommentUpdateRequest
        {
            Content = "Updated integration comment",
            Version = createdComment.Version
        });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var patchDocument = new JsonPatchDocument<CommentUpdateRequest>();
        patchDocument.Replace(comment => comment.Content, "Patched integration comment");
        var patchRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/boards/{Seed.BoardId}/tasks/{Seed.TaskId}/comments/{createdComment.Id}")
        {
            Content = new StringContent(JsonConvert.SerializeObject(patchDocument), Encoding.UTF8, "application/json-patch+json")
        };
        var patchResponse = await client.SendAsync(patchRequest);
        patchResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getResponse = await client.GetAsync($"/api/boards/{Seed.BoardId}/tasks/{Seed.TaskId}/comments/{createdComment.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var deleteResponse = await client.DeleteAsync($"/api/boards/{Seed.BoardId}/tasks/{Seed.TaskId}/comments/{createdComment.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
            (await context.Comments.AnyAsync(comment => comment.Id == createdComment.Id)).Should().BeFalse();
        }
    }

    [Fact]
    public async Task GetCommentById_WithUnknownCommentId_ReturnsNotFoundAndLeavesSeedComment()
    {
        // Arrange
        var client = await CreateAuthenticatedClientAsync("viewer");

        // Act
        var response = await client.GetAsync($"/api/boards/{Seed.BoardId}/tasks/{Seed.TaskId}/comments/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await ReadJsonAsync<ErrorDetails>(response);
        error.Should().NotBeNull();
        error!.StatusCode.Should().Be((int)HttpStatusCode.NotFound);

        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        (await context.Comments.AnyAsync(comment => comment.Id == Seed.CommentId)).Should().BeTrue();
    }
}