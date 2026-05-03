using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.Common.Exceptions;

namespace WorthBoards.IntegrationTests.Infrastructure;

[Collection(IntegrationTestCollection.CollectionName)]
public abstract class IntegrationTestBase(WorthBoardsApiFactory factory) : IAsyncLifetime
{
    protected readonly WorthBoardsApiFactory Factory = factory;
    protected SeedState Seed = default!;

    public async Task InitializeAsync()
    {
        Seed = await IntegrationTestSeed.ResetAndSeedAsync(Factory);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    protected HttpClient CreateClient() => Factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false
    });

    protected async Task<HttpClient> CreateAuthenticatedClientAsync(string userName)
    {
        var client = CreateClient();
        var token = await LoginAsync(client, userName);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    protected async Task<string> LoginAsync(HttpClient client, string userName)
    {
        var response = await client.PostAsJsonAsync("/login", new UserLoginRequest(userName, IntegrationTestSeed.Password));
        response.EnsureSuccessStatusCode();

        var loginResponse = await response.Content.ReadFromJsonAsync<UserLoginResponse>();
        loginResponse.Should().NotBeNull();
        loginResponse!.JwtToken.Should().NotBeNullOrWhiteSpace();

        return loginResponse.JwtToken;
    }

    protected static async Task<T?> ReadJsonAsync<T>(HttpResponseMessage response)
    {
        return await response.Content.ReadFromJsonAsync<T>();
    }
}