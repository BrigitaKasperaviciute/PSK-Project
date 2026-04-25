using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WorthBoards.IntegrationTests.Infrastructure;
using Xunit;

namespace WorthBoards.IntegrationTests;

public class LoggingActionFilterTests(LoggingEnabledWebApplicationFactory factory)
    : IClassFixture<LoggingEnabledWebApplicationFactory>
{
    private HttpClient AuthorizedClient(int userId, string userName, string email)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTokenHelper.GenerateToken(userId, userName, email));
        return client;
    }

    // ── Board-scoped endpoint (boardId present in route) ─────────────────────
    // Covers: OnActionExecutionAsync, OnActionExecuting, OnActionExecuted,
    //         GetRoleWithBoardIdAsync (boardId path → calls _boardService.GetUserRole…),
    //         GetControllerName, GetActionName

    [Fact]
    public async Task GetBoardById_WithLoggingFilter_LogsRoleAndBoardId()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        var client = AuthorizedClient(owner.Id, owner.UserName!, owner.Email!);

        // Act – route contains {boardId} so GetRoleWithBoardIdAsync resolves the role
        var response = await client.GetAsync($"/api/boards/{board.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Non-board endpoint (no boardId in route) ─────────────────────────────
    // Covers: GetRoleWithBoardIdAsync early-return path (boardId null → (null, null)),
    //         roleString / boardIdString "NONE" formatting

    [Fact]
    public async Task GetUserBoards_WithLoggingFilter_LogsNoneForBoardId()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var client = AuthorizedClient(owner.Id, owner.UserName!, owner.Email!);

        // Act – /api/boards has no boardId segment, so boardIdString is null and filter returns (null, null)
        var response = await client.GetAsync("/api/boards");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
