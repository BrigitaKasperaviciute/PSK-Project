using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using WorthBoards.Business.Services.Interfaces;
using WorthBoards.Common.Enums;

namespace WorthBoards.Api.Utils
{
    public class PermissionHandler(IBoardService boardService, IHttpContextAccessor httpContextAccessor) : AuthorizationHandler<PermissionRequirement>
    {
        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
        {
            // In .NET 8 endpoint routing, context.Resource is the Endpoint, not HttpContext.
            // IHttpContextAccessor is the reliable way to access the current HttpContext.
            var httpContext = httpContextAccessor.HttpContext;
            var routeValues = httpContext?.Request.RouteValues;

            if (routeValues is null)
            {
                return;
            }

            var boardIdObj = routeValues.FirstOrDefault(kv => kv.Key.ToLower().EndsWith("id")).Value;
            var boardId = boardIdObj?.ToString();
            if (string.IsNullOrEmpty(boardId))
            {
                return;
            }

            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId is null)
            {
                return;
            }

            var userRole = await boardService.GetUserRoleByBoardIdAndUserIdAsync(int.Parse(boardId), int.Parse(userId));

            if (userRole <= Enum.Parse<UserRoleEnum>(requirement.Role))
                context.Succeed(requirement);
        }
    }
}
