using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Common.Enums;
using WorthBoards.Data.Database;
using Xunit;

namespace WorthBoards.IntegrationTests.Experiment1.TestInfrastructure;

[Collection(IntegrationTestCollection.Name)]
public abstract class IntegrationTestBase(IntegrationTestWebApplicationFactory factory) : IAsyncLifetime
{
    protected readonly IntegrationTestWebApplicationFactory Factory = factory;

    public async Task InitializeAsync()
    {
        await Factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    protected HttpClient CreateAnonymousClient()
    {
        return Factory.CreateApiClient();
    }

    protected async Task<TestUserContext> CreateAuthenticatedUserAsync(string prefix)
    {
        var client = CreateAnonymousClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var userName = $"{prefix[..Math.Min(prefix.Length, 20)]}_{suffix}";
        userName = userName[..Math.Min(userName.Length, 30)];

        var registerRequest = new UserRegisterRequest(
            FirstName: "Integration",
            LastName: "Tester",
            UserName: userName,
            Email: $"{userName}@example.com",
            Password: "Passw0rd!"
        );

        var registerResponse = await client.PostAsJsonAsync("/register", registerRequest);
        registerResponse.IsSuccessStatusCode.Should().BeTrue();

        var registeredUser = await registerResponse.Content.ReadFromJsonAsync<UserResponse>();
        registeredUser.Should().NotBeNull();

        var loginResponse = await client.PostAsJsonAsync("/login", new UserLoginRequest(userName, "Passw0rd!"));
        loginResponse.IsSuccessStatusCode.Should().BeTrue();

        var loginPayload = await loginResponse.Content.ReadFromJsonAsync<UserLoginResponse>();
        loginPayload.Should().NotBeNull();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginPayload!.JwtToken);

        return new TestUserContext(
            registeredUser!.Id,
            userName,
            registerRequest.Email,
            "Passw0rd!",
            client);
    }

    protected async Task<int> CreateBoardAsync(HttpClient client, string title)
    {
        var response = await client.PostAsJsonAsync("/api/boards", new BoardRequest
        {
            Title = title,
            Description = "Board description",
            ImageName = "board.png"
        });

        response.IsSuccessStatusCode.Should().BeTrue();

        using var contentStream = await response.Content.ReadAsStreamAsync();
        var json = await JsonDocument.ParseAsync(contentStream);
        return json.RootElement.GetProperty("id").GetInt32();
    }

    protected async Task<int> CreateBoardTaskAsync(HttpClient client, int boardId, string title, TaskStatusEnum status = TaskStatusEnum.PENDING)
    {
        var response = await client.PostAsJsonAsync($"/api/boards/{boardId}/tasks", new BoardTaskRequest
        {
            Title = title,
            Description = "Task description",
            DeadlineEnd = DateTime.UtcNow.AddDays(3),
            TaskStatus = status
        });

        response.IsSuccessStatusCode.Should().BeTrue();

        using var contentStream = await response.Content.ReadAsStreamAsync();
        var json = await JsonDocument.ParseAsync(contentStream);
        return json.RootElement.GetProperty("id").GetInt32();
    }

    protected async Task<int> CreateCommentAsync(HttpClient client, int boardId, int taskId, string content)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/boards/{boardId}/tasks/{taskId}/comments",
            new CommentRequest { Content = content });

        response.IsSuccessStatusCode.Should().BeTrue();

        using var contentStream = await response.Content.ReadAsStreamAsync();
        var json = await JsonDocument.ParseAsync(contentStream);
        return json.RootElement.GetProperty("id").GetInt32();
    }

    protected ApplicationDbContext CreateDbContextScope()
    {
        var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }
}

public record TestUserContext(int UserId, string UserName, string Email, string Password, HttpClient Client);
