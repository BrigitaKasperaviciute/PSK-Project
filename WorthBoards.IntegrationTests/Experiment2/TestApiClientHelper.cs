using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using WorthBoards.Common.Enums;

namespace WorthBoards.IntegrationTests.Experiment2;

public static class TestApiClientHelper
{
    public static async Task<string> RegisterAndLoginAsync(HttpClient client)
    {
        var userName = $"exp2_user_{Guid.NewGuid():N}"[..18];
        var email = $"exp2-{Guid.NewGuid():N}@example.com";

        var registerPayload = new
        {
            firstName = "Integration",
            lastName = "Tester",
            userName,
            email,
            password = "P@ssword1"
        };

        var registerResponse = await client.PostAsJsonAsync("/register", registerPayload);
        if (registerResponse.StatusCode != HttpStatusCode.OK)
        {
            throw new InvalidOperationException($"Register failed with status {registerResponse.StatusCode}.");
        }

        var loginPayload = new
        {
            userName,
            password = "P@ssword1"
        };

        var loginResponse = await client.PostAsJsonAsync("/login", loginPayload);
        if (loginResponse.StatusCode != HttpStatusCode.OK)
        {
            throw new InvalidOperationException($"Login failed with status {loginResponse.StatusCode}.");
        }

        var loginJson = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        return loginJson.GetProperty("jwtToken").GetString()
            ?? throw new InvalidOperationException("Missing jwtToken in login response body.");
    }

    public static HttpClient CreateAuthorizedClient(CustomWebApplicationFactory factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public static async Task<int> CreateBoardAsync(HttpClient authorizedClient)
    {
        var payload = new
        {
            title = $"Experiment2 Board {Guid.NewGuid():N}",
            description = "Board seeded for integration testing",
            imageName = "default.png"
        };

        var response = await authorizedClient.PostAsJsonAsync("/api/boards", payload);
        if (response.StatusCode != HttpStatusCode.Created)
        {
            throw new InvalidOperationException($"Create board failed with status {response.StatusCode}.");
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("id").GetInt32();
    }

    public static async Task<(int TaskId, uint Version)> CreateTaskAsync(HttpClient authorizedClient, int boardId, string title)
    {
        var payload = new
        {
            title,
            description = "Task seeded for integration testing",
            deadlineEnd = DateTime.UtcNow.AddDays(1),
            taskStatus = TaskStatusEnum.PENDING
        };

        var response = await authorizedClient.PostAsJsonAsync($"/api/boards/{boardId}/tasks", payload);
        if (response.StatusCode != HttpStatusCode.Created)
        {
            throw new InvalidOperationException($"Create task failed with status {response.StatusCode}.");
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (json.GetProperty("id").GetInt32(), json.GetProperty("version").GetUInt32());
    }
}
