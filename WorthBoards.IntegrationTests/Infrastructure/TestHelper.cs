using System.Net.Http.Json;
using System.Net.Http.Headers;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;

namespace WorthBoards.IntegrationTests.Infrastructure;

public class TestHelper
{
    private readonly HttpClient _httpClient;
    private readonly IntegrationTestBase _testBase;

    public TestHelper(HttpClient httpClient, IntegrationTestBase testBase)
    {
        _httpClient = httpClient;
        _testBase = testBase;
    }

    public async Task<(int UserId, string Token, UserLoginResponse Response)> CreateAndLoginUserAsync(
        string? userName = null, 
        string? email = null)
    {
        var registerRequest = TestDataBuilder.CreateUserRegisterRequest(userName, email);
        
        // Register user
        var registerResponse = await _httpClient.PostAsJsonAsync("/register", registerRequest);
        registerResponse.EnsureSuccessStatusCode();
        var registerResult = await registerResponse.Content.ReadFromJsonAsync<UserLoginResponse>();

        // Login to get token
        var loginRequest = TestDataBuilder.CreateUserLoginRequest(
            registerRequest.UserName,
            registerRequest.Password
        );

        var loginResponse = await _httpClient.PostAsJsonAsync("/login", loginRequest);
        loginResponse.EnsureSuccessStatusCode();
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<UserLoginResponse>();

        var token = _testBase.GenerateJwtToken(loginResult.Id, registerRequest.Email);

        return (loginResult.Id, token, loginResult);
    }

    public async Task<BoardResponse> CreateBoardAsync(
        int userId,
        string? token = null,
        string? title = null,
        string? description = null)
    {
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var boardRequest = TestDataBuilder.CreateBoardRequest(title, description);
        var response = await _httpClient.PostAsJsonAsync("api/boards", boardRequest);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<BoardResponse>();
    }

    public async Task<BoardTaskResponse> CreateBoardTaskAsync(
        int boardId,
        string token,
        string? title = null,
        TaskStatusEnum? status = null)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var taskRequest = TestDataBuilder.CreateBoardTaskRequest(title, status: status);
        var response = await _httpClient.PostAsJsonAsync(
            $"api/boards/{boardId}/tasks",
            taskRequest
        );
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<BoardTaskResponse>();
    }

    public async Task<CommentResponse> CreateCommentAsync(
        int boardId,
        int taskId,
        string token,
        string? content = null)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var commentRequest = TestDataBuilder.CreateCommentRequest(content);
        var response = await _httpClient.PostAsJsonAsync(
            $"api/boards/{boardId}/tasks/{taskId}/comments",
            commentRequest
        );
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<CommentResponse>();
    }

    public async Task LinkUserToBoardAsync(
        int boardId,
        int userId,
        string token,
        UserRoleEnum role = UserRoleEnum.VIEWER)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var linkRequest = TestDataBuilder.CreateLinkUserToBoardRequest(role);
        var response = await _httpClient.PostAsJsonAsync(
            $"api/boards/{boardId}/link/{userId}",
            linkRequest
        );
        response.EnsureSuccessStatusCode();
    }

    public async Task LinkUserToTaskAsync(
        int boardId,
        int taskId,
        int userId,
        string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var linkRequest = new[] { TestDataBuilder.CreateLinkUserToTaskRequest(userId) };
        var response = await _httpClient.PostAsJsonAsync(
            $"api/boards/{boardId}/tasks/{taskId}/users/link",
            linkRequest
        );
        response.EnsureSuccessStatusCode();
    }

    public void ClearAuthHeader()
    {
        _httpClient.DefaultRequestHeaders.Authorization = null;
    }
}

public record UserLoginResponse(int Id, string Token);
public record BoardTaskResponse(int Id, int BoardId, string Title, string? Description, TaskStatusEnum TaskStatus);
public record CommentResponse(int Id, int TaskId, int UserId, string Content, DateTime CreationDate);
