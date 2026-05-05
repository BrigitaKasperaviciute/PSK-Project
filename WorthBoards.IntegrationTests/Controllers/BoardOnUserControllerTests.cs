using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.Common.Exceptions;
using WorthBoards.Data.Database;
using WorthBoards.IntegrationTests.Infrastructure;
using Xunit;

namespace WorthBoards.IntegrationTests.Controllers;

[Collection(nameof(IntegrationCollection))]
public sealed class BoardOnUserControllerTests(IntegrationTestAppFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task InviteUser_WithOwnerRole_ReturnsNoContentAndCreatesInvitationNotification()
    {
        // Arrange
        var owner = await Factory.CreateUserAsync("Holly", "Grant", "invite-owner");
        var invitee = await Factory.CreateUserAsync("Ian", "Brooks", "invitee");
        var board = await Factory.SeedBoardAsync(owner.Id, "Hiring board", "Invitation flow");
        var client = Factory.CreateAuthenticatedClient(owner);
        var request = new InvitationRequest
        {
            UserId = invitee.Id,
            Role = UserRoleEnum.VIEWER
        };

        // Act
        var response = await client.PostAsJsonAsync($"/api/boards/{board.Id}/invite", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await Factory.WithDbContextAsync(async db =>
        {
            var notification = await db.Notifications.AsNoTracking()
                .SingleAsync(item => item.BoardId == board.Id && item.SubjectUserId == invitee.Id);

            notification.NotificationType.Should().Be(NotificationEventTypeEnum.INVITATION);
            notification.InvitationRole.Should().Be(UserRoleEnum.VIEWER);

            var recipients = await db.NotificationsOnUsers.AsNoTracking()
                .Where(item => item.NotificationId == notification.Id)
                .ToListAsync();
            recipients.Should().ContainSingle(item => item.UserId == invitee.Id);
        });
    }

    [Fact]
    public async Task TransferOwnership_ToSelf_ReturnsBadRequestAndLeavesRolesUnchanged()
    {
        // Arrange
        var owner = await Factory.CreateUserAsync("Jane", "Stone", "transfer-owner");
        var board = await Factory.SeedBoardAsync(owner.Id, "Ownership board", "Transfer guard");
        var client = Factory.CreateAuthenticatedClient(owner);

        // Act
        var response = await client.PostAsync($"/api/boards/{board.Id}/collaborators/{owner.Id}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<ErrorDetails>();
        error.Should().NotBeNull();
        error!.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        error.Details.Should().Contain("ownership");

        await Factory.WithDbContextAsync(async db =>
        {
            var ownerLink = await db.BoardOnUsers.AsNoTracking()
                .SingleAsync(item => item.BoardId == board.Id && item.UserId == owner.Id);
            ownerLink.UserRole.Should().Be(UserRoleEnum.OWNER);
        });
    }

    [Fact]
    public async Task LinkUpdatePatchRemoveAndTransfer_WithOwnerRole_ExercisesBoardMembershipLifecycle()
    {
        // Arrange
        var owner = await Factory.CreateUserAsync("Uma", "North", "board-user-owner");
        var editor = await Factory.CreateUserAsync("Victor", "Cross", "board-user-editor");
        var viewer = await Factory.CreateUserAsync("Wendy", "Page", "board-user-viewer");
        var outsider = await Factory.CreateUserAsync("Xavier", "Lane", "board-user-outsider");
        var board = await Factory.SeedBoardAsync(owner.Id, "Membership board", "Lifecycle of memberships");
        await Factory.SeedBoardUserAsync(board.Id, viewer.Id, UserRoleEnum.VIEWER);
        var client = Factory.CreateAuthenticatedClient(owner);

        // Act - link editor
        var linkResponse = await client.PostAsJsonAsync($"/api/boards/{board.Id}/link/{editor.Id}", new LinkUserToBoardRequest
        {
            UserRole = UserRoleEnum.VIEWER
        });

        // Assert - link editor
        linkResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act - get links
        var linksResponse = await client.GetAsync($"/api/boards/{board.Id}/links");

        // Assert - get links
        linksResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var linksBody = await linksResponse.Content.ReadFromJsonAsync<List<LinkedUserToBoardResponse>>();
        linksBody.Should().HaveCount(3);

        // Act - get single link
        var singleLinkResponse = await client.GetAsync($"/api/boards/{board.Id}/link/{editor.Id}");

        // Assert - get single link
        singleLinkResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act - update link
        var updateLinkResponse = await client.PutAsJsonAsync($"/api/boards/{board.Id}/link/{editor.Id}", new LinkUserToBoardRequest
        {
            UserRole = UserRoleEnum.EDITOR
        });

        // Assert - update link
        updateLinkResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act - patch link
        var patchOperations = new[]
        {
            new Dictionary<string, object?> { ["op"] = "replace", ["path"] = "/userRole", ["value"] = UserRoleEnum.VIEWER }
        };
        var patchContent = new StringContent(JsonSerializer.Serialize(patchOperations), Encoding.UTF8, "application/json-patch+json");
        var patchLinkResponse = await client.PatchAsync($"/api/boards/{board.Id}/link/{editor.Id}", patchContent);

        // Assert - patch link
        patchLinkResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act - get users by username excludes linked user
        var collaboratorResponse = await client.GetAsync($"/api/boards/{board.Id}/collaborators?userName={outsider.UserName}");

        // Assert - collaborators
        collaboratorResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var collaboratorBody = await collaboratorResponse.Content.ReadFromJsonAsync<List<UserResponse>>();
        collaboratorBody.Should().HaveCount(1);

        // Act - remove viewer
        var removeResponse = await client.PostAsync($"/api/boards/{board.Id}/remove/{viewer.Id}", null);

        // Assert - remove viewer
        removeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act - transfer ownership to editor
        var transferResponse = await client.PostAsync($"/api/boards/{board.Id}/collaborators/{editor.Id}", null);

        // Assert - transfer ownership
        transferResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await Factory.WithDbContextAsync(async db =>
        {
            var ownerLink = await db.BoardOnUsers.AsNoTracking().SingleAsync(item => item.BoardId == board.Id && item.UserId == owner.Id);
            ownerLink.UserRole.Should().Be(UserRoleEnum.VIEWER);

            var editorLink = await db.BoardOnUsers.AsNoTracking().SingleAsync(item => item.BoardId == board.Id && item.UserId == editor.Id);
            editorLink.UserRole.Should().Be(UserRoleEnum.OWNER);

            var viewerLink = await db.BoardOnUsers.AsNoTracking().SingleOrDefaultAsync(item => item.BoardId == board.Id && item.UserId == viewer.Id);
            viewerLink.Should().BeNull();
        });
    }
}