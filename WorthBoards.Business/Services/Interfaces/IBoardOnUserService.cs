using Microsoft.AspNetCore.JsonPatch;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;

namespace WorthBoards.Business.Services.Interfaces
{
    public interface IBoardOnUserService
    {
        Task<IEnumerable<LinkedUserToBoardResponse>> GetAllBoardToUserLinks(int boardId, CancellationToken cancellationToken);
        Task<LinkedUserToBoardResponse> GetBoardToUserLink(int boardId, int userId, CancellationToken cancellationToken);
        Task<LinkedUserToBoardResponse> LinkUserToBoard(int boardId, int userId, LinkUserToBoardRequest linkUserToBoardRequest, CancellationToken cancellationToken);
        Task UnlinkUserFromBoard(int boardId, int userId, int responsibleUserId, CancellationToken cancellationToken);
        Task<LinkedUserToBoardResponse> UpdateUserOnBoard(int boardId, int userId, LinkUserToBoardRequest linkUserToBoardRequest, CancellationToken cancellationToken);
        Task<LinkedUserToBoardResponse> PatchUserOnBoard(int boardId, int userId, JsonPatchDocument<LinkUserToBoardRequest> linkUserToBoardPatchDoc, CancellationToken cancellationToken);
        Task<IEnumerable<LinkedUserToBoardResponse>> GetUsersLinkedToBoardAsync(int boardId, CancellationToken cancellationToken);
        Task<List<UserResponse>> GetUsersByUserNameAsync(int boardId, string userName, CancellationToken cancellationToken);
        Task TransferOwnershipAsync(int boardId, int currentOwnerId, int newOwnerId, CancellationToken cancellationToken);
    }
}
