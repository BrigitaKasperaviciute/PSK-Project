using Microsoft.EntityFrameworkCore;
using WorthBoards.Data.Database;
using WorthBoards.Domain.Entities;

namespace WorthBoards.IntegrationTests.Infrastructure;

public sealed class TestApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Board>()
            .Property(board => board.Version)
            .ValueGeneratedNever();

        builder.Entity<BoardOnUser>()
            .Property(boardOnUser => boardOnUser.Version)
            .ValueGeneratedNever();

        builder.Entity<BoardTask>()
            .Property(boardTask => boardTask.Version)
            .ValueGeneratedNever();

        builder.Entity<Comment>()
            .Property(comment => comment.Version)
            .ValueGeneratedNever();
    }
}