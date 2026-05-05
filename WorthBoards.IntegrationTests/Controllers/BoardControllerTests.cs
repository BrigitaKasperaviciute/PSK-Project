using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.Data.Database;
using WorthBoards.IntegrationTests.Infrastructure;
using Xunit;

namespace WorthBoards.IntegrationTests.Controllers;

[Collection(nameof(IntegrationCollection))]
public sealed class BoardControllerTests(IntegrationTestAppFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Create_WithValidPayload_ReturnsCreatedAndPersistsOwnerLink()
    {
        // Arrange
        var owner = await Factory.CreateUserAsync("Eva", "Green", "board-create");
        var client = Factory.CreateAuthenticatedClient(owner);
        var request = new BoardRequest
        {
            Title = "Product launch",
            Description = "Track launch work",
            ImageName = "launch.png"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/boards", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var body = await response.Content.ReadFromJsonAsync<BoardResponse>();
        body.Should().NotBeNull();
        body!.Title.Should().Be(request.Title);
        body.Description.Should().Be(request.Description);
        body.ImageURL.Should().Be("http://localhost:5000/images/launch.png");

        await Factory.WithDbContextAsync(async db =>
        {
            var persistedBoard = await db.Boards.AsNoTracking().SingleAsync(item => item.Id == body.Id);
            persistedBoard.Title.Should().Be(request.Title);
            persistedBoard.Description.Should().Be(request.Description);

            var ownerLink = await db.BoardOnUsers.AsNoTracking().SingleAsync(item => item.BoardId == body.Id && item.UserId == owner.Id);
            ownerLink.UserRole.Should().Be(UserRoleEnum.OWNER);
        });
    }

    [Fact]
    public async Task Delete_WithViewerRole_ReturnsForbiddenAndLeavesBoardIntact()
    {
        // Arrange
        var owner = await Factory.CreateUserAsync("Frank", "Stone", "board-delete-owner");
        var viewer = await Factory.CreateUserAsync("Grace", "Lane", "board-delete-viewer");
        var board = await Factory.SeedBoardAsync(owner.Id, "Migration board", "Delete guard board");
        await Factory.SeedBoardUserAsync(board.Id, viewer.Id, UserRoleEnum.VIEWER);
        var client = Factory.CreateAuthenticatedClient(viewer);

        // Act
        var response = await client.DeleteAsync($"/api/boards/{board.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await Factory.WithDbContextAsync(async db =>
        {
            var persistedBoard = await db.Boards.AsNoTracking().SingleAsync(item => item.Id == board.Id);
            persistedBoard.Title.Should().Be(board.Title);

            var links = await db.BoardOnUsers.AsNoTracking().Where(item => item.BoardId == board.Id).ToListAsync();
            links.Should().HaveCount(2);
        });
    }

    [Fact]
    public async Task GetUpdatePatchDelete_WithOwnerRole_ExercisesBoardLifecycle()
    {
        // Arrange
        var owner = await Factory.CreateUserAsync("Helen", "Quill", "board-lifecycle");
        var client = Factory.CreateAuthenticatedClient(owner);

        var firstBoardResponse = await client.PostAsJsonAsync("/api/boards", new BoardRequest
        {
            Title = "Quarterly planning",
            Description = "Board one",
            ImageName = "planning.png"
        });
        firstBoardResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstBoard = await firstBoardResponse.Content.ReadFromJsonAsync<BoardResponse>();
        firstBoard.Should().NotBeNull();

        var secondBoardResponse = await client.PostAsJsonAsync("/api/boards", new BoardRequest
        {
            Title = "Support planning",
            Description = "Board two",
            ImageName = "support.png"
        });
        secondBoardResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var secondBoard = await secondBoardResponse.Content.ReadFromJsonAsync<BoardResponse>();
        secondBoard.Should().NotBeNull();

        // Act - list boards
        var listResponse = await client.GetAsync("/api/boards");

        // Assert - list boards
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var listBody = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        listBody.GetProperty("items").EnumerateArray().Should().HaveCount(2);

        // Act - get by id
        var getResponse = await client.GetAsync($"/api/boards/{firstBoard!.Id}");

        // Assert - get by id
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var getBody = await getResponse.Content.ReadFromJsonAsync<BoardResponse>();
        getBody.Should().NotBeNull();
        getBody!.Id.Should().Be(firstBoard.Id);

        // Act - update
        var updateRequest = new BoardUpdateRequest
        {
            Title = "Quarterly planning updated",
            Description = "Board one updated",
            ImageName = "planning-updated.png",
            Version = getBody.Version
        };
        var updateResponse = await client.PutAsJsonAsync($"/api/boards/{firstBoard.Id}", updateRequest);

        // Assert - update
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedBoard = await updateResponse.Content.ReadFromJsonAsync<BoardResponse>();
        updatedBoard.Should().NotBeNull();
        updatedBoard!.Title.Should().Be(updateRequest.Title);
        updatedBoard.Description.Should().Be(updateRequest.Description);

        // Act - patch
        var patchOperations = new[]
        {
            new Dictionary<string, object?> { ["op"] = "replace", ["path"] = "/title", ["value"] = "Quarterly planning patched" },
            new Dictionary<string, object?> { ["op"] = "replace", ["path"] = "/version", ["value"] = updatedBoard!.Version }
        };
        var patchContent = new StringContent(JsonSerializer.Serialize(patchOperations), Encoding.UTF8, "application/json-patch+json");
        var patchResponse = await client.PatchAsync($"/api/boards/{firstBoard.Id}", patchContent);

        // Assert - patch
        patchResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var patchedBoard = await patchResponse.Content.ReadFromJsonAsync<BoardResponse>();
        patchedBoard.Should().NotBeNull();
        patchedBoard!.Title.Should().Be("Quarterly planning patched");

        // Act - delete
        var deleteResponse = await client.DeleteAsync($"/api/boards/{firstBoard.Id}");

        // Assert - delete
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await Factory.WithDbContextAsync(async db =>
        {
            var deletedBoard = await db.Boards.AsNoTracking().SingleOrDefaultAsync(item => item.Id == firstBoard.Id);
            deletedBoard.Should().BeNull();

            var remainingBoard = await db.Boards.AsNoTracking().SingleAsync(item => item.Id == secondBoard!.Id);
            remainingBoard.Title.Should().Be(secondBoard.Title);
        });
    }
}