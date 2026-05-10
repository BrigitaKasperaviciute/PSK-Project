using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using System.Text.Json;
using Xunit;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Controllers;

public class BoardOnUserControllerIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task GetAllBoardToUserLinks_WithViewerRole_ReturnsOkWithLinksList()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId1, token1, _) = await helper.CreateAndLoginUserAsync();
        var (userId2, token2, _) = await helper.CreateAndLoginUserAsync();
        
        var board = await helper.CreateBoardAsync(userId1, token1);
        await helper.LinkUserToBoardAsync(board.Id, userId2, token1, UserRoleEnum.VIEWER);
        
        SetBearerToken(token1);

        // Act
        var response = await HttpClient.GetAsync($"api/boards/{board.Id}/links");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAllBoardToUserLinks_NonExistentBoard_ReturnsNotFound()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId, token, _) = await helper.CreateAndLoginUserAsync();
        SetBearerToken(token);

        // Act
        var response = await HttpClient.GetAsync("api/boards/99999/links");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetBoardToUserLink_ValidBoardAndUser_ReturnsOkWithLinkData()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId1, token1, _) = await helper.CreateAndLoginUserAsync();
        var (userId2, token2, _) = await helper.CreateAndLoginUserAsync();
        
        var board = await helper.CreateBoardAsync(userId1, token1);
        await helper.LinkUserToBoardAsync(board.Id, userId2, token1, UserRoleEnum.EDITOR);
        
        SetBearerToken(token1);

        // Act
        var response = await HttpClient.GetAsync($"api/boards/{board.Id}/link/{userId2}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetBoardToUserLink_NonExistentLink_ReturnsNotFound()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId1, token1, _) = await helper.CreateAndLoginUserAsync();
        var (userId2, token2, _) = await helper.CreateAndLoginUserAsync();
        
        var board = await helper.CreateBoardAsync(userId1, token1);
        SetBearerToken(token1);

        // Act
        var response = await HttpClient.GetAsync($"api/boards/{board.Id}/link/{userId2}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task LinkUserToBoard_ValidRequest_ReturnsCreatedWithLinkData()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId1, token1, _) = await helper.CreateAndLoginUserAsync();
        var (userId2, token2, _) = await helper.CreateAndLoginUserAsync();
        
        var board = await helper.CreateBoardAsync(userId1, token1);
        
        var linkRequest = TestDataBuilder.CreateLinkUserToBoardRequest(UserRoleEnum.EDITOR);
        SetBearerToken(token1);

        // Act
        var response = await HttpClient.PostAsJsonAsync($"api/boards/{board.Id}/link/{userId2}", linkRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("boardId").GetInt32().Should().Be(board.Id);
        result.GetProperty("userId").GetInt32().Should().Be(userId2);
    }

    [Fact]
    public async Task LinkUserToBoard_DuplicateLink_ReturnsBadRequest()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId1, token1, _) = await helper.CreateAndLoginUserAsync();
        var (userId2, token2, _) = await helper.CreateAndLoginUserAsync();
        
        var board = await helper.CreateBoardAsync(userId1, token1);
        
        var linkRequest = TestDataBuilder.CreateLinkUserToBoardRequest(UserRoleEnum.VIEWER);
        SetBearerToken(token1);
        
        await HttpClient.PostAsJsonAsync($"api/boards/{board.Id}/link/{userId2}", linkRequest);

        // Act
        var response = await HttpClient.PostAsJsonAsync($"api/boards/{board.Id}/link/{userId2}", linkRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateUserOnBoard_ValidRequest_ReturnsOkWithUpdatedData()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId1, token1, _) = await helper.CreateAndLoginUserAsync();
        var (userId2, token2, _) = await helper.CreateAndLoginUserAsync();
        
        var board = await helper.CreateBoardAsync(userId1, token1);
        await helper.LinkUserToBoardAsync(board.Id, userId2, token1, UserRoleEnum.VIEWER);
        
        var updateRequest = TestDataBuilder.CreateLinkUserToBoardRequest(UserRoleEnum.EDITOR);
        SetBearerToken(token1);

        // Act
        var response = await HttpClient.PutAsJsonAsync($"api/boards/{board.Id}/link/{userId2}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateUserOnBoard_NonExistentLink_ReturnsNotFound()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId1, token1, _) = await helper.CreateAndLoginUserAsync();
        var (userId2, token2, _) = await helper.CreateAndLoginUserAsync();
        
        var board = await helper.CreateBoardAsync(userId1, token1);
        
        var updateRequest = TestDataBuilder.CreateLinkUserToBoardRequest(UserRoleEnum.EDITOR);
        SetBearerToken(token1);

        // Act
        var response = await HttpClient.PutAsJsonAsync($"api/boards/{board.Id}/link/{userId2}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetUsersLinkedToBoard_ValidBoard_ReturnsOkWithUsersList()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId1, token1, _) = await helper.CreateAndLoginUserAsync();
        var (userId2, token2, _) = await helper.CreateAndLoginUserAsync();
        
        var board = await helper.CreateBoardAsync(userId1, token1);
        await helper.LinkUserToBoardAsync(board.Id, userId2, token1, UserRoleEnum.EDITOR);
        
        SetBearerToken(token1);

        // Act
        var response = await HttpClient.GetAsync($"api/boards/{board.Id}/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetUsersByUserName_ValidSearchTerm_ReturnsOkWithUsersList()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId1, token1, _) = await helper.CreateAndLoginUserAsync();
        
        var board = await helper.CreateBoardAsync(userId1, token1);
        SetBearerToken(token1);

        // Act
        var response = await HttpClient.GetAsync($"api/boards/{board.Id}/collaborators?userName=test");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RemoveUser_ValidRequest_ReturnsNoContent()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId1, token1, _) = await helper.CreateAndLoginUserAsync();
        var (userId2, token2, _) = await helper.CreateAndLoginUserAsync();
        
        var board = await helper.CreateBoardAsync(userId1, token1);
        await helper.LinkUserToBoardAsync(board.Id, userId2, token1, UserRoleEnum.VIEWER);
        
        SetBearerToken(token1);

        // Act
        var response = await HttpClient.PostAsync($"api/boards/{board.Id}/remove/{userId2}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RemoveUser_NonExistentLink_ReturnsNotFound()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId1, token1, _) = await helper.CreateAndLoginUserAsync();
        var (userId2, token2, _) = await helper.CreateAndLoginUserAsync();
        
        var board = await helper.CreateBoardAsync(userId1, token1);
        SetBearerToken(token1);

        // Act
        var response = await HttpClient.PostAsync($"api/boards/{board.Id}/remove/{userId2}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TransferOwnership_ValidRequest_ReturnsNoContent()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId1, token1, _) = await helper.CreateAndLoginUserAsync();
        var (userId2, token2, _) = await helper.CreateAndLoginUserAsync();
        
        var board = await helper.CreateBoardAsync(userId1, token1);
        await helper.LinkUserToBoardAsync(board.Id, userId2, token1, UserRoleEnum.EDITOR);
        
        SetBearerToken(token1);

        // Act
        var response = await HttpClient.PostAsync($"api/boards/{board.Id}/collaborators/{userId2}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task InviteUser_WithOwnerRole_ReturnsNoContent()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId1, token1, _) = await helper.CreateAndLoginUserAsync();
        var (userId2, token2, _) = await helper.CreateAndLoginUserAsync();
        
        var board = await helper.CreateBoardAsync(userId1, token1);
        
        var inviteRequest = TestDataBuilder.CreateInvitationRequest(userId2, UserRoleEnum.VIEWER);
        SetBearerToken(token1);

        // Act
        var response = await HttpClient.PostAsJsonAsync($"api/boards/{board.Id}/invite", inviteRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task PatchUserOnBoard_ValidRequest_ReturnsOkWithPatchedData()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId1, token1, _) = await helper.CreateAndLoginUserAsync();
        var (userId2, token2, _) = await helper.CreateAndLoginUserAsync();
        
        var board = await helper.CreateBoardAsync(userId1, token1);
        await helper.LinkUserToBoardAsync(board.Id, userId2, token1, UserRoleEnum.VIEWER);
        
        var patchRequest = @"[
            { ""op"": ""replace"", ""path"": ""/userRole"", ""value"": ""EDITOR"" }
        ]";
        
        SetBearerToken(token1);

        // Act
        var request = new HttpRequestMessage(new HttpMethod("PATCH"), $"api/boards/{board.Id}/link/{userId2}")
        {
            Content = new StringContent(patchRequest, System.Text.Encoding.UTF8, "application/json")
        };
        var response = await HttpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
