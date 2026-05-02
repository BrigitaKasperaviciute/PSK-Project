using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.Data.Database;
using WorthBoards.Domain.Entities;

namespace WorthBoards.IntegrationTests.Infrastructure;

public abstract class IntegrationTestBase : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    protected readonly CustomWebApplicationFactory Factory;
    protected HttpClient Client = null!;

    protected IntegrationTestBase(CustomWebApplicationFactory factory)
    {
        Factory = factory;
    }

    public async Task InitializeAsync()
    {
        Client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = true
        });

        // Ensure schema exists (idempotent across tests in the same class).
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync()
    {
        Client.Dispose();
        return Task.CompletedTask;
    }

    // ── Auth helpers ────────────────────────────────────────────────────────

    protected async Task<(int UserId, string Token)> RegisterAndLoginAsync(string? suffix = null)
    {
        var id = Guid.NewGuid().ToString("N")[..10];
        suffix ??= id;

        var register = new UserRegisterRequest(
            FirstName: "Test",
            LastName: "User",
            UserName: $"user_{suffix}",
            Email: $"user_{suffix}@test.com",
            Password: "Password1!"
        );

        var registerResponse = await Client.PostAsJsonAsync("/register", register);
        registerResponse.EnsureSuccessStatusCode();
        var userResponse = await registerResponse.Content.ReadFromJsonAsync<UserResponse>();

        var login = new UserLoginRequest($"user_{suffix}", "Password1!");
        var loginResponse = await Client.PostAsJsonAsync("/login", login);
        loginResponse.EnsureSuccessStatusCode();
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<UserLoginResponse>();

        return (loginResult!.Id, loginResult.JwtToken);
    }

    protected void SetAuthToken(string token)
    {
        Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    protected void ClearAuthToken()
    {
        Client.DefaultRequestHeaders.Authorization = null;
    }

    // ── Board helpers ────────────────────────────────────────────────────────

    protected async Task<BoardResponse> CreateBoardAsync(string token)
    {
        SetAuthToken(token);
        var request = new BoardRequest
        {
            Title = $"Board-{Guid.NewGuid():N}",
            Description = "Integration test board",
            ImageName = "default.jpg"
        };
        var response = await Client.PostAsJsonAsync("/api/boards", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BoardResponse>())!;
    }

    // ── Task helpers ─────────────────────────────────────────────────────────

    protected async Task<BoardTaskResponse> CreateTaskAsync(int boardId, string token)
    {
        SetAuthToken(token);
        var request = new BoardTaskRequest
        {
            Title = $"Task-{Guid.NewGuid():N}",
            Description = "Integration test task",
            TaskStatus = TaskStatusEnum.PENDING
        };
        var response = await Client.PostAsJsonAsync($"/api/boards/{boardId}/tasks", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BoardTaskResponse>())!;
    }

    // ── Comment helpers ──────────────────────────────────────────────────────

    protected async Task<CommentResponse> CreateCommentAsync(int boardId, int taskId, string token)
    {
        SetAuthToken(token);
        var request = new CommentRequest { Content = "Integration test comment" };
        var response = await Client.PostAsJsonAsync(
            $"/api/boards/{boardId}/tasks/{taskId}/comments", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CommentResponse>())!;
    }

    // ── BoardOnUser helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Inserts a BoardOnUser record directly into the DB, bypassing the invitation flow.
    /// </summary>
    protected async Task LinkUserToBoardDirectlyAsync(int boardId, int userId, UserRoleEnum role)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var exists = await db.BoardOnUsers.AnyAsync(b => b.BoardId == boardId && b.UserId == userId);
        if (!exists)
        {
            db.BoardOnUsers.Add(new BoardOnUser
            {
                BoardId = boardId,
                UserId = userId,
                UserRole = role,
                AddedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
    }

    // ── Notification helpers ─────────────────────────────────────────────────

    protected async Task<int> InviteUserAndGetNotificationAsync(
        int boardId, int inviteeId, UserRoleEnum role, string ownerToken)
    {
        SetAuthToken(ownerToken);
        var request = new InvitationRequest { UserId = inviteeId, Role = role };
        var response = await Client.PostAsJsonAsync($"/api/boards/{boardId}/invite", request);
        response.EnsureSuccessStatusCode();

        // Switch to invitee context to read the notification
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var notificationOnUser = await db.NotificationsOnUsers
            .Where(n => n.UserId == inviteeId)
            .OrderByDescending(n => n.NotificationId)
            .FirstOrDefaultAsync();

        return notificationOnUser!.NotificationId;
    }

    // ── DB query helpers ─────────────────────────────────────────────────────

    protected async Task<T?> QueryAsync<T>(Func<ApplicationDbContext, Task<T?>> query)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await query(db);
    }
}
