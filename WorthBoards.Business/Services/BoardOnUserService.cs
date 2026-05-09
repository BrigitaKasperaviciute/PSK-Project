using AutoMapper;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.Extensions.DependencyModel;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Business.Services.Interfaces;
using WorthBoards.Common.Enums;
using WorthBoards.Common.Exceptions;
using WorthBoards.Common.Exceptions.Custom;
using WorthBoards.Data.Identity;
using WorthBoards.Data.Repositories.Interfaces;
using WorthBoards.Domain.Entities;

namespace WorthBoards.Business.Services
{
    public class BoardOnUserService(IUnitOfWork _unitOfWork, IMapper _mapper, INotificationService _notificationService) : IBoardOnUserService
    {
        public async Task<IEnumerable<LinkedUserToBoardResponse>> GetAllBoardToUserLinks(int boardId, CancellationToken cancellationToken)
        {
            var boardToUserLinks = await _unitOfWork.BoardOnUserRepository.GetAllByExpressionAsync(b => b.BoardId == boardId, cancellationToken);
            var userIds = boardToUserLinks.Select(b => b.UserId).ToList();
            var users = await _unitOfWork.UserRepository.GetAllByExpressionAsync(u => userIds.Contains(u.Id), cancellationToken);

            var result = new List<LinkedUserToBoardResponse>();
            foreach (var link in boardToUserLinks)
            {
                var user = users.FirstOrDefault(u => u.Id == link.UserId);
                if (user != null)
                {
                    var tuple = Tuple.Create(link, user);
                    var mapped = _mapper.Map<LinkedUserToBoardResponse>(tuple);
                    result.Add(mapped);
                }
            }
            return result;
        }

        public async Task<LinkedUserToBoardResponse> GetBoardToUserLink(int boardId, int userId, CancellationToken cancellationToken)
        {
            var boardToUserLink = await _unitOfWork.BoardOnUserRepository.GetByExpressionAsync(b => b.BoardId == boardId && b.UserId == userId, cancellationToken)
                ?? throw new NotFoundException(ExceptionFormatter.NotFound(nameof(BoardOnUser), [boardId, userId]));

            var user = await _unitOfWork.UserRepository.GetByIdAsync(userId, cancellationToken)
                ?? throw new NotFoundException(ExceptionFormatter.NotFound(nameof(ApplicationUser), [userId]));

            var tuple = Tuple.Create(boardToUserLink, user);
            return _mapper.Map<LinkedUserToBoardResponse>(tuple);
        }

