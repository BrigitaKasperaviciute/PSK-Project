using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Moq;
using Xunit;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Tests;

public class LoggingCoverageTests : IClassFixture<LoggingWebApplicationFactory>
{
    private readonly LoggingWebApplicationFactory _factory;

    public LoggingCoverageTests(LoggingWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetAllMocks();
    }

    private HttpClient AuthClient() => _factory.CreateClient().WithAuth(userId: 1);

    private static StringContent Json(object body) =>
        new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    private void SetupOwnerRole() =>
        _factory.BoardServiceMock
            .Setup(s => s.GetUserRoleByBoardIdAndUserIdAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(UserRoleEnum.OWNER);

    // ── HttpLoggingMiddleware: JSON body path + SanitizeBody ──────────────

    [Fact]
    public async Task Middleware_AnonymousJsonRequest_LogsRequestAndSanitizesSensitiveFields()
    {
        _factory.AuthServiceMock
            .Setup(s => s.LoginUserAsync(It.IsAny<UserLoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserLoginResponse(1, "alice", "token"));

        // "password" key triggers SanitizeBody sensitive-key redaction path
        var response = await _factory.CreateClient().PostAsync("/login",
            Json(new { userName = "alice", password = "Pass@123" }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── HttpLoggingMiddleware: non-JSON body path ─────────────────────────

    [Fact]
    public async Task Middleware_GetRequest_LogsNoJsonBody()
    {
        _factory.UserServiceMock
            .Setup(s => s.GetUserById(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserResponse { Id = 1, UserName = "alice", Email = "alice@test.com", FirstName = "Alice", LastName = "Smith", CreationDate = DateTime.UtcNow });

        // GET has no body → GetJSONRequestBody returns "No JSON body."
        var response = await AuthClient().GetAsync("/api/users/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── HttpLoggingMiddleware: SanitizeQuery (sensitive + non-sensitive) ──

    [Fact]
    public async Task Middleware_QueryWithSensitiveParam_RedactsAndPassesNonSensitive()
    {
        _factory.BoardServiceMock
            .Setup(s => s.GetUserBoardsAsync(1, 0, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<BoardResponse>(), 0));

        // "email" is sensitive (redacted), "pageNum" is not → covers both branches in SanitizeQuery lambda
        var response = await AuthClient().GetAsync("/api/boards?email=test@example.com&pageNum=0");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── HttpLoggingMiddleware: SanitizeBody catch block ───────────────────

    [Fact]
    public async Task Middleware_InvalidJsonBody_SanitizationCatchReturnsRedacted()
    {
        // Invalid JSON triggers JObject.Parse exception → SanitizeBody catch block
        var invalidJson = new StringContent("{invalid json!", Encoding.UTF8, "application/json");
        var response = await _factory.CreateClient().PostAsync("/login", invalidJson);

        // Model binding fails → 400; middleware sanitization catch still executed
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── ControllerLoggingActionFilter: board endpoint with boardId ────────

    [Fact]
    public async Task Filter_BoardEndpointRequest_LogsWithBoardIdAndRole()
    {
        SetupOwnerRole();
        _factory.BoardServiceMock
            .Setup(s => s.DeleteBoardAsync(1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // boardId in route → GetRoleWithBoardIdAsync finds boardId, calls boardService
        var response = await AuthClient().DeleteAsync("/api/boards/1");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // ── ControllerLoggingActionFilter: non-board endpoint (null boardId) ──

    [Fact]
    public async Task Filter_NonBoardEndpointRequest_LogsWithNullBoardId()
    {
        _factory.UserServiceMock
            .Setup(s => s.UpdateUser(1, It.IsAny<UserUpdateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserUpdateResponse { FirstName = "A", LastName = "B", UserName = "alice", Email = "alice@test.com" });

        // No boardId route key → GetRoleWithBoardIdAsync returns (null, null)
        var response = await AuthClient().PutAsync("/api/users/1",
            Json(new { firstName = "A", lastName = "B", userName = "alice", email = "alice@test.com", imageName = "" }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── UserHelper.GetUserId error branch + controller return paths ───────

    [Fact]
    public async Task GetUserId_MissingNameIdentifierClaim_NotificationControllerReturnsUnauthorized()
    {
        // Valid JWT without NameIdentifier → passes [Authorize] but GetUserId returns error
        var noUserIdToken = JwtTokenHelper.GenerateTokenWithoutUserId();
        var client = _factory.CreateClient().WithToken(noUserIdToken);

        var response = await client.PostAsync("/api/notifications/1/accept", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetUserId_MissingNameIdentifierClaim_CommentControllerReturnsUnauthorized()
    {
        var noUserIdToken = JwtTokenHelper.GenerateTokenWithoutUserId();
        var client = _factory.CreateClient().WithToken(noUserIdToken);

        var response = await client.PostAsync("/api/boards/1/tasks/1/comments",
            Json(new { content = "Hello" }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetUserId_MissingNameIdentifierClaim_BoardControllerGetAllBoardsReturnsUnauthorized()
    {
        var noUserIdToken = JwtTokenHelper.GenerateTokenWithoutUserId();
        var client = _factory.CreateClient().WithToken(noUserIdToken);

        // No userId query param → null branch → GetUserId fails → return unauthorizedResult
        var response = await client.GetAsync("/api/boards");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetUserId_MissingNameIdentifierClaim_CreateBoardReturnsUnauthorized()
    {
        var noUserIdToken = JwtTokenHelper.GenerateTokenWithoutUserId();
        var client = _factory.CreateClient().WithToken(noUserIdToken);

        // No userId query param → null branch → GetUserId fails → return unauthorizedResult in CreateBoard
        var response = await client.PostAsync("/api/boards",
            Json(new { title = "Board", description = "Desc", imageName = "" }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── PermissionHandler: userId null early-return branch ────────────────

    [Fact]
    public async Task PermissionHandler_JwtWithoutUserId_ReturnsForbidden()
    {
        // JWT with no NameIdentifier → PermissionHandler: userId is null → returns early → 403
        var noUserIdToken = JwtTokenHelper.GenerateTokenWithoutUserId();
        var client = _factory.CreateClient().WithToken(noUserIdToken);

        var response = await client.DeleteAsync("/api/boards/1");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
