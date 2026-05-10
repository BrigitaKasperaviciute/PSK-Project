using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WorthBoards.Api.Filters.ActionFilters;
using WorthBoards.Api.Utils;
using WorthBoards.Business.Services.Interfaces;
using WorthBoards.Common.Enums;

namespace WorthBoards.IntegrationTests;

public sealed class BranchCoverageTests
{
    [Fact]
    public void GetUserId_ReturnsParsedValue_WhenClaimIsValid()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "17")
        }));

        var result = UserHelper.GetUserId(principal);

        Assert.Equal(17, result.Value);
    }

    [Fact]
    public void GetUserId_ReturnsUnauthorized_WhenClaimIsMissingOrInvalid()
    {
        var missingClaimPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        var invalidClaimPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "not-a-number")
        }));

        Assert.IsType<UnauthorizedObjectResult>(UserHelper.GetUserId(missingClaimPrincipal).Result);
        Assert.IsType<UnauthorizedObjectResult>(UserHelper.GetUserId(invalidClaimPrincipal).Result);
    }

    [Fact]
    public async Task PermissionHandler_Succeeds_WhenRouteAndRoleAreValid()
    {
        var boardService = new Mock<IBoardService>(MockBehavior.Strict);
        boardService
            .Setup(service => service.GetUserRoleByBoardIdAndUserIdAsync(5, 7))
            .ReturnsAsync(UserRoleEnum.EDITOR);

        var handler = new PermissionHandler(boardService.Object);
        var requirement = new PermissionRequirement(UserRoleEnum.EDITOR);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues["boardId"] = 5;
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "7")
        }, "Test"));
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, httpContext);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
        boardService.Verify(service => service.GetUserRoleByBoardIdAndUserIdAsync(5, 7), Times.Once);
    }

    [Fact]
    public async Task PermissionHandler_ReturnsWithoutSuccess_WhenContextCannotResolveRouteOrUser()
    {
        var boardService = new Mock<IBoardService>(MockBehavior.Strict);
        var handler = new PermissionHandler(boardService.Object);
        var requirement = new PermissionRequirement(UserRoleEnum.EDITOR);

        var nullResourceContext = new AuthorizationHandlerContext(new[] { requirement }, new ClaimsPrincipal(new ClaimsIdentity()), null);
        await handler.HandleAsync(nullResourceContext);

        var routeWithoutBoardId = new DefaultHttpContext();
        routeWithoutBoardId.Request.RouteValues["taskId"] = 11;
        var noUserContext = new AuthorizationHandlerContext(new[] { requirement }, new ClaimsPrincipal(new ClaimsIdentity()), routeWithoutBoardId);
        await handler.HandleAsync(noUserContext);

        Assert.False(nullResourceContext.HasSucceeded);
        Assert.False(noUserContext.HasSucceeded);
    }

    [Fact]
    public async Task ControllerLoggingActionFilter_UsesBoardRouteWhenAvailable()
    {
        var boardService = new Mock<IBoardService>(MockBehavior.Strict);
        boardService
            .Setup(service => service.GetUserRoleByBoardIdAndUserIdAsync(5, 7))
            .ReturnsAsync(UserRoleEnum.OWNER);

        var logger = new Mock<ILogger<ControllerLoggingActionFilter>>();
        var filter = new ControllerLoggingActionFilter(logger.Object, boardService.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "7"),
            new Claim(ClaimTypes.Name, "tester")
        }, "Test"));
        httpContext.Request.RouteValues["boardId"] = 5;

        var actionContext = new ActionContext(httpContext, new RouteData(new RouteValueDictionary
        {
            ["controller"] = "Board",
            ["action"] = "GetBoardById"
        }), new ControllerActionDescriptor());
        var executingContext = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(), new object());
        var executedContext = new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), new object());

        await filter.OnActionExecuting(executingContext);
        filter.OnActionExecuted(executedContext);

        boardService.Verify(service => service.GetUserRoleByBoardIdAndUserIdAsync(5, 7), Times.Once);
    }

    [Fact]
    public async Task ControllerLoggingActionFilter_FallsBackWhenRouteHasNoBoardId()
    {
        var boardService = new Mock<IBoardService>(MockBehavior.Strict);
        var logger = new Mock<ILogger<ControllerLoggingActionFilter>>();
        var filter = new ControllerLoggingActionFilter(logger.Object, boardService.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "7"),
            new Claim(ClaimTypes.Name, "tester")
        }, "Test"));

        var actionContext = new ActionContext(httpContext, new RouteData(new RouteValueDictionary()), new ControllerActionDescriptor());
        var executingContext = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(), new object());
        var executedContext = new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), new object());

        await filter.OnActionExecuting(executingContext);
        filter.OnActionExecuted(executedContext);

        boardService.VerifyNoOtherCalls();
    }
}

public sealed class DevelopmentProgramStartupTests
{
    [Fact]
    public void Program_BuildsInDevelopmentMode_WithControllerFilterDisabled()
    {
        using var factory = new DevelopmentProgramFactory();
        using var client = factory.CreateClient();

        Assert.NotNull(client);
    }

    private sealed class DevelopmentProgramFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            TestEnvironment.EnsureConfigured();

            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Logger:UseHttpLoggingMiddleware"] = "false",
                    ["Logger:UseControllerLoggingActionFilter"] = "false"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    options.DefaultScheme = TestAuthHandler.SchemeName;
                }).AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
            });
        }
    }
}