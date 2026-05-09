using Microsoft.EntityFrameworkCore;
using WorthBoards.Data.Database;
using WorthBoards.Domain.Entities;

namespace WorthBoards.IntegrationTests.Infrastructure;

internal sealed class IntegrationTestDbContext(DbContextOptions<IntegrationTestDbContext> options)
    : ApplicationDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Board>()
            .Property(x => x.Version)
            .ValueGeneratedNever();

        builder.Entity<BoardOnUser>()
            .Property(x => x.Version)
            .ValueGeneratedNever();

        builder.Entity<BoardTask>()
            .Property(x => x.Version)
            .ValueGeneratedNever();

        builder.Entity<Comment>()
            .Property(x => x.Version)
            .ValueGeneratedNever();
    }
}
