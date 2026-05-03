using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Exceptions;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests;

public sealed class BoardControllerTests(WorthBoardsApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task CreateBoard_WithValidPayload_PerformsFullBoardLifecycle()
    {
        // Arrange
        var client = await CreateAuthenticatedClientAsync("owner");
        var createRequest = new BoardRequest
        {
            Title = "Integration board",
            Description = "Board created by the integration test",
            ImageName = "integration-board.png"
        };

        // Act
        var createResponse = await client.PostAsJsonAsync("/api/boards", createRequest);

        // Assert
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdBoard = await ReadJsonAsync<BoardResponse>(createResponse);
        createdBoard.Should().NotBeNull();
        createdBoard!.Title.Should().Be(createRequest.Title);
        createdBoard.Description.Should().Be(createRequest.Description);

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
            var storedBoard = await context.Boards.FindAsync(createdBoard.Id);
            storedBoard.Should().NotBeNull();
            storedBoard!.Title.Should().Be(createRequest.Title);
            context.BoardOnUsers.Should().ContainSingle(link => link.BoardId == createdBoard.Id && link.UserRole == WorthBoards.Common.Enums.UserRoleEnum.OWNER);
        }

        var listResponse = await client.GetAsync("/api/boards");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var listBody = await listResponse.Content.ReadAsStringAsync();
        listBody.Should().Contain(createRequest.Title);

        var getResponse = await client.GetAsync($"/api/boards/{createdBoard.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updateRequest = new BoardUpdateRequest
        {
            Title = "Updated integration board",
            Description = "Updated description",
            ImageName = "updated-board.png",
            Version = createdBoard.Version
        };
        var updateResponse = await client.PutAsJsonAsync($"/api/boards/{createdBoard.Id}", updateRequest);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var patchDocument = new JsonPatchDocument<BoardUpdateRequest>();
        patchDocument.Replace(board => board.Title, "Patched integration board");
        var patchRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/boards/{createdBoard.Id}")
        {
            Content = new StringContent(JsonConvert.SerializeObject(patchDocument), Encoding.UTF8, "application/json-patch+json")
        };
        var patchResponse = await client.SendAsync(patchRequest);
        patchResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var patchBody = await ReadJsonAsync<BoardResponse>(patchResponse);
        patchBody.Should().NotBeNull();
        patchBody!.Title.Should().Be("Patched integration board");

        var deleteResponse = await client.DeleteAsync($"/api/boards/{createdBoard.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
            (await context.Boards.FindAsync(createdBoard.Id)).Should().BeNull();
        }
    }

    [Fact]
    public async Task GetBoardById_WithUnknownBoardId_ReturnsNotFoundAndKeepsStoredBoards()
    {
        // Arrange
        var client = await CreateAuthenticatedClientAsync("owner");

        // Act
        var response = await client.GetAsync("/api/boards/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await ReadJsonAsync<ErrorDetails>(response);
        error.Should().NotBeNull();
        error!.StatusCode.Should().Be((int)HttpStatusCode.NotFound);

        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        (await context.Boards.CountAsync()).Should().BeGreaterThan(0);
    }
}