using Microsoft.EntityFrameworkCore;
using WorthBoards.Domain.Entities;

namespace WorthBoards.Data.Database;

static class PropertyConfigurationManager {
    public static void AddConcurrencyTokens(ModelBuilder builder) {
        builder.Entity<Board>()
            .Property(b => b.Version)
            .ValueGeneratedNever()
            .IsConcurrencyToken();

        builder.Entity<BoardOnUser>()
            .Property(bou => bou.Version)
            .ValueGeneratedNever()
            .IsConcurrencyToken();

        builder.Entity<BoardTask>()
            .Property(t => t.Version)
            .ValueGeneratedNever()
            .IsConcurrencyToken();

        builder.Entity<Comment>()
            .Property(c => c.Version)
            .ValueGeneratedNever()
            .IsConcurrencyToken();
    }
}