        public async Task<LinkedUserToBoardResponse> LinkUserToBoard(int boardId, int userId, LinkUserToBoardRequest linkUserToBoardRequest, CancellationToken cancellationToken)
        {
            //In the future check if user has invitation before allowing to link
            var responsibleUser = await _unitOfWork.BoardOnUserRepository.GetByExpressionAsync(bou => bou.UserId == userId && bou.BoardId == boardId && bou.UserRole == UserRoleEnum.OWNER, cancellationToken);
            if (responsibleUser != null) throw new BadRequestException(ExceptionFormatter.NotFound(nameof(responsibleUser), [userId]));

            var boardOnUser = _mapper.Map<BoardOnUser>(linkUserToBoardRequest);
            boardOnUser.BoardId = boardId;
            boardOnUser.UserId = userId;

            await _unitOfWork.BoardOnUserRepository.CreateAsync(boardOnUser, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var user = await _unitOfWork.UserRepository.GetByIdAsync(userId, cancellationToken)
                ?? throw new NotFoundException(ExceptionFormatter.NotFound(nameof(ApplicationUser), [userId]));

            var tuple = Tuple.Create(boardOnUser, user);
            return _mapper.Map<LinkedUserToBoardResponse>(tuple);
        }

        public async Task UnlinkUserFromBoard(int boardId, int userId, int responsibleUserId, CancellationToken cancellationToken)
        {
            var responsibleUser = await _unitOfWork.BoardOnUserRepository.GetByExpressionAsync(bou => bou.UserId == responsibleUserId && bou.BoardId == boardId);
            if (responsibleUser == null) throw new NotFoundException(ExceptionFormatter.NotFound(nameof(responsibleUser), [responsibleUserId]));
            if (!responsibleUser.UserRole.CanRemoveUsers() && userId != responsibleUserId) throw new BadRequestException(ExceptionFormatter.BadRequestRemoveUser);

            var boardOnUser = await _unitOfWork.BoardOnUserRepository.GetByExpressionAsync(b => b.BoardId == boardId && b.UserId == userId, cancellationToken)
                ?? throw new NotFoundException(ExceptionFormatter.NotFound(nameof(BoardOnUser), [boardId, userId]));

            if (boardOnUser.UserRole == UserRoleEnum.OWNER)
                throw new BadRequestException(ExceptionFormatter.BadRequestUnlinkOwner());

            await _notificationService.NotifyUserRemoved(boardId, userId, responsibleUserId, cancellationToken);
            _unitOfWork.BoardOnUserRepository.Delete(boardOnUser);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task<LinkedUserToBoardResponse> UpdateUserOnBoard(int boardId, int userId, LinkUserToBoardRequest linkUserToBoardRequest, CancellationToken cancellationToken)
        {
            var boardOnUserToUpdate = await _unitOfWork.BoardOnUserRepository.GetByExpressionAsync(b => b.BoardId == boardId && b.UserId == userId, cancellationToken)
                ?? throw new NotFoundException(ExceptionFormatter.NotFound(nameof(BoardOnUser), [boardId, userId]));

            _mapper.Map(linkUserToBoardRequest, boardOnUserToUpdate);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var user = await _unitOfWork.UserRepository.GetByIdAsync(userId, cancellationToken)
                ?? throw new NotFoundException(ExceptionFormatter.NotFound(nameof(ApplicationUser), [userId]));

            var tuple = Tuple.Create(boardOnUserToUpdate, user);
            return _mapper.Map<LinkedUserToBoardResponse>(tuple);
        }

        public async Task<LinkedUserToBoardResponse> PatchUserOnBoard(int boardId, int userId, JsonPatchDocument<LinkUserToBoardRequest> linkUserToBoardPatchDoc, CancellationToken cancellationToken)
        {
            var boardTaskToPatch = await _unitOfWork.BoardOnUserRepository.GetByExpressionAsync(b => b.BoardId == boardId && b.UserId == userId && b.UserRole != UserRoleEnum.OWNER, cancellationToken)
                ?? throw new BadRequestException(ExceptionFormatter.NotFound(nameof(BoardOnUser), [boardId, userId]));

            var boardTaskToPatchDto = _mapper.Map<LinkUserToBoardRequest>(boardTaskToPatch);
            linkUserToBoardPatchDoc.ApplyTo(boardTaskToPatchDto);

            _mapper.Map(boardTaskToPatchDto, boardTaskToPatch);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var user = await _unitOfWork.UserRepository.GetByIdAsync(userId, cancellationToken)
                ?? throw new NotFoundException(ExceptionFormatter.NotFound(nameof(ApplicationUser), [userId]));

            var tuple = Tuple.Create(boardTaskToPatch, user);
            return _mapper.Map<LinkedUserToBoardResponse>(tuple);
        }

        public async Task<IEnumerable<LinkedUserToBoardResponse>> GetUsersLinkedToBoardAsync(int boardId, CancellationToken cancellationToken)
        {
            var links = await _unitOfWork.BoardOnUserRepository.GetUsersLinkedToBoardAsync(boardId, cancellationToken);
            var linksDto = _mapper.Map<IEnumerable<LinkedUserToBoardResponse>>(links);

            return linksDto;
        }

        public async Task<List<UserResponse>> GetUsersByUserNameAsync(int boardId, string userName, CancellationToken cancellationToken)
        {
            _ = await _unitOfWork.BoardRepository.GetByIdAsync(boardId, cancellationToken)
                ?? throw new NotFoundException(ExceptionFormatter.NotFound(nameof(Board), [boardId]));
            
            var users = await _unitOfWork.BoardOnUserRepository.GetUsersByUserNameAsync(userName, cancellationToken);
            var usersNotInBoard = new List<ApplicationUser>();

            foreach (var user in users)
            {
                if (await _unitOfWork.BoardOnUserRepository.GetByExpressionAsync(b => b.BoardId == boardId && b.UserId == user.Id, cancellationToken) == null)
                {
                    usersNotInBoard.Add(user);
                }
            }
            return _mapper.Map<List<UserResponse>>(usersNotInBoard);
        }

        public async Task TransferOwnershipAsync(int boardId, int currentOwnerId, int newOwnerId, CancellationToken cancellationToken)
        {
            if (currentOwnerId == newOwnerId)
                throw new BadRequestException("You cannot transfer ownership to yourself.");

            var currentOwner = await _unitOfWork.BoardOnUserRepository.GetByExpressionAsync(
                b => b.BoardId == boardId && b.UserId == currentOwnerId, cancellationToken
            ) ?? throw new NotFoundException("Current owner not found.");

            if (currentOwner.UserRole != UserRoleEnum.OWNER)
                throw new BadRequestException("Only the owner can transfer ownership.");

            var newOwner = await _unitOfWork.BoardOnUserRepository.GetByExpressionAsync(
                b => b.BoardId == boardId && b.UserId == newOwnerId, cancellationToken
            ) ?? throw new NotFoundException("Target user is not linked to the board.");

            currentOwner.UserRole = UserRoleEnum.VIEWER;
            newOwner.UserRole = UserRoleEnum.OWNER;

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

    }
}
