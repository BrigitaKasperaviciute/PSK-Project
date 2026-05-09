using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.JsonPatch;
using Newtonsoft.Json;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.Data.Database;
using WorthBoards.Data.Identity;
using WorthBoards.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace WorthBoards.IntegrationTests.Controllers;

// Helper record to deserialize paged responses from BoardController
file record PagedResponse<T>
{
    [JsonProperty("items")]
    public IEnumerable<T>? Data { get; set; }
    
    [JsonProperty("pageNumber")]
    public int PageNumber { get; set; }
    
    [JsonProperty("pageCount")]
    public int PageCount { get; set; }
    
    [JsonProperty("pageSize")]
    public int PageSize { get; set; }
}

[Collection(nameof(ApiCollection))]
public class BoardControllerIntegrationTests
{
    private readonly ApiFactory _factory;

    public BoardControllerIntegrationTests(ApiFactory factory)
    {
        _factory = factory;
    }

    #region GetAllCurrentUserBoards Tests

    [Fact]
    public async Task GetAllCurrentUserBoards_WithValidUser_ReturnsOkAndBoardList()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1, "owner@test.com");
        var client = _factory.CreateClientForUser(owner.Id, "OWNER");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        // Create test boards
        var board1 = await helper.CreateBoardAsync(owner.Id, "Board 1", "Description 1");
        var board2 = await helper.CreateBoardAsync(owner.Id, "Board 2", "Description 2");

        // Act
        var response = await client.GetAsync("/api/boards?pageNum=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var boards = JsonConvert.DeserializeObject<PagedResponse<BoardResponse>>(content);
        boards.Should().NotBeNull();
        boards!.Data.Should().HaveCount(2);
        boards.Data.Should().Contain(b => b.Title == "Board 1");
        boards.Data.Should().Contain(b => b.Title == "Board 2");

        dbContext.Dispose();
    }

    [Fact]
    public async Task GetAllCurrentUserBoards_WithPagination_ReturnsPaginatedResults()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1, "owner@test.com");
        var client = _factory.CreateClientForUser(owner.Id, "OWNER");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        // Create multiple boards
        for (int i = 0; i < 15; i++)
        {
            await helper.CreateBoardAsync(owner.Id, $"Board {i}", $"Description {i}");
        }

        // Act
        var response1 = await client.GetAsync("/api/boards?pageNum=0&pageSize=10");
        var response2 = await client.GetAsync("/api/boards?pageNum=1&pageSize=10");

        // Assert
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);

        var page1 = JsonConvert.DeserializeObject<PagedResponse<BoardResponse>>(
            await response1.Content.ReadAsStringAsync());
        var page2 = JsonConvert.DeserializeObject<PagedResponse<BoardResponse>>(
            await response2.Content.ReadAsStringAsync());

        page1!.Data.Should().HaveCount(10);
        page2!.Data.Should().HaveCount(5);

        dbContext.Dispose();
    }

    [Fact]
    public async Task GetAllCurrentUserBoards_WithoutAuthorization_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/boards?pageNum=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region GetBoardById Tests

    [Fact]
    public async Task GetBoardById_WithValidBoardId_ReturnsOkAndBoardData()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1, "owner@test.com");
        var (viewer, _) = await CreateUserAsync(2, "viewer@test.com");
        var client = _factory.CreateClientForUser(viewer.Id, "VIEWER");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id, "Test Board", "Test Description");
        await helper.AddUserToBoardAsync(board.Id, viewer.Id, UserRoleEnum.VIEWER);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var boardResponse = JsonConvert.DeserializeObject<BoardResponse>(content);
        boardResponse.Should().NotBeNull();
        boardResponse!.Id.Should().Be(board.Id);
        boardResponse.Title.Should().Be("Test Board");

        dbContext.Dispose();
    }

    [Fact]
    public async Task GetBoardById_WithNonexistentBoard_ReturnsNotFound()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (user, _) = await CreateUserAsync(1, "user@test.com");
        var client = _factory.CreateClientForUser(user.Id, "VIEWER");

        // Act
        var response = await client.GetAsync("/api/boards/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetBoardById_WithUnauthorizedUser_ReturnsForbidden()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1, "owner@test.com");
        var (outsider, _) = await CreateUserAsync(2, "outsider@test.com");
        var client = _factory.CreateClientForUser(outsider.Id, "VIEWER");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id, "Private Board", "Private");

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        dbContext.Dispose();
    }

    #endregion

    #region CreateBoard Tests

    [Fact]
    public async Task CreateBoard_WithValidData_ReturnsCreatedAndBoardData()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1, "owner@test.com");
        var client = _factory.CreateClientForUser(owner.Id, "OWNER");

        var createRequest = new BoardRequestBuilder()
            .WithTitle("New Board")
            .WithDescription("New Board Description")
            .Build();

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(createRequest),
            Encoding.UTF8,
            "application/json");
        var response = await client.PostAsync("/api/boards", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var responseContent = await response.Content.ReadAsStringAsync();
        var boardResponse = JsonConvert.DeserializeObject<BoardResponse>(responseContent);
        boardResponse.Should().NotBeNull();
        boardResponse!.Title.Should().Be("New Board");
        boardResponse.Description.Should().Be("New Board Description");

        // Verify database persistence
        var dbContext = _factory.CreateDbContext();
        var savedBoard = dbContext.Boards.FirstOrDefault(b => b.Title == "New Board");
        savedBoard.Should().NotBeNull();

        dbContext.Dispose();
    }

    [Fact]
    public async Task CreateBoard_WithMissingTitle_ReturnsBadRequest()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1, "owner@test.com");
        var client = _factory.CreateClientForUser(owner.Id, "OWNER");

        var createRequest = new { Description = "Description", ImageName = "test.png" };

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(createRequest),
            Encoding.UTF8,
            "application/json");
        var response = await client.PostAsync("/api/boards", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateBoard_WithoutAuthorization_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        var createRequest = new BoardRequestBuilder().Build();

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(createRequest),
            Encoding.UTF8,
            "application/json");
        var response = await client.PostAsync("/api/boards", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region UpdateBoard Tests

    [Fact]
    public async Task UpdateBoard_WithValidData_ReturnsOkAndUpdatedBoard()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1, "owner@test.com");
        var client = _factory.CreateClientForUser(owner.Id, "EDITOR");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id, "Original Board", "Original Description");
        await helper.AddUserToBoardAsync(board.Id, owner.Id, UserRoleEnum.EDITOR);

        var updateRequest = new BoardUpdateRequestBuilder()
            .WithTitle("Updated Board")
            .WithDescription("Updated Description")
            .WithVersion(board.Version)
            .Build();

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(updateRequest),
            Encoding.UTF8,
            "application/json");
        var response = await client.PutAsync($"/api/boards/{board.Id}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();
        var boardResponse = JsonConvert.DeserializeObject<BoardResponse>(responseContent);
        boardResponse.Should().NotBeNull();
        boardResponse!.Title.Should().Be("Updated Board");

        dbContext.Dispose();
    }

    [Fact]
    public async Task UpdateBoard_WithInvalidVersion_ReturnsBadRequest()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1, "owner@test.com");
        var client = _factory.CreateClientForUser(owner.Id, "EDITOR");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id, "Test Board", "Description");
        await helper.AddUserToBoardAsync(board.Id, owner.Id, UserRoleEnum.EDITOR);

        var updateRequest = new BoardUpdateRequestBuilder()
            .WithVersion(999) // Wrong version
            .Build();

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(updateRequest),
            Encoding.UTF8,
            "application/json");
        var response = await client.PutAsync($"/api/boards/{board.Id}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        dbContext.Dispose();
    }

    [Fact]
    public async Task UpdateBoard_WithoutEditorRole_ReturnsForbidden()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1, "owner@test.com");
        var (viewer, _) = await CreateUserAsync(2, "viewer@test.com");
        var client = _factory.CreateClientForUser(viewer.Id, "VIEWER");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id, "Test Board", "Description");
        await helper.AddUserToBoardAsync(board.Id, viewer.Id, UserRoleEnum.VIEWER);

        var updateRequest = new BoardUpdateRequestBuilder()
            .WithVersion(board.Version)
            .Build();

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(updateRequest),
            Encoding.UTF8,
            "application/json");
        var response = await client.PutAsync($"/api/boards/{board.Id}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        dbContext.Dispose();
    }

    #endregion

    #region PatchBoard Tests

    [Fact]
    public async Task PatchBoard_WithValidPatch_ReturnsOkAndAppliesChanges()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1, "owner@test.com");
        var client = _factory.CreateClientForUser(owner.Id, "EDITOR");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id, "Original Board", "Original Description");
        await helper.AddUserToBoardAsync(board.Id, owner.Id, UserRoleEnum.EDITOR);

        var patchDoc = new Microsoft.AspNetCore.JsonPatch.JsonPatchDocument<BoardUpdateRequest>();
        patchDoc.Replace(x => x.Title, "Patched Board");

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(patchDoc),
            Encoding.UTF8,
            "application/json-patch+json");
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json-patch+json");
        var response = await client.PatchAsync($"/api/boards/{board.Id}", content);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body
        var responseContent = await response.Content.ReadAsStringAsync();
        var boardResponse = JsonConvert.DeserializeObject<BoardResponse>(responseContent);
        boardResponse.Should().NotBeNull();
        boardResponse!.Title.Should().Be("Patched Board");
        boardResponse.Description.Should().Be("Original Description"); // Should remain unchanged

        // Assert - database state
        using var assertionContext = _factory.CreateDbContext();
        var patchedBoard = assertionContext.Boards.AsNoTracking().FirstOrDefault(b => b.Id == board.Id);
        patchedBoard!.Title.Should().Be("Patched Board");

        dbContext.Dispose();
    }

    [Fact]
    public async Task PatchBoard_PatchMultipleFields_ReturnsOkAndAppliesAllChanges()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1, "owner@test.com");
        var client = _factory.CreateClientForUser(owner.Id, "EDITOR");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id, "Original", "Original Desc");
        await helper.AddUserToBoardAsync(board.Id, owner.Id, UserRoleEnum.EDITOR);

        var patchDoc = new Microsoft.AspNetCore.JsonPatch.JsonPatchDocument<BoardUpdateRequest>();
        patchDoc.Replace(x => x.Title, "New Title");
        patchDoc.Replace(x => x.Description, "New Description");

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(patchDoc),
            Encoding.UTF8,
            "application/json-patch+json");
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json-patch+json");
        var response = await client.PatchAsync($"/api/boards/{board.Id}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();
        var boardResponse = JsonConvert.DeserializeObject<BoardResponse>(responseContent);
        boardResponse!.Title.Should().Be("New Title");
        boardResponse.Description.Should().Be("New Description");

        dbContext.Dispose();
    }

    [Fact]
    public async Task PatchBoard_WithoutEditorRole_ReturnsForbidden()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1, "owner@test.com");
        var (viewer, _) = await CreateUserAsync(2, "viewer@test.com");
        var client = _factory.CreateClientForUser(viewer.Id, "VIEWER");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id, "Board", "Description");
        await helper.AddUserToBoardAsync(board.Id, viewer.Id, UserRoleEnum.VIEWER);

        var patchDoc = new Microsoft.AspNetCore.JsonPatch.JsonPatchDocument<BoardUpdateRequest>();
        patchDoc.Replace(x => x.Title, "New Title");

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(patchDoc),
            Encoding.UTF8,
            "application/json-patch+json");
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json-patch+json");
        var response = await client.PatchAsync($"/api/boards/{board.Id}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        dbContext.Dispose();
    }

    [Fact]
    public async Task PatchBoard_WithInvalidPatchDocument_ReturnsBadRequest()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1, "owner@test.com");
        var client = _factory.CreateClientForUser(owner.Id, "EDITOR");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id, "Board", "Description");
        await helper.AddUserToBoardAsync(board.Id, owner.Id, UserRoleEnum.EDITOR);

        // Act
        var content = new StringContent(
            "invalid json patch",
            Encoding.UTF8,
            "application/json-patch+json");
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json-patch+json");
        var response = await client.PatchAsync($"/api/boards/{board.Id}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        dbContext.Dispose();
    }

    [Fact]
    public async Task PatchBoard_OnNonexistentBoard_ReturnsNotFound()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1, "owner@test.com");
        var client = _factory.CreateClientForUser(owner.Id, "EDITOR");

        var patchDoc = new Microsoft.AspNetCore.JsonPatch.JsonPatchDocument<BoardUpdateRequest>();
        patchDoc.Replace(x => x.Title, "New Title");

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(patchDoc),
            Encoding.UTF8,
            "application/json-patch+json");
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json-patch+json");
        var response = await client.PatchAsync($"/api/boards/99999", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region DeleteBoard Tests

    [Fact]
    public async Task DeleteBoard_WithValidBoardAsOwner_ReturnsNoContent()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1, "owner@test.com");
        var client = _factory.CreateClientForUser(owner.Id, "OWNER");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id, "Board to Delete", "Description");

        // Act
        var response = await client.DeleteAsync($"/api/boards/{board.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify deletion
        var deletedBoard = dbContext.Boards.FirstOrDefault(b => b.Id == board.Id);
        deletedBoard.Should().BeNull();

        dbContext.Dispose();
    }

    [Fact]
    public async Task DeleteBoard_WithoutOwnerRole_ReturnsForbidden()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1, "owner@test.com");
        var (editor, _) = await CreateUserAsync(2, "editor@test.com");
        var client = _factory.CreateClientForUser(editor.Id, "EDITOR");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id, "Board", "Description");
        await helper.AddUserToBoardAsync(board.Id, editor.Id, UserRoleEnum.EDITOR);

        // Act
        var response = await client.DeleteAsync($"/api/boards/{board.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        dbContext.Dispose();
    }

    [Fact]
    public async Task DeleteBoard_WithNonexistentBoard_ReturnsNotFound()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1, "owner@test.com");
        var client = _factory.CreateClientForUser(owner.Id, "OWNER");

        // Act
        var response = await client.DeleteAsync("/api/boards/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region Helper Methods

    private async Task<(ApplicationUser user, string token)> CreateUserAsync(
        int id,
        string email)
    {
        var dbContext = _factory.CreateDbContext();
        var userManager = GetUserManager(dbContext);

        var user = new ApplicationUserBuilder()
            .WithEmail(email)
            .Build();

        var result = await userManager.CreateAsync(user, "TestPassword123!");
        if (!result.Succeeded)
            throw new InvalidOperationException("Failed to create test user");

        var token = TestJwtTokenGenerator.GenerateToken(user.Id, "OWNER");
        dbContext.Dispose();

        return (user, token);
    }

    private UserManager<ApplicationUser> GetUserManager(ApplicationDbContext dbContext)
    {
        return _factory.GetUserManager(dbContext);
    }

    #endregion
}
