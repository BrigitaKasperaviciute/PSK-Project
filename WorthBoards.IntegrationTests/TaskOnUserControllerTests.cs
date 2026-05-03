using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Exceptions;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests;

public sealed class TaskOnUserControllerTests(WorthBoardsApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task LinkUsersToTask_WithEditorToken_StoresAndRemovesAssignments()
    {
        // Arrange
        var client = await CreateAuthenticatedClientAsync("editor");
        var linkRequests = new[]
        {
            new LinkUserToTaskRequest { UserId = Seed.ViewerUserId },
            new LinkUserToTaskRequest { UserId = Seed.OutsiderUserId }
        };

        // Act
        var linkResponse = await client.PostAsJsonAsync($"/api/boards/{Seed.BoardId}/tasks/{Seed.TaskId}/users/link", linkRequests);

        // Assert
        linkResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var linkBody = await ReadJsonAsync<List<LinkUserToTaskResponse>>(linkResponse);
        linkBody.Should().HaveCount(2);

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
            (await context.TasksOnUsers.CountAsync(link => link.BoardTaskId == Seed.TaskId)).Should().Be(2);
        }

        var getResponse = await client.GetAsync($"/api/boards/{Seed.BoardId}/tasks/{Seed.TaskId}/users");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getBody = await ReadJsonAsync<List<LinkedUserToTaskResponse>>(getResponse);
        getBody.Should().HaveCount(2);

        var unlinkResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/boards/{Seed.BoardId}/tasks/{Seed.TaskId}/users/unlink")
        {
            Content = JsonContent.Create(linkRequests)
        });
        unlinkResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var unlinkBody = await ReadJsonAsync<List<LinkUserToTaskResponse>>(unlinkResponse);
        unlinkBody.Should().HaveCount(2);

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
            (await context.TasksOnUsers.AnyAsync(link => link.BoardTaskId == Seed.TaskId)).Should().BeFalse();
        }
    }

    [Fact]
    public async Task LinkUsersToTask_WithUnknownTaskId_ReturnsNotFoundAndDoesNotCreateAssignments()
    {
        // Arrange
        var client = await CreateAuthenticatedClientAsync("editor");

        // Act
        var response = await client.PostAsJsonAsync($"/api/boards/{Seed.BoardId}/tasks/999999/users/link", new[]
        {
            new LinkUserToTaskRequest { UserId = Seed.ViewerUserId }
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await ReadJsonAsync<ErrorDetails>(response);
        error.Should().NotBeNull();
        error!.StatusCode.Should().Be((int)HttpStatusCode.NotFound);

        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        (await context.TasksOnUsers.AnyAsync(link => link.BoardTaskId == Seed.TaskId)).Should().BeFalse();
    }
}