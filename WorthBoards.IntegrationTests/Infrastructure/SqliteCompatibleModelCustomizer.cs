using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace WorthBoards.IntegrationTests.Infrastructure;

public class SqliteCompatibleModelCustomizer(ModelCustomizerDependencies dependencies)
    : ModelCustomizer(dependencies)
{
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);

        // IsRowVersion() on uint uses Npgsql's xmin system column (absent in SQLite).
        // Strip ValueGeneratedOnAddOrUpdate so EF Core includes Version in INSERT/UPDATE SQL.
        // IsConcurrencyToken stays true, so optimistic-concurrency WHERE clauses still work.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties()
                         .Where(p => p.ClrType == typeof(uint)
                                  && p.ValueGenerated == ValueGenerated.OnAddOrUpdate))
            {
                property.ValueGenerated = ValueGenerated.Never;
            }
        }
    }
}
