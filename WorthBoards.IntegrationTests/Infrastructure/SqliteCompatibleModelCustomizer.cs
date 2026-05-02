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

        // SQLite has no xmin row-version concept; strip ValueGeneratedOnAddOrUpdate
        // from uint Version columns while keeping IsConcurrencyToken = true.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (IMutableProperty property in entityType.GetProperties()
                         .Where(p => p.ClrType == typeof(uint)
                                     && p.ValueGenerated == ValueGenerated.OnAddOrUpdate))
            {
                property.ValueGenerated = ValueGenerated.Never;
            }
        }
    }
}
