using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace WorthBoards.IntegrationTests.Infrastructure;

// Strips ValueGeneratedOnAddOrUpdate from uint concurrency-token columns because
// Postgres xmin/rowversion behaviour is not supported by SQLite.
public class SqliteCompatibleModelCustomizer(ModelCustomizerDependencies dependencies)
    : ModelCustomizer(dependencies)
{
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties()
                .Where(p => p.ClrType == typeof(uint) && p.IsConcurrencyToken))
            {
                ((IMutableProperty)property).ValueGenerated = ValueGenerated.Never;
            }
        }
    }
}
