using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.Data.Identity;

namespace WorthBoards.IntegrationTests.Infrastructure;

public abstract class IntegrationTestBase : IClassFixture<CustomWebApplicationFactory>
{
    protected readonly CustomWebApplicationFactory Factory;

    protected IntegrationTestBase(CustomWebApplicationFactory factory)
    {
        Factory = factory;
    }

    protected HttpClient CreateAnonymousClient() => Factory.CreateClient();

    protected HttpClient CreateAuthenticatedClient(string token)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    protected async Task<(string Token, int UserId)> RegisterAndLoginAsync(string? suffix = null)
    {
        suffix ??= Guid.NewGuid().ToString("N")[..10];
        var username = $"u_{suffix}";
        var email = $"u_{suffix}@test.com";
        const string password = "Test@1234!";

        var client = CreateAnonymousClient();

        var registerRequest = new UserRegisterRequest("Test", "User", username, email, password);
        await client.PostAsJsonAsync("/register", registerRequest);

        var loginRequest = new UserLoginRequest(username, password);
        var loginResponse = await client.PostAsJsonAsync("/login", loginRequest);
        var loginData = await loginResponse.Content.ReadFromJsonAsync<UserLoginResponse>();

        return (loginData!.JwtToken, loginData.Id);
    }

    protected async Task<BoardResponse> CreateBoardAsync(HttpClient client, string? title = null)
    {
        var request = new BoardRequest
        {
            Title = title ?? $"Board_{Guid.NewGuid():N}",
            Description = "Integration test board",
            ImageName = "default.jpg"
        };
        var response = await client.PostAsJsonAsync("/api/boards", request);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"CreateBoardAsync failed ({response.StatusCode}): {body}");
        }
        return (await response.Content.ReadFromJsonAsync<BoardResponse>())!;
    }

    protected async Task<BoardTaskResponse> CreateTaskAsync(HttpClient client, int boardId, string? title = null)
    {
        var request = new BoardTaskRequest
        {
            Title = title ?? $"Task_{Guid.NewGuid():N}",
            Description = "Integration test task",
            TaskStatus = TaskStatusEnum.PENDING
        };
        var response = await client.PostAsJsonAsync($"/api/boards/{boardId}/tasks", request);
        return (await response.Content.ReadFromJsonAsync<BoardTaskResponse>())!;
    }

    protected async Task<CommentResponse> CreateCommentAsync(HttpClient client, int boardId, int taskId)
    {
        var request = new CommentRequest { Content = "Integration test comment" };
        var response = await client.PostAsJsonAsync($"/api/boards/{boardId}/tasks/{taskId}/comments", request);
        return (await response.Content.ReadFromJsonAsync<CommentResponse>())!;
    }

    protected async Task<string> GetPasswordResetTokenAsync(string email)
    {
        using var scope = Factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        return await userManager.GeneratePasswordResetTokenAsync(user!);
    }

    protected async Task<NotificationResponse> InviteUserAndGetNotificationAsync(
        HttpClient ownerClient, HttpClient guestClient, int boardId, int guestUserId)
    {
        var inviteRequest = new InvitationRequest { UserId = guestUserId, Role = UserRoleEnum.VIEWER };
        await ownerClient.PostAsJsonAsync($"/api/boards/{boardId}/invite", inviteRequest);

        var notifResponse = await guestClient.GetAsync("/api/notifications");
        var notifications = await notifResponse.Content.ReadFromJsonAsync<List<NotificationResponse>>();
        return notifications!.First(n => n.BoardId == boardId);
    }

    protected async Task LinkUserToBoardDirectlyAsync(HttpClient ownerClient, int boardId, int userId,
        UserRoleEnum role = UserRoleEnum.VIEWER)
    {
        var request = new LinkUserToBoardRequest { UserRole = role };
        await ownerClient.PostAsJsonAsync($"/api/boards/{boardId}/link/{userId}", request);
    }
}
