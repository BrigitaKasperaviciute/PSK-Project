using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using WorthBoards.Common.Enums;

namespace WorthBoards.IntegrationTests;

public sealed class IntegrationScenariosTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public IntegrationScenariosTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RegisterUser_SendsWelcomeEmail_HappyFlow()
    {
        await _factory.ResetStateAsync();
        using var client = _factory.CreateClient();

        var email = $"register-happy-{Guid.NewGuid():N}@example.com";
        var payload = new
        {
            firstName = "Test",
            lastName = "User",
            userName = $"user_{Guid.NewGuid():N}"[..13],
            email,
            password = "P@ssword1"
        };

        var response = await client.PostAsJsonAsync("/register", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var sentEmails = _factory.SentEmails;
        Assert.Single(sentEmails);
        Assert.Equal(email, sentEmails[0].RecipientEmail);
        Assert.False(string.IsNullOrWhiteSpace(sentEmails[0].Subject));
    }

    [Fact]
    public async Task RegisterUser_WithDuplicateEmail_ReturnsBadRequest_NegativeFlow()
    {
        await _factory.ResetStateAsync();
        using var client = _factory.CreateClient();

        var email = $"register-negative-{Guid.NewGuid():N}@example.com";
        var firstPayload = new
        {
            firstName = "First",
            lastName = "User",
            userName = $"user_{Guid.NewGuid():N}"[..13],
            email,
            password = "P@ssword1"
        };

        var duplicatePayload = new
        {
            firstName = "Second",
            lastName = "User",
            userName = $"user_{Guid.NewGuid():N}"[..13],
            email,
            password = "P@ssword1"
        };

        var firstResponse = await client.PostAsJsonAsync("/register", firstPayload);
        var duplicateResponse = await client.PostAsJsonAsync("/register", duplicatePayload);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, duplicateResponse.StatusCode);
    }

    [Fact]
    public async Task AddTaskToBoard_CreatesTask_HappyFlow()
    {
        await _factory.ResetStateAsync();

        var token = await RegisterAndLoginAsync();
        var boardId = await CreateBoardAsync(token);

        using var client = CreateAuthorizedClient(token);
        var taskPayload = new
        {
            title = "Integration task",
            description = "Task created in integration test",
            deadlineEnd = DateTime.UtcNow.AddDays(1),
            taskStatus = TaskStatusEnum.PENDING
        };

        var response = await client.PostAsJsonAsync($"/api/boards/{boardId}/tasks", taskPayload);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(boardId, json.GetProperty("boardId").GetInt32());
        Assert.Equal("Integration task", json.GetProperty("title").GetString());
        Assert.Equal((int)TaskStatusEnum.PENDING, json.GetProperty("taskStatus").GetInt32());
    }

    [Fact]
    public async Task AddTaskToBoard_WithoutToken_ReturnsUnauthorized_NegativeFlow()
    {
        await _factory.ResetStateAsync();

        using var client = _factory.CreateClient();
        var taskPayload = new
        {
            title = "Unauthorized task",
            description = "Should fail",
            deadlineEnd = DateTime.UtcNow.AddDays(1),
            taskStatus = TaskStatusEnum.PENDING
        };

        var response = await client.PostAsJsonAsync("/api/boards/1/tasks", taskPayload);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTask_MoveStatusToInProgress_HappyFlow()
    {
        await _factory.ResetStateAsync();

        var token = await RegisterAndLoginAsync();
        var boardId = await CreateBoardAsync(token);
        var (taskId, version) = await CreateTaskAsync(token, boardId, "Move me");

        using var client = CreateAuthorizedClient(token);
        var updatePayload = new
        {
            title = "Move me",
            description = "Moved to in progress",
            deadlineEnd = DateTime.UtcNow.AddDays(2),
            taskStatus = TaskStatusEnum.IN_PROGRESS,
            version
        };

        var response = await client.PutAsJsonAsync($"/api/boards/{boardId}/tasks/{taskId}", updatePayload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)TaskStatusEnum.IN_PROGRESS, json.GetProperty("taskStatus").GetInt32());
        Assert.Equal("Moved to in progress", json.GetProperty("description").GetString());
    }

    [Fact]
    public async Task UpdateTask_WithStaleVersion_ReturnsConflict_NegativeFlow()
    {
        await _factory.ResetStateAsync();

        var token = await RegisterAndLoginAsync();
        var boardId = await CreateBoardAsync(token);
        var (taskId, version) = await CreateTaskAsync(token, boardId, "Concurrency task");

        using var client = CreateAuthorizedClient(token);
        var staleVersionPayload = new
        {
            title = "Concurrency task",
            description = "Try update with stale version",
            deadlineEnd = DateTime.UtcNow.AddDays(2),
            taskStatus = TaskStatusEnum.COMPLETED,
            version = version + 1
        };

        var response = await client.PutAsJsonAsync($"/api/boards/{boardId}/tasks/{taskId}", staleVersionPayload);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private async Task<string> RegisterAndLoginAsync()
    {
        using var client = _factory.CreateClient();

        var userName = $"iuser_{Guid.NewGuid():N}"[..18];
        var email = $"integration-{Guid.NewGuid():N}@example.com";

        var registerPayload = new
        {
            firstName = "Integration",
            lastName = "Tester",
            userName,
            email,
            password = "P@ssword1"
        };

        var registerResponse = await client.PostAsJsonAsync("/register", registerPayload);
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        var loginPayload = new
        {
            userName,
            password = "P@ssword1"
        };

        var loginResponse = await client.PostAsJsonAsync("/login", loginPayload);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var loginJson = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        return loginJson.GetProperty("jwtToken").GetString()
            ?? throw new InvalidOperationException("JWT token was not returned by login endpoint.");
    }

    private async Task<int> CreateBoardAsync(string token)
    {
        using var client = CreateAuthorizedClient(token);

        var boardPayload = new
        {
            title = $"Integration Board {Guid.NewGuid():N}",
            description = "Board for integration tests",
            imageName = "default.png"
        };

        var response = await client.PostAsJsonAsync("/api/boards", boardPayload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("id").GetInt32();
    }

    private async Task<(int taskId, uint version)> CreateTaskAsync(string token, int boardId, string title)
    {
        using var client = CreateAuthorizedClient(token);

        var taskPayload = new
        {
            title,
            description = "Task seed",
            deadlineEnd = DateTime.UtcNow.AddDays(1),
            taskStatus = TaskStatusEnum.PENDING
        };

        var response = await client.PostAsJsonAsync($"/api/boards/{boardId}/tasks", taskPayload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (json.GetProperty("id").GetInt32(), json.GetProperty("version").GetUInt32());
    }

    private HttpClient CreateAuthorizedClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
