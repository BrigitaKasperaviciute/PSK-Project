using System.Net;
using System.Net.Http.Json;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Controllers;

public class BoardControllerTests : IntegrationTestBase
{
    public BoardControllerTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetBoards_Authenticated_ReturnsOk()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);

        var response = await client.GetAsync("/api/boards");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetBoards_Unauthenticated_ReturnsUnauthorized()
    {
        var response = await CreateAnonymousClient().GetAsync("/api/boards");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetBoardById_ExistingBoard_ReturnsOk()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var board = await CreateBoardAsync(client);

        var response = await client.GetAsync($"/api/boards/{board.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BoardResponse>();
        body!.Id.Should().Be(board.Id);
    }

    [Fact]
    public async Task GetBoardById_NonExistingBoard_ReturnsNotFound()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);

        var response = await client.GetAsync("/api/boards/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateBoard_ValidRequest_ReturnsCreated()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);

        var request = new BoardRequest { Title = "My Board", Description = "Test desc", ImageName = "img.jpg" };
        var response = await client.PostAsJsonAsync("/api/boards", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<BoardResponse>();
        body!.Title.Should().Be("My Board");
    }

    [Fact]
    public async Task CreateBoard_Unauthenticated_ReturnsUnauthorized()
    {
        var request = new BoardRequest { Title = "Board", Description = "Desc", ImageName = "img.jpg" };
        var response = await CreateAnonymousClient().PostAsJsonAsync("/api/boards", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteBoard_AsOwner_ReturnsNoContent()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var board = await CreateBoardAsync(client);

        var response = await client.DeleteAsync($"/api/boards/{board.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteBoard_AsNonMember_ReturnsForbidden()
    {
        var (ownerToken, _) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(CreateAuthenticatedClient(ownerToken));

        var (otherToken, _) = await RegisterAndLoginAsync();
        var response = await CreateAuthenticatedClient(otherToken).DeleteAsync($"/api/boards/{board.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateBoard_AsOwner_ReturnsOk()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var board = await CreateBoardAsync(client);

        var updateRequest = new BoardUpdateRequest
        {
            Title = "Updated Title",
            Description = "Updated desc",
            ImageName = "new.jpg",
            Version = board.Version
        };
        var response = await client.PutAsJsonAsync($"/api/boards/{board.Id}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BoardResponse>();
        body!.Title.Should().Be("Updated Title");
    }

    [Fact]
    public async Task UpdateBoard_VersionMismatch_ReturnsConflict()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var board = await CreateBoardAsync(client);

        var updateRequest = new BoardUpdateRequest
        {
            Title = "Bad Version",
            Description = "Desc",
            ImageName = "img.jpg",
            Version = board.Version + 99
        };
        var response = await client.PutAsJsonAsync($"/api/boards/{board.Id}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PatchBoard_AsOwner_ReturnsOk()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var board = await CreateBoardAsync(client);

        var patchDoc = new[]
        {
            new { op = "replace", path = "/title", value = (object)"Patched Title" },
            new { op = "replace", path = "/version", value = (object)board.Version }
        };
        var response = await client.PatchAsJsonAsync($"/api/boards/{board.Id}", patchDoc);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PatchBoard_AsNonMember_ReturnsForbidden()
    {
        var (ownerToken, _) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(CreateAuthenticatedClient(ownerToken));

        var (otherToken, _) = await RegisterAndLoginAsync();
        var patchDoc = new[] { new { op = "replace", path = "/title", value = "Hacked" } };
        var response = await CreateAuthenticatedClient(otherToken)
            .PatchAsJsonAsync($"/api/boards/{board.Id}", patchDoc);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
