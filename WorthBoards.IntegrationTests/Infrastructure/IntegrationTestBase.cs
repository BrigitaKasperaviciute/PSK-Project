using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using WorthBoards.Data.Database;
using WorthBoards.Domain.Entities;

namespace WorthBoards.IntegrationTests.Infrastructure;

public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly CustomWebApplicationFactory Factory;
    protected readonly HttpClient Client;

    protected IntegrationTestBase(CustomWebApplicationFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
    }

    public virtual async Task InitializeAsync() => await ClearDatabaseAsync();

    public virtual Task DisposeAsync() => Task.CompletedTask;

    // ── Database helpers ──────────────────────────────────────────────────────

    protected async Task ClearDatabaseAsync()
    {
        await using var db = Factory.CreateDbContext();
        // Delete in FK-safe order (children before parents).
        await db.NotificationsOnUsers.ExecuteDeleteAsync();
        await db.TasksOnUsers.ExecuteDeleteAsync();
        await db.Comments.ExecuteDeleteAsync();
        await db.Notifications.ExecuteDeleteAsync();
        await db.BoardTasks.ExecuteDeleteAsync();
        await db.BoardOnUsers.ExecuteDeleteAsync();
        await db.Boards.ExecuteDeleteAsync();
        // Identity tables
        await db.UserClaims.ExecuteDeleteAsync();
        await db.UserRoles.ExecuteDeleteAsync();
        await db.UserLogins.ExecuteDeleteAsync();
        await db.UserTokens.ExecuteDeleteAsync();
        await db.Users.ExecuteDeleteAsync();
        await db.Roles.ExecuteDeleteAsync();
    }

    protected ApplicationDbContext CreateDbContext() => Factory.CreateDbContext();

    // ── Auth helpers ──────────────────────────────────────────────────────────

    protected void SetAuthToken(string token) =>
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    protected void ClearAuthToken() =>
        Client.DefaultRequestHeaders.Authorization = null;

    /// <summary>
    /// Registers a unique user and immediately logs in.
    /// Returns the user ID and the JWT.
    /// </summary>
    protected async Task<(int UserId, string Token)> RegisterAndLoginAsync(
        string? username = null, string? email = null, string? password = null)
    {
        var suffix = Guid.NewGuid().ToString("N")[..10];
        username ??= $"user{suffix}";
        email    ??= $"{suffix}@test.com";
        password ??= "Test@1234!";

        var registerRequest = new UserRegisterRequest(
            FirstName: "Test",
            LastName: "User",
            UserName: username,
            Email: email,
            Password: password);

        var registerResponse = await Client.PostAsJsonAsync("/register", registerRequest);
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"registration of '{username}' should succeed");

        var loginRequest = new UserLoginRequest(UserName: username, Password: password);
        var loginResponse = await Client.PostAsJsonAsync("/login", loginRequest);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"login of '{username}' should succeed");

        var loginBody = await loginResponse.Content.ReadFromJsonAsync<UserLoginResponse>();
        return (loginBody!.Id, loginBody.JwtToken);
    }

    // ── Entity creation helpers ───────────────────────────────────────────────

    protected async Task<BoardResponse> CreateBoardAsync(string title = "Test Board", string description = "Test Description")
    {
        var request = new BoardRequest { Title = title, Description = description, ImageName = "default.png" };
        var response = await Client.PostAsJsonAsync("/api/boards", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created, "board creation should succeed");
        return (await response.Content.ReadFromJsonAsync<BoardResponse>())!;
    }

    protected async Task<BoardTaskResponse> CreateTaskAsync(int boardId, string title = "Test Task")
    {
        var request = new BoardTaskRequest { Title = title, TaskStatus = TaskStatusEnum.PENDING };
        var response = await Client.PostAsJsonAsync($"/api/boards/{boardId}/tasks", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created, "task creation should succeed");
        return (await response.Content.ReadFromJsonAsync<BoardTaskResponse>())!;
    }

    protected async Task<CommentResponse> CreateCommentAsync(int boardId, int taskId, string content = "Test comment")
    {
        var request = new CommentRequest { Content = content };
        var response = await Client.PostAsJsonAsync($"/api/boards/{boardId}/tasks/{taskId}/comments", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created, "comment creation should succeed");
        return (await response.Content.ReadFromJsonAsync<CommentResponse>())!;
    }

    /// <summary>
    /// Directly inserts a BoardOnUser row, bypassing the invitation flow.
    /// Use this to set up roles for tests that need a specific board membership.
    /// </summary>
    protected async Task LinkUserToBoardDirectlyAsync(int boardId, int userId, UserRoleEnum role)
    {
        await using var db = Factory.CreateDbContext();
        var existing = await db.BoardOnUsers.FindAsync(boardId, userId);
        if (existing != null)
        {
            existing.UserRole = role;
        }
        else
        {
            db.BoardOnUsers.Add(new BoardOnUser
            {
                BoardId = boardId,
                UserId = userId,
                UserRole = role,
                AddedAt = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Creates an INVITATION notification for <paramref name="inviteeUserId"/> to join <paramref name="boardId"/>.
    /// Returns the notification ID.
    /// </summary>
    protected async Task<int> CreateInvitationNotificationAsync(
        int boardId, int inviteeUserId, int senderUserId, UserRoleEnum role = UserRoleEnum.EDITOR)
    {
        await using var db = Factory.CreateDbContext();
        var notification = new Notification
        {
            BoardId = boardId,
            SenderId = senderUserId,
            NotificationType = NotificationEventTypeEnum.INVITATION,
            SubjectUserId = inviteeUserId,
            InvitationRole = role,
            NotificationsOnUsers = new List<NotificationOnUser>
            {
                new NotificationOnUser { UserId = inviteeUserId }
            }
        };
        db.Notifications.Add(notification);
        await db.SaveChangesAsync();
        return notification.Id;
    }
}
