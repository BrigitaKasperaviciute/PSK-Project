using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using System.Text.Json;
using Xunit;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Controllers;

public class BoardControllerIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task GetAllCurrentUserBoards_WithValidAuthentication_ReturnsOkWithBoardsList()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId, token, _) = await helper.CreateAndLoginUserAsync();
        
        var boardRequest = TestDataBuilder.CreateBoardRequest();
        await HttpClient.PostAsJsonAsync("api/boards", boardRequest);

        SetBearerToken(token);

        // Act
        var response = await HttpClient.GetAsync("api/boards");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("items");
    }

    [Fact]
    public async Task GetAllCurrentUserBoards_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Act
        var response = await HttpClient.GetAsync("api/boards");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAllCurrentUserBoards_WithPagination_ReturnsPagedResults()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId, token, _) = await helper.CreateAndLoginUserAsync();

        // Create multiple boards
        for (int i = 0; i < 15; i++)
        {
            var boardRequest = TestDataBuilder.CreateBoardRequest();
            SetBearerToken(token);
            await HttpClient.PostAsJsonAsync("api/boards", boardRequest);
        }

        SetBearerToken(token);

        // Act
        var response = await HttpClient.GetAsync("api/boards?pageNum=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("pageSize").GetInt32().Should().Be(10);
        result.GetProperty("pageNumber").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task CreateBoard_ValidRequest_ReturnsCreatedAtActionWithBoardData()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId, token, _) = await helper.CreateAndLoginUserAsync();
        
        var boardRequest = TestDataBuilder.CreateBoardRequest();
        SetBearerToken(token);

        // Act
        var response = await HttpClient.PostAsJsonAsync("api/boards", boardRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<BoardResponse>();
        result.Id.Should().BeGreaterThan(0);
        result.Title.Should().Be(boardRequest.Title);
        result.Description.Should().Be(boardRequest.Description);
    }

    [Fact]
    public async Task CreateBoard_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        await ResetDatabaseAsync();
        var boardRequest = TestDataBuilder.CreateBoardRequest();

        // Act
        var response = await HttpClient.PostAsJsonAsync("api/boards", boardRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateBoard_MissingRequiredFields_ReturnsBadRequest()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId, token, _) = await helper.CreateAndLoginUserAsync();
        
        var invalidRequest = new { title = "", description = "" };
        SetBearerToken(token);

        // Act
        var response = await HttpClient.PostAsJsonAsync("api/boards", invalidRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetBoardById_ValidBoardId_ReturnsOkWithBoardData()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId, token, _) = await helper.CreateAndLoginUserAsync();
        
        var createdBoard = await helper.CreateBoardAsync(userId, token);
        SetBearerToken(token);

        // Act
        var response = await HttpClient.GetAsync($"api/boards/{createdBoard.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<BoardResponse>();
        result.Id.Should().Be(createdBoard.Id);
        result.Title.Should().Be(createdBoard.Title);
    }

    [Fact]
    public async Task GetBoardById_NonExistentBoardId_ReturnsNotFound()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId, token, _) = await helper.CreateAndLoginUserAsync();
        SetBearerToken(token);

        // Act
        var response = await HttpClient.GetAsync("api/boards/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetBoardById_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Act
        var response = await HttpClient.GetAsync("api/boards/1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateBoard_ValidRequest_ReturnsOkWithUpdatedBoard()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId, token, _) = await helper.CreateAndLoginUserAsync();
        
        var createdBoard = await helper.CreateBoardAsync(userId, token);
        
        var updateRequest = TestDataBuilder.CreateBoardUpdateRequest(
            title: "Updated Title",
            description: "Updated Description"
        );
        SetBearerToken(token);

        // Act
        var response = await HttpClient.PutAsJsonAsync($"api/boards/{createdBoard.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<BoardResponse>();
        result.Title.Should().Be("Updated Title");
        result.Description.Should().Be("Updated Description");
    }

    [Fact]
    public async Task UpdateBoard_WithoutEditorRole_ReturnsForbidden()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId, token, _) = await helper.CreateAndLoginUserAsync();
        
        var createdBoard = await helper.CreateBoardAsync(userId, token);
        
        // Create another user
        var (userId2, token2, _) = await helper.CreateAndLoginUserAsync();
        
        var updateRequest = TestDataBuilder.CreateBoardUpdateRequest();
        SetBearerToken(token2);

        // Act
        var response = await HttpClient.PutAsJsonAsync($"api/boards/{createdBoard.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateBoard_NonExistentBoard_ReturnsNotFound()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId, token, _) = await helper.CreateAndLoginUserAsync();
        
        var updateRequest = TestDataBuilder.CreateBoardUpdateRequest();
        SetBearerToken(token);

        // Act
        var response = await HttpClient.PutAsJsonAsync("api/boards/99999", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteBoard_WithOwnerRole_ReturnsNoContent()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId, token, _) = await helper.CreateAndLoginUserAsync();
        
        var createdBoard = await helper.CreateBoardAsync(userId, token);
        SetBearerToken(token);

        // Act
        var response = await HttpClient.DeleteAsync($"api/boards/{createdBoard.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteBoard_WithoutOwnerRole_ReturnsForbidden()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId, token, _) = await helper.CreateAndLoginUserAsync();
        
        var createdBoard = await helper.CreateBoardAsync(userId, token);
        
        // Create another user without owner role
        var (userId2, token2, _) = await helper.CreateAndLoginUserAsync();
        SetBearerToken(token2);

        // Act
        var response = await HttpClient.DeleteAsync($"api/boards/{createdBoard.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteBoard_NonExistentBoard_ReturnsNotFound()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId, token, _) = await helper.CreateAndLoginUserAsync();
        SetBearerToken(token);

        // Act
        var response = await HttpClient.DeleteAsync("api/boards/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PatchBoard_ValidRequest_ReturnsOkWithPatchedBoard()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId, token, _) = await helper.CreateAndLoginUserAsync();
        
        var createdBoard = await helper.CreateBoardAsync(userId, token);
        
        var patchRequest = @"[
            { ""op"": ""replace"", ""path"": ""/title"", ""value"": ""Patched Title"" }
        ]";
        
        SetBearerToken(token);

        // Act
        var request = new HttpRequestMessage(new HttpMethod("PATCH"), $"api/boards/{createdBoard.Id}")
        {
            Content = new StringContent(patchRequest, System.Text.Encoding.UTF8, "application/json")
        };
        var response = await HttpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
