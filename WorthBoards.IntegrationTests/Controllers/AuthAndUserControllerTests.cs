using System.Net;
using System.Text.Json;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Exceptions.Custom;

namespace WorthBoards.IntegrationTests.Controllers;

public sealed class AuthAndUserControllerTests
{
    [Fact]
    public async Task RegisterUser_ReturnsOk_WhenRegistrationSucceeds()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.AuthServiceMock
            .Setup(service => service.RegisterUserAsync(It.IsAny<UserRegisterRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserResponse
            {
                Id = 7,
                FirstName = "Jane",
                LastName = "Doe",
                UserName = "jane.doe",
                Email = "jane@example.com",
                CreationDate = DateTime.UtcNow
            });

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(
            HttpMethod.Post,
            "/register",
            new UserRegisterRequest("Jane", "Doe", "jane.doe", "jane@example.com", "P@ssw0rd!"),
            userId: null,
            email: null));

        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("jane.doe", body);
        factory.AuthServiceMock.Verify(service => service.RegisterUserAsync(It.IsAny<UserRegisterRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterUser_ReturnsBadRequest_WhenServiceThrowsBadRequestException()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.AuthServiceMock
            .Setup(service => service.RegisterUserAsync(It.IsAny<UserRegisterRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BadRequestException("Email already exists"));

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(
            HttpMethod.Post,
            "/register",
            new UserRegisterRequest("Jane", "Doe", "jane.doe", "jane@example.com", "P@ssw0rd!"),
            userId: null,
            email: null));

        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Email already exists", body);
    }

    [Fact]
    public async Task LoginUser_ReturnsOk_WhenCredentialsAreValid()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.AuthServiceMock
            .Setup(service => service.LoginUserAsync(It.IsAny<UserLoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserLoginResponse(7, "jane.doe", "jwt-token"));

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(
            HttpMethod.Post,
            "/login",
            new UserLoginRequest("jane.doe", "P@ssw0rd!"),
            userId: null,
            email: null));

        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("jwt-token", body);
    }

    [Fact]
    public async Task LoginUser_ReturnsUnauthorized_WhenServiceThrowsUnauthorizedException()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.AuthServiceMock
            .Setup(service => service.LoginUserAsync(It.IsAny<UserLoginRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedException("Invalid credentials"));

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(
            HttpMethod.Post,
            "/login",
            new UserLoginRequest("jane.doe", "bad-password"),
            userId: null,
            email: null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_ReturnsOk_WhenEmailClaimIsPresent()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.AuthServiceMock
            .Setup(service => service.ChangePasswordAsync(
                It.IsAny<ChangePasswordRequest>(),
                "tester@example.com",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChangePasswordResponse("Password changed"));

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(
            HttpMethod.Post,
            "/change-password",
            new ChangePasswordRequest { OldPassword = "OldP@ss1", NewPassword = "NewP@ss1" },
            userId: 1,
            email: "tester@example.com"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_ReturnsInternalServerError_WhenEmailClaimIsMissing()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(
            HttpMethod.Post,
            "/change-password",
            new ChangePasswordRequest { OldPassword = "OldP@ss1", NewPassword = "NewP@ss1" },
            userId: 1,
            email: null));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task GetUserById_ReturnsOk_WhenServiceSucceeds()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.UserServiceMock
            .Setup(service => service.GetUserById(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserResponse
            {
                Id = 7,
                FirstName = "Jane",
                LastName = "Doe",
                UserName = "jane.doe",
                Email = "jane@example.com",
                CreationDate = DateTime.UtcNow
            });

        var response = await client.GetAsync("/api/users/7");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateUser_ReturnsOk_WhenServiceSucceeds()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.UserServiceMock
            .Setup(service => service.UpdateUser(7, It.IsAny<UserUpdateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserUpdateResponse
            {
                FirstName = "Jane",
                LastName = "Doe",
                UserName = "jane.doe",
                Email = "jane@example.com",
                ImageURL = null
            });

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(
            HttpMethod.Put,
            "/api/users/7",
            new UserUpdateRequest { FirstName = "Jane", LastName = "Doe", UserName = "jane.doe", Email = "jane@example.com", ImageName = "avatar.png" }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}