# WorthBoards Integration Tests

This project contains integration tests for the WorthBoards API, covering 9 controllers with 18 tests total (2 tests per controller).

## Test Coverage

| Controller | Tests | Scenarios |
|------------|-------|-----------|
| AuthController | 2 | Valid login, Invalid password |
| BoardController | 2 | Board exists, Board not found |
| BoardOnUserController | 2 | Link exists, Link not found |
| BoardTaskController | 2 | Task exists, Task not found |
| CommentController | 2 | Comment exists, Comment not found |
| NotificationController | 2 | Delete existing, Delete non-existing |
| TaskOnUserController | 2 | Get users for task, Task not found |
| UploadController | 2 | Authorized upload, Unauthorized upload |
| UserController | 2 | User exists, User not found |

**Total: 18 integration tests**

## Running Tests

### Basic Test Execution

Run all tests once:
```bash
dotnet test
```

Run tests from solution root:
```bash
cd /path/to/PSK
dotnet test
```

### Run Tests Multiple Times with Statistics

#### Windows (PowerShell)
Run tests 20 times and get average execution time:
```powershell
.\run-tests-multiple.ps1 -Iterations 20
```

Run with custom number of iterations:
```powershell
.\run-tests-multiple.ps1 -Iterations 50
```

#### Linux/Mac (Bash)
Run tests 20 times:
```bash
./run-tests-multiple.sh 20
```

Run with custom iterations:
```bash
./run-tests-multiple.sh 50
```

### Run Specific Test

Run a single test:
```bash
dotnet test --filter "FullyQualifiedName=WorthBoards.Api.IntegrationTests.Controllers.AuthControllerTests.Login_WithValidCredentials_ReturnsToken"
```

Run all tests from one controller:
```bash
dotnet test --filter "FullyQualifiedName~AuthControllerTests"
```

## Test Architecture

### Test Infrastructure

- **TestWebApplicationFactory**: Custom WebApplicationFactory that configures the test server with:
  - In-memory database (unique per test class)
  - Test authentication handler
  - Test configuration values

- **TestAuthHandler**: Mock authentication handler that allows testing secured endpoints without real JWT tokens

- **IntegrationTestBase**: Base class for all test classes providing:
  - Pre-configured HTTP clients (authenticated and unauthenticated)
  - Database context for test data setup
  - Automatic database cleanup before each test

### Database Isolation

Each test class gets a unique in-memory database instance, and the database is cleared before each test to ensure complete isolation between tests.

## Test Results (20 Iterations)

Based on benchmark runs:
- **Average Execution Time**: ~2.3 seconds
- **Minimum Time**: 2 seconds
- **Maximum Time**: 3 seconds
- **Success Rate**: 100% (all tests passing)

## Configuration

Tests use the following configuration (defined in `TestWebApplicationFactory.cs`):
- JWT Key: Test key (32+ characters)
- Database: In-memory (unique per test class)
- Authentication: Mock authentication handler
- Email: Mock configuration (emails not actually sent)

## Adding New Tests

To add new integration tests:

1. Create a new test class in the `Controllers` folder
2. Inherit from `IntegrationTestBase`
3. Inject `TestWebApplicationFactory` in the constructor
4. Write test methods using xUnit `[Fact]` attribute

Example:
```csharp
public class MyControllerTests : IntegrationTestBase
{
    public MyControllerTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task MyEndpoint_WhenValid_ReturnsOk()
    {
        // Arrange
        var client = CreateAuthenticatedClient("1", "test@example.com");

        // Act
        var response = await client.GetAsync("/api/my-endpoint");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
```

## Troubleshooting

### Tests fail with "Key already exists"
This indicates database isolation is broken. Ensure `CleanDatabase()` is being called in `IntegrationTestBase` constructor.

### Tests fail with "Unauthorized"
Check that the test authentication handler is properly configured and the `Authorization` header is being set.

### Tests are slow
The first run is typically slower due to build and initialization. Subsequent runs are faster (2-3 seconds).

## CI/CD Integration

To integrate these tests into CI/CD pipelines:

```yaml
# Example GitHub Actions
- name: Run Integration Tests
  run: dotnet test --logger "trx;LogFileName=test-results.trx"

- name: Run Integration Tests 20 times
  run: |
    chmod +x run-tests-multiple.sh
    ./run-tests-multiple.sh 20
```

## Dependencies

- xUnit 2.9.2
- Microsoft.AspNetCore.Mvc.Testing 8.0.11
- Microsoft.EntityFrameworkCore.InMemory 8.0.11
- Microsoft.NET.Test.Sdk 17.11.1
