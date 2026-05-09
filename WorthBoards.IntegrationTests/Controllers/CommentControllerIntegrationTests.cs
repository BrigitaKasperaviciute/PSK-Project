using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Newtonsoft.Json;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.Data.Database;
using WorthBoards.Data.Identity;
using WorthBoards.IntegrationTests.Infrastructure;
using Xunit;

namespace WorthBoards.IntegrationTests.Controllers;

// Helper record to deserialize paged responses from CommentController
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
public class CommentControllerIntegrationTests
{
    private readonly ApiFactory _factory;

    public CommentControllerIntegrationTests(ApiFactory factory)
    {
        _factory = factory;
    }

    #region GetAllBoardTaskComments Tests

    [Fact]
    public async Task GetAllBoardTaskComments_WithValidTask_ReturnsOkAndCommentList()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1);
        var (commenter, _) = await CreateUserAsync(2);
        var client = _factory.CreateClientForUser(owner.Id, "VIEWER");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id);
        var task = await helper.CreateBoardTaskAsync(board.Id);
        var comment1 = await helper.CreateCommentAsync(task.Id, owner.Id, "First comment");
        var comment2 = await helper.CreateCommentAsync(task.Id, commenter.Id, "Second comment");

        // Act
        var response = await client.GetAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments?pageNum=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var pagedResponse = JsonConvert.DeserializeObject<PagedResponse<CommentResponse>>(content);
        pagedResponse.Should().NotBeNull();
        pagedResponse!.Data.Should().HaveCount(2);
        pagedResponse.Data.Should().Contain(c => c.Content == "First comment");
        pagedResponse.Data.Should().Contain(c => c.Content == "Second comment");

        dbContext.Dispose();
    }

    [Fact]
    public async Task GetAllBoardTaskComments_WithPagination_ReturnsPaginatedResults()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1);
        var client = _factory.CreateClientForUser(owner.Id, "VIEWER");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id);
        var task = await helper.CreateBoardTaskAsync(board.Id);

        // Create 15 comments
        for (int i = 0; i < 15; i++)
        {
            await helper.CreateCommentAsync(task.Id, owner.Id, $"Comment {i}");
        }

        // Act
        var response1 = await client.GetAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments?pageNum=0&pageSize=10");
        var response2 = await client.GetAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments?pageNum=1&pageSize=10");

        // Assert
        var page1 = JsonConvert.DeserializeObject<PagedResponse<CommentResponse>>(
            await response1.Content.ReadAsStringAsync());
        var page2 = JsonConvert.DeserializeObject<PagedResponse<CommentResponse>>(
            await response2.Content.ReadAsStringAsync());

        page1!.Data.Should().HaveCount(10);
        page2!.Data.Should().HaveCount(5);

        dbContext.Dispose();
    }

    [Fact]
    public async Task GetAllBoardTaskComments_WithoutAuthorization_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/boards/1/tasks/1/comments?pageNum=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAllBoardTaskComments_WithUnauthorizedUser_ReturnsForbidden()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1);
        var (outsider, _) = await CreateUserAsync(2);
        var client = _factory.CreateClientForUser(outsider.Id, "VIEWER");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id);
        var task = await helper.CreateBoardTaskAsync(board.Id);

        // Act
        var response = await client.GetAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments?pageNum=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        dbContext.Dispose();
    }

    #endregion

    #region GetCommentById Tests

    [Fact]
    public async Task GetCommentById_WithValidIds_ReturnsOkAndCommentData()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1);
        var client = _factory.CreateClientForUser(owner.Id, "VIEWER");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id);
        var task = await helper.CreateBoardTaskAsync(board.Id);
        var comment = await helper.CreateCommentAsync(task.Id, owner.Id, "Test comment");

        // Act
        var response = await client.GetAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var commentResponse = JsonConvert.DeserializeObject<CommentResponse>(content);
        commentResponse.Should().NotBeNull();
        commentResponse!.Content.Should().Be("Test comment");
        commentResponse.Edited.Should().BeFalse();

        dbContext.Dispose();
    }

    [Fact]
    public async Task GetCommentById_WithNonexistentComment_ReturnsNotFound()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1);
        var client = _factory.CreateClientForUser(owner.Id, "VIEWER");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id);
        var task = await helper.CreateBoardTaskAsync(board.Id);

        // Act
        var response = await client.GetAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        dbContext.Dispose();
    }

    #endregion

    #region CreateComment Tests

    [Fact]
    public async Task CreateComment_WithValidData_ReturnsCreatedAndCommentData()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1);
        var client = _factory.CreateClientForUser(owner.Id, "VIEWER");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id);
        var task = await helper.CreateBoardTaskAsync(board.Id);

        var createRequest = new CommentRequestBuilder()
            .WithContent("This is a new comment")
            .Build();

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(createRequest),
            Encoding.UTF8,
            "application/json");
        var response = await client.PostAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var responseContent = await response.Content.ReadAsStringAsync();
        var commentResponse = JsonConvert.DeserializeObject<CommentResponse>(responseContent);
        commentResponse.Should().NotBeNull();
        commentResponse!.Content.Should().Be("This is a new comment");
        commentResponse.UserId.Should().Be(owner.Id);

        dbContext.Dispose();
    }

    [Fact]
    public async Task CreateComment_WithEmptyContent_ReturnsBadRequest()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1);
        var client = _factory.CreateClientForUser(owner.Id, "VIEWER");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id);
        var task = await helper.CreateBoardTaskAsync(board.Id);

        var createRequest = new { Content = "" };

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(createRequest),
            Encoding.UTF8,
            "application/json");
        var response = await client.PostAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        dbContext.Dispose();
    }

    [Fact]
    public async Task CreateComment_WithoutAuthorization_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        var createRequest = new CommentRequestBuilder().Build();

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(createRequest),
            Encoding.UTF8,
            "application/json");
        var response = await client.PostAsync("/api/boards/1/tasks/1/comments", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region UpdateComment Tests

    [Fact]
    public async Task UpdateComment_WithValidData_ReturnsOkAndUpdatedComment()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1);
        var client = _factory.CreateClientForUser(owner.Id, "VIEWER");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id);
        var task = await helper.CreateBoardTaskAsync(board.Id);
        var comment = await helper.CreateCommentAsync(task.Id, owner.Id, "Original comment");

        var updateRequest = new CommentUpdateRequestBuilder()
            .WithContent("Updated comment")
            .WithVersion(comment.Version)
            .Build();

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(updateRequest),
            Encoding.UTF8,
            "application/json");
        var response = await client.PutAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();
        var commentResponse = JsonConvert.DeserializeObject<CommentResponse>(responseContent);
        commentResponse.Should().NotBeNull();
        commentResponse!.Content.Should().Be("Updated comment");
        commentResponse.Edited.Should().BeTrue();

        dbContext.Dispose();
    }

    [Fact]
    public async Task UpdateComment_WithInvalidVersion_ReturnsBadRequest()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1);
        var client = _factory.CreateClientForUser(owner.Id, "VIEWER");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id);
        var task = await helper.CreateBoardTaskAsync(board.Id);
        var comment = await helper.CreateCommentAsync(task.Id, owner.Id, "Comment");

        var updateRequest = new CommentUpdateRequestBuilder()
            .WithVersion(999) // Wrong version
            .Build();

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(updateRequest),
            Encoding.UTF8,
            "application/json");
        var response = await client.PutAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        dbContext.Dispose();
    }

    [Fact]
    public async Task UpdateComment_ByDifferentUser_ReturnsForbidden()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1);
        var (otherUser, _) = await CreateUserAsync(2);
        var client = _factory.CreateClientForUser(otherUser.Id, "VIEWER");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id);
        await helper.AddUserToBoardAsync(board.Id, otherUser.Id, UserRoleEnum.VIEWER);
        var task = await helper.CreateBoardTaskAsync(board.Id);
        var comment = await helper.CreateCommentAsync(task.Id, owner.Id, "Comment");

        var updateRequest = new CommentUpdateRequestBuilder()
            .WithVersion(comment.Version)
            .Build();

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(updateRequest),
            Encoding.UTF8,
            "application/json");
        var response = await client.PutAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        dbContext.Dispose();
    }

    #endregion

    #region DeleteComment Tests

    [Fact]
    public async Task DeleteComment_ByCommentOwner_ReturnsNoContent()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1);
        var client = _factory.CreateClientForUser(owner.Id, "VIEWER");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id);
        var task = await helper.CreateBoardTaskAsync(board.Id);
        var comment = await helper.CreateCommentAsync(task.Id, owner.Id, "Comment to delete");

        // Act
        var response = await client.DeleteAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify deletion
        var deletedComment = dbContext.Comments.FirstOrDefault(c => c.Id == comment.Id);
        deletedComment.Should().BeNull();

        dbContext.Dispose();
    }

    [Fact]
    public async Task DeleteComment_ByDifferentUser_ReturnsForbidden()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1);
        var (otherUser, _) = await CreateUserAsync(2);
        var client = _factory.CreateClientForUser(otherUser.Id, "VIEWER");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id);
        await helper.AddUserToBoardAsync(board.Id, otherUser.Id, UserRoleEnum.VIEWER);
        var task = await helper.CreateBoardTaskAsync(board.Id);
        var comment = await helper.CreateCommentAsync(task.Id, owner.Id, "Comment");

        // Act
        var response = await client.DeleteAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        dbContext.Dispose();
    }

    [Fact]
    public async Task DeleteComment_WithNonexistentComment_ReturnsNotFound()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1);
        var client = _factory.CreateClientForUser(owner.Id, "VIEWER");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id);
        var task = await helper.CreateBoardTaskAsync(board.Id);

        // Act
        var response = await client.DeleteAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        dbContext.Dispose();
    }

    #endregion

    #region Helper Methods

    private async Task<(ApplicationUser user, string token)> CreateUserAsync(int id)
    {
        var dbContext = _factory.CreateDbContext();
        var userManager = GetUserManager(dbContext);

        var user = new ApplicationUserBuilder()
            .WithEmail($"user{id}@test.com")
            .Build();

        var result = await userManager.CreateAsync(user, "TestPassword123!");
        if (!result.Succeeded)
            throw new InvalidOperationException("Failed to create test user");

        var token = TestJwtTokenGenerator.GenerateToken(user.Id, "VIEWER");
        dbContext.Dispose();

        return (user, token);
    }

    private UserManager<ApplicationUser> GetUserManager(ApplicationDbContext dbContext)
    {
        return _factory.GetUserManager(dbContext);
    }

    #endregion
}
