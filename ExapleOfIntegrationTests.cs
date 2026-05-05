// Example of shop integrations tests and shared test infrastructure:
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shop.Api;                       // Program.cs (top-level)
using Shop.Api.Infrastructure;        // ShopDbContext
using Shop.Api.Integrations.Payments; // IPaymentGateway (3rd party)
using Testcontainers.PostgreSql;
using Xunit;

namespace Shop.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the real API (full DI, middleware, routing, validation, EF, auth)
/// against a disposable Postgres container. Only third-party dependencies are mocked.
/// Lifetime: one container + one host per test class collection.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime

{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()

        .WithImage("postgres:16-alpine")

        .WithDatabase("shop_tests")

        .Build();

    public Mock<IPaymentGateway> PaymentGatewayMock { get; } = new(MockBehavior.Strict);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTests");
        builder.ConfigureTestServices(services =>
        {
            // Swap EF Core to the test container.
            services.RemoveAll<DbContextOptions<ShopDbContext>>();
            services.AddDbContext<ShopDbContext>(o => o.UseNpgsql(_db.GetConnectionString()));
            // Mock ONLY third-party integrations.
            services.RemoveAll<IPaymentGateway>();
            services.AddSingleton(PaymentGatewayMock.Object);
            // Deterministic test authentication.
            services.AddTestAuthentication();
        });
    }

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ShopDbContext>().Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await base.DisposeAsync();
    }

    public ShopDbContext CreateDbContext() =>
        Services.CreateScope().ServiceProvider.GetRequiredService<ShopDbContext>();

    public HttpClient CreateClientFor(string role)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test", role);

        return client;
    }
}

[CollectionDefinition(nameof(ApiCollection))]
public sealed class ApiCollection : ICollectionFixture<ApiFactory> { }
 
//and Test data builder (maintainability helper):

using Shop.Api.Domain;
namespace Shop.Api.IntegrationTests.Infrastructure;

internal sealed class CustomerBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _email = $"user-{Guid.NewGuid():N}@example.com";
    private string _name = "Default Name";
    private bool _hasOpenOrders;

    public CustomerBuilder WithEmail(string email) { _email = email; return this; }
    public CustomerBuilder WithName(string name) { _name = name; return this; }
    public CustomerBuilder WithOpenOrders() { _hasOpenOrders = true; return this; }

    public Customer Build() => new()
    {
        Id = _id,
        Email = _email,
        Name = _name,
        HasOpenOrders = _hasOpenOrders,
        CreatedAt = DateTime.UtcNow
    };
}

// CRUD controller integration tests - happy + negative flow examples:

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Shop.Api.Contracts.Customers;            // CreateCustomerRequest, CustomerResponse
using Shop.Api.IntegrationTests.Infrastructure;
using Xunit;
 
namespace Shop.Api.IntegrationTests.Controllers;
 
[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]

public sealed class CustomersControllerTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public CustomersControllerTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClientFor(role: "Admin");
    }

    // Autonomy: every test starts from an empty Customers table.
    public async Task InitializeAsync()
    {
        await using var db = _factory.CreateDbContext();
        await db.Customers.ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
    // happy path

    [Fact]

    public async Task Create_WithValidPayload_ReturnsCreatedAndPersistsCustomer()
    {
        // Arrange
        var request = new CreateCustomerRequest(
            Email: alice@example.com,
            Name:  "Alice Johnson");

        // Act
        var response = await _client.PostAsJsonAsync("/api/customers", request);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().StartWith("/api/customers/");

        // Assert - response body (real values, not just NotNull)
        var body = await response.Content.ReadFromJsonAsync<CustomerResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().NotBeEmpty();
        body.Email.Should().Be(request.Email);
        body.Name.Should().Be(request.Name);
        body.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));

        // Assert - database state
        await using var db = _factory.CreateDbContext();
        var persisted = await db.Customers.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == body.Id);

        persisted.Should().NotBeNull();
        persisted!.Email.Should().Be(request.Email);
        persisted.Name.Should().Be(request.Name);
        persisted.HasOpenOrders.Should().BeFalse();
    }

    // negative flow
    [Fact]
    public async Task Delete_WhenCustomerHasOpenOrders_ReturnsConflictAndKeepsRow()
    {
        // Arrange - seed a customer that violates the delete business rule.
        var customer = new CustomerBuilder()
            .WithEmail(bob@example.com)
            .WithName("Bob Smith")
            .WithOpenOrders()
            .Build();

        await using (var seed = _factory.CreateDbContext())
        {
            seed.Customers.Add(customer);
            await seed.SaveChangesAsync();
        }

        // Act
        var response = await _client.DeleteAsync($"/api/customers/{customer.Id}");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Assert - RFC 7807 problem details with meaningful explanation
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Title.Should().Be("Customer cannot be deleted");
        problem.Status.Should().Be((int)HttpStatusCode.Conflict);
        problem.Detail.Should().Contain("open orders");
        problem.Extensions.Should().ContainKey("customerId")
            .WhoseValue!.ToString().Should().Be(customer.Id.ToString());

        // Assert - database state unchanged
        await using var db = _factory.CreateDbContext();
        var stillThere = await db.Customers.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == customer.Id);

        stillThere.Should().NotBeNull();
        stillThere!.HasOpenOrders.Should().BeTrue();
        stillThere.Email.Should().Be(bob@example.com);
    }
}