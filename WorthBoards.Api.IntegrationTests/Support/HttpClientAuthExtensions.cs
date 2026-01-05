namespace WorthBoards.Api.IntegrationTests.Support;

public static class HttpClientAuthExtensions
{
    public static HttpClient WithUser(this HttpClient client, int userId = 1, string? userName = null)
    {
        var headerValue = $"Test {userId} {userName ?? $"user{userId}"}";
        client.DefaultRequestHeaders.Remove("Authorization");
        client.DefaultRequestHeaders.Add("Authorization", headerValue);
        return client;
    }
}