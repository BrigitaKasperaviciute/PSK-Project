# Performance Tests

This directory contains performance tests that run each endpoint **20 times** and calculate statistical metrics.

## Test Structure

- **Regular Tests** (`Controllers/` folder): Standard integration tests (1 execution per test)
- **Performance Tests** (`Performance/` folder): Tests that execute 20 times with performance metrics

## Running Performance Tests

### Quick Run (All Performance Tests)

**Windows (PowerShell):**
```powershell
.\run-performance-tests.ps1
```

**Linux/Mac:**
```bash
chmod +x run-performance-tests.sh
./run-performance-tests.sh
```

### Manual Run

**All performance tests:**
```bash
dotnet test --filter "FullyQualifiedName~Performance"
```

**Specific controller:**
```bash
dotnet test --filter "FullyQualifiedName~Performance.AuthController"
```

**Single test:**
```bash
dotnet test --filter "Login_WithValidCredentials_PerformanceTest"
```

## Performance Metrics

Each test provides the following metrics after 20 iterations:

- **Average**: Mean execution time across all iterations
- **Median**: Middle value of execution times (less affected by outliers)
- **Min**: Fastest execution time
- **Max**: Slowest execution time
- **Std Dev**: Standard deviation (shows consistency)

### Example Output

```
Performance Test Results (20 iterations):
================================================
Average:    148.05 ms
Median:     122.00 ms
Min:        119 ms
Max:        545 ms
Std Dev:    91.66 ms
================================================
```

## Test Coverage

### 18 Performance Tests (20 iterations each)

1. **AuthController** (2 tests)
   - Login with valid credentials
   - Login with invalid password

2. **BoardController** (2 tests)
   - Get board by ID (exists)
   - Get board by ID (not found)

3. **BoardOnUserController** (2 tests)
   - Get board-to-user link (exists)
   - Get board-to-user link (not found)

4. **BoardTaskController** (2 tests)
   - Get task by ID (exists)
   - Get task by ID (not found)

5. **CommentController** (2 tests)
   - Get comment by ID (exists)
   - Get comment by ID (not found)

6. **NotificationController** (2 tests)
   - Delete notification (exists)
   - Delete notification (not found)

7. **TaskOnUserController** (2 tests)
   - Get users linked to task (exists)
   - Get users linked to task (not found)

8. **UploadController** (2 tests)
   - Upload image (authorized)
   - Upload image (unauthorized)

9. **UserController** (2 tests)
   - Get user by ID (exists)
   - Get user by ID (not found)

## Customizing Iterations

To change the number of iterations, modify the `DefaultIterations` constant in `Infrastructure/PerformanceTestBase.cs`:

```csharp
private const int DefaultIterations = 20; // Change this value
```

Or pass a custom value when calling the test:

```csharp
var result = await RunPerformanceTestAsync(async () =>
{
    // test code
}, iterations: 50); // Custom iteration count
```

## Performance Test Infrastructure

- **PerformanceTestBase**: Base class that provides performance testing capabilities
- **PerformanceResult**: Contains statistical metrics for test results
- Database is cleared before each iteration to ensure consistency
- Each test creates unique test data (using GUIDs) to avoid conflicts

## Best Practices

1. **Warm-up**: The first iteration may be slower due to JIT compilation and caching
2. **Isolation**: Each iteration runs with a clean database state
3. **Consistency**: Tests use the same data structure across iterations
4. **Statistics**: Multiple metrics help identify performance patterns and outliers

## Interpreting Results

- **Low Standard Deviation**: Consistent performance across iterations
- **High Standard Deviation**: Variable performance (may indicate caching effects or GC)
- **Median vs Average**: If significantly different, there may be outliers
- **Min vs Max**: Large gap indicates variable performance

## Total Test Count

- **18 Regular Tests**: Standard integration tests
- **18 Performance Tests**: 20 iterations each = 360 total executions
- **Total: 36 tests** in the test suite
