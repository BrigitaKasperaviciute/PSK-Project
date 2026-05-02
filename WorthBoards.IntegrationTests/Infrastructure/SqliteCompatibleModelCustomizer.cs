using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace WorthBoards.IntegrationTests.Infrastructure;

/// <summary>
/// Removes ValueGeneratedOnAddOrUpdate from uint version columns so SQLite
/// can store and compare them manually (PostgreSQL maps these to xmin).
/// </summary>
public class SqliteCompatibleModelCustomizer : RelationalModelCustomizer
{
    public SqliteCompatibleModelCustomizer(ModelCustomizerDependencies dependencies)
        : base(dependencies) { }

    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
            .Where(e => e.ClrType != null))
        {
            foreach (var property in entityType.GetProperties()
                .Where(p => p.ClrType == typeof(uint) && p.IsConcurrencyToken))
            {
                modelBuilder
                    .Entity(entityType.ClrType)
                    .Property(property.Name)
                    .ValueGeneratedNever();
            }
        }
    }
}
