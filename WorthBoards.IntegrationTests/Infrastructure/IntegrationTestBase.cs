using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.Data.Database;
using WorthBoards.Domain.Entities;

namespace WorthBoards.IntegrationTests.Infrastructure;

[Collection(nameof(IntegrationTestCollection))]
[Trait("Category", "Integration")]
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

    public virtual async Task InitializeAsync()
    {
        await using var db = Factory.CreateDbContext();
        await db.Comments.ExecuteDeleteAsync();
        await db.TasksOnUsers.ExecuteDeleteAsync();
        await db.NotificationsOnUsers.ExecuteDeleteAsync();
        await db.Notifications.ExecuteDeleteAsync();
        await db.BoardTasks.ExecuteDeleteAsync();
        await db.BoardOnUsers.ExecuteDeleteAsync();
        await db.Boards.ExecuteDeleteAsync();
        await db.UserTokens.ExecuteDeleteAsync();
        await db.UserClaims.ExecuteDeleteAsync();
        await db.UserLogins.ExecuteDeleteAsync();
        await db.UserRoles.ExecuteDeleteAsync();
        await db.RoleClaims.ExecuteDeleteAsync();
        await db.Roles.ExecuteDeleteAsync();
        await db.Users.ExecuteDeleteAsync();
    }

    public virtual Task DisposeAsync() => Task.CompletedTask;

    protected async Task<(int UserId, string Token)> RegisterAndLoginAsync(string? suffix = null)
    {
        var id = suffix ?? Guid.NewGuid().ToString("N")[..8];
        var request = new UserRegisterRequest(
            FirstName: "Test",
            LastName: "User",
            UserName: $"testuser{id}",
            Email: $"test{id}@example.com",
            Password: "TestPass123!"
        );

        await Client.PostAsJsonAsync("/register", request);

        var loginResponse = await Client.PostAsJsonAsync("/login", new UserLoginRequest(
            UserName: request.UserName,
            Password: request.Password
        ));

        var loginData = await loginResponse.Content.ReadFromJsonAsync<UserLoginResponse>();
        return (loginData!.Id, loginData.JwtToken);
    }

    protected void SetAuthToken(string token)
        => Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    protected void ClearAuthToken()
        => Client.DefaultRequestHeaders.Authorization = null;

    protected async Task<BoardResponse> CreateBoardAsync(string title = "Test Board")
    {
        var request = new BoardRequest { Title = title, Description = "Test Description", ImageName = "default.jpg" };
        var response = await Client.PostAsJsonAsync("/api/boards", request);
        return (await response.Content.ReadFromJsonAsync<BoardResponse>())!;
    }

    protected async Task<BoardTaskResponse> CreateTaskAsync(int boardId, string title = "Test Task")
    {
        var request = new BoardTaskRequest
        {
            Title = title,
            Description = "Test task description",
            TaskStatus = TaskStatusEnum.PENDING
        };
        var response = await Client.PostAsJsonAsync($"/api/boards/{boardId}/tasks", request);
        return (await response.Content.ReadFromJsonAsync<BoardTaskResponse>())!;
    }

    protected async Task<CommentResponse> CreateCommentAsync(int boardId, int taskId, string content = "Test comment")
    {
        var request = new CommentRequest { Content = content };
        var response = await Client.PostAsJsonAsync($"/api/boards/{boardId}/tasks/{taskId}/comments", request);
        return (await response.Content.ReadFromJsonAsync<CommentResponse>())!;
    }

    protected async Task LinkUserToBoardDirectlyAsync(int boardId, int userId, UserRoleEnum role)
    {
        await using var db = Factory.CreateDbContext();
        db.BoardOnUsers.Add(new BoardOnUser
        {
            BoardId = boardId,
            UserId = userId,
            UserRole = role,
            AddedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    protected async Task<(int NotificationId, int BoardId)> InviteUserAndGetNotificationAsync(
        int boardId, int inviteeUserId, UserRoleEnum role = UserRoleEnum.VIEWER)
    {
        var inviteResponse = await Client.PostAsJsonAsync(
            $"/api/boards/{boardId}/invite",
            new { UserId = inviteeUserId, Role = role });
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var db = Factory.CreateDbContext();
        var notification = await db.Notifications
            .OrderByDescending(n => n.Id)
            .FirstAsync(n => n.SubjectUserId == inviteeUserId);

        return (notification.Id, boardId);
    }
}
