using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.Data.Database;
using WorthBoards.Domain.Entities;

namespace WorthBoards.IntegrationTests.Infrastructure;

public abstract class IntegrationTestBase : IClassFixture<CustomWebApplicationFactory>
{
    protected readonly CustomWebApplicationFactory Factory;

    protected IntegrationTestBase(CustomWebApplicationFactory factory)
    {
        Factory = factory;
    }

    protected HttpClient CreateClient(string? token = null)
    {
        var client = Factory.CreateClient();
        if (token != null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    protected async Task<(int UserId, string Token, HttpClient Client)> RegisterAndLoginAsync()
    {
        var id = Guid.NewGuid().ToString("N")[..10];
        var request = new UserRegisterRequest(
            FirstName: "Test",
            LastName: "User",
            UserName: $"user{id}",
            Email: $"user{id}@example.com",
            Password: "Test@12345"
        );

        var anonClient = Factory.CreateClient();
        var registerResponse = await anonClient.PostAsJsonAsync("/register", request);
        registerResponse.EnsureSuccessStatusCode();
        var user = await registerResponse.Content.ReadFromJsonAsync<UserResponse>();

        var loginResponse = await anonClient.PostAsJsonAsync("/login",
            new UserLoginRequest(request.UserName, request.Password));
        loginResponse.EnsureSuccessStatusCode();
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<UserLoginResponse>();

        var client = CreateClient(loginResult!.JwtToken);
        return (user!.Id, loginResult.JwtToken, client);
    }

    protected async Task<BoardResponse> CreateBoardAsync(HttpClient client, string? title = null)
    {
        var request = new BoardRequest
        {
            Title = title ?? $"Board-{Guid.NewGuid():N}",
            Description = "Test board",
            ImageName = ""
        };
        var response = await client.PostAsJsonAsync("/api/boards", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BoardResponse>())!;
    }

    protected async Task<BoardTaskResponse> CreateTaskAsync(HttpClient client, int boardId, string? title = null)
    {
        var request = new BoardTaskRequest
        {
            Title = title ?? $"Task-{Guid.NewGuid():N}",
            Description = "Test task",
            DeadlineEnd = DateTime.UtcNow.AddDays(7),
            TaskStatus = TaskStatusEnum.PENDING
        };
        var response = await client.PostAsJsonAsync($"/api/boards/{boardId}/tasks", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BoardTaskResponse>())!;
    }

    protected async Task<CommentResponse> CreateCommentAsync(HttpClient client, int boardId, int taskId, string? content = null)
    {
        var request = new CommentRequest { Content = content ?? "Test comment" };
        var response = await client.PostAsJsonAsync($"/api/boards/{boardId}/tasks/{taskId}/comments", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CommentResponse>())!;
    }

    protected async Task LinkUserToBoardDirectlyAsync(int boardId, int userId, UserRoleEnum role)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.BoardOnUsers.Add(new BoardOnUser
        {
            BoardId = boardId,
            UserId = userId,
            UserRole = role,
            AddedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    protected ApplicationDbContext GetDbContext()
    {
        var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }
}
