using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace WorthBoards.IntegrationTests.Experiment1;

public sealed record AuthSession(HttpClient Client, int UserId, string UserName, string Email, string JwtToken, string Password);

public static class TestAuthHelpers
{
    public static async Task<AuthSession> RegisterAndLoginAsync(TestApplicationFactory factory, string? userNamePrefix = null)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var prefix = string.IsNullOrWhiteSpace(userNamePrefix) ? "integration" : userNamePrefix;
        if (prefix.Length > 21)
        {
            prefix = prefix[..21];
        }

        var userName = $"{prefix}_{suffix}";
        var email = $"{userName}@example.com";
        const string password = "Passw0rd!";

        var anonymousClient = factory.CreateClient();

        var registerResponse = await anonymousClient.PostAsJsonAsync("/register", new
        {
            firstName = "Integration",
            lastName = "Tester",
            userName,
            email,
            password
        });
        registerResponse.EnsureSuccessStatusCode();

        var loginResponse = await anonymousClient.PostAsJsonAsync("/login", new
        {
            userName,
            password
        });
        loginResponse.EnsureSuccessStatusCode();

        var loginContent = await loginResponse.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(loginContent);
        var root = document.RootElement;

        var token = ReadString(root, "jwtToken");
        var userId = ReadInt(root, "id");

        var authenticatedClient = factory.CreateClient();
        authenticatedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return new AuthSession(authenticatedClient, userId, userName, email, token, password);
    }

    public static async Task<int> CreateBoardAsync(HttpClient client, string? title = null)
    {
        var response = await client.PostAsJsonAsync("/api/boards", new
        {
            title = title ?? $"Board-{Guid.NewGuid():N}"[..12],
            description = "Board created for integration tests",
            imageName = "board.png"
        });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        return ReadInt(document.RootElement, "id");
    }

    public static async Task<int> CreateTaskAsync(HttpClient client, int boardId, string? title = null)
    {
        var response = await client.PostAsJsonAsync($"/api/boards/{boardId}/tasks", new
        {
            title = title ?? $"Task-{Guid.NewGuid():N}"[..12],
            description = "Task created for integration tests",
            deadlineEnd = DateTime.UtcNow.AddDays(7),
            taskStatus = 0
        });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        return ReadInt(document.RootElement, "id");
    }

    public static async Task LinkUserToBoardAsync(HttpClient client, int boardId, int userId, int userRole)
    {
        var response = await client.PostAsJsonAsync($"/api/boards/{boardId}/link/{userId}", new
        {
            userRole
        });

        response.EnsureSuccessStatusCode();
    }

    public static async Task<uint> GetBoardVersionAsync(HttpClient client, int boardId)
    {
        var response = await client.GetAsync($"/api/boards/{boardId}");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        return ReadUInt(document.RootElement, "version");
    }

    public static async Task<uint> GetTaskVersionAsync(HttpClient client, int boardId, int taskId)
    {
        var response = await client.GetAsync($"/api/boards/{boardId}/tasks/{taskId}");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        return ReadUInt(document.RootElement, "version");
    }

    public static async Task<(int CommentId, uint Version)> CreateCommentAsync(HttpClient client, int boardId, int taskId, string content)
    {
        var response = await client.PostAsJsonAsync($"/api/boards/{boardId}/tasks/{taskId}/comments", new
        {
            content
        });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        return (ReadInt(document.RootElement, "id"), ReadUInt(document.RootElement, "version"));
    }

    public static async Task<int> GetFirstNotificationIdAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/notifications");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);

        if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("No notifications were returned.");
        }

        return ReadInt(document.RootElement[0], "id");
    }

    public static StringContent BuildJsonPatchContent(string patchJson)
    {
        return new StringContent(patchJson, Encoding.UTF8, "application/json-patch+json");
    }

    public static string GetResetPasswordTokenFromFakeEmail(string recipientEmail)
    {
        if (!FakeEmailService.TryGetLastEmail(recipientEmail, out var email))
        {
            throw new InvalidOperationException($"No email captured for recipient '{recipientEmail}'.");
        }

        return ExtractResetTokenFromBody(email.Body);
    }

    private static string ReadString(JsonElement root, string propertyName)
    {
        if (!TryGetPropertyCaseInsensitive(root, propertyName, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"Expected string property '{propertyName}' in JSON response.");
        }

        return value.GetString() ?? throw new InvalidOperationException($"Property '{propertyName}' was null.");
    }

    private static int ReadInt(JsonElement root, string propertyName)
    {
        if (!TryGetPropertyCaseInsensitive(root, propertyName, out var value) || value.ValueKind != JsonValueKind.Number)
        {
            throw new InvalidOperationException($"Expected numeric property '{propertyName}' in JSON response.");
        }

        return value.GetInt32();
    }

    private static uint ReadUInt(JsonElement root, string propertyName)
    {
        if (!TryGetPropertyCaseInsensitive(root, propertyName, out var value) || value.ValueKind != JsonValueKind.Number)
        {
            throw new InvalidOperationException($"Expected unsigned numeric property '{propertyName}' in JSON response.");
        }

        return value.GetUInt32();
    }

    private static bool TryGetPropertyCaseInsensitive(JsonElement root, string propertyName, out JsonElement value)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string ExtractResetTokenFromBody(string body)
    {
        var hrefMatch = Regex.Match(body, "href=\\\"(?<url>[^\\\"]+)\\\"", RegexOptions.IgnoreCase);
        if (!hrefMatch.Success)
        {
            throw new InvalidOperationException("Reset password email does not contain an href URL.");
        }

        var url = hrefMatch.Groups["url"].Value;
        var tokenMatch = Regex.Match(url, "(?:\\?|&)token=(?<token>[^&]+)", RegexOptions.IgnoreCase);
        if (!tokenMatch.Success)
        {
            throw new InvalidOperationException("Reset password URL does not contain a token query parameter.");
        }

        return Uri.UnescapeDataString(tokenMatch.Groups["token"].Value);
    }
}
