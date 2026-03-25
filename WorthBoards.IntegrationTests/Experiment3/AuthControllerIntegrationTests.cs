using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;

namespace WorthBoards.IntegrationTests.Experiment3;

public sealed class AuthControllerIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthControllerIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RegisterUser_ValidRequest_ReturnsOkAndPersistsUserAndSendsEmail()
    {
        // Arrange
        await _factory.ResetStateAsync();
        using var client = _factory.CreateClient();

        var email = $"exp3-register-happy-{Guid.NewGuid():N}@example.com";
        var payload = new
        {
            firstName = "Test",
            lastName = "User",
            userName = $"exp3_user_{Guid.NewGuid():N}"[..18],
            email,
            password = "P@ssword1"
        };

        // Act
        var response = await client.PostAsJsonAsync("/register", payload);
        var responseBody = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(email, responseBody);

        var userCount = await _factory.WithDbContextAsync(db =>
            db.Users.CountAsync(user => user.Email == email));
        Assert.Equal(1, userCount);

        var sentEmails = _factory.SentEmails;
        Assert.Single(sentEmails);
        Assert.Equal(email, sentEmails[0].RecipientEmail);
        Assert.False(string.IsNullOrWhiteSpace(sentEmails[0].Subject));
    }

    [Fact]
    public async Task RegisterUser_DuplicateEmail_ReturnsBadRequestAndDoesNotPersistDuplicate()
    {
        // Arrange
        await _factory.ResetStateAsync();
        using var client = _factory.CreateClient();

        var email = $"exp3-register-negative-{Guid.NewGuid():N}@example.com";
        var firstPayload = new
        {
            firstName = "First",
            lastName = "User",
            userName = $"exp3_user_{Guid.NewGuid():N}"[..18],
            email,
            password = "P@ssword1"
        };

        var duplicatePayload = new
        {
            firstName = "Second",
            lastName = "User",
            userName = $"exp3_user_{Guid.NewGuid():N}"[..18],
            email,
            password = "P@ssword1"
        };

        await client.PostAsJsonAsync("/register", firstPayload);

        // Act
        var response = await client.PostAsJsonAsync("/register", duplicatePayload);
        var responseBody = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("already", responseBody, StringComparison.OrdinalIgnoreCase);

        var userCount = await _factory.WithDbContextAsync(db =>
            db.Users.CountAsync(user => user.Email == email));
        Assert.Equal(1, userCount);

        Assert.Single(_factory.SentEmails);
    }
}
