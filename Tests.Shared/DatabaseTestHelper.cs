using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Tests.Shared;

/// <summary>
/// Helper class for database testing operations
/// </summary>
public static class DatabaseTestHelper
{
    /// <summary>
    /// Creates an in-memory database context for testing
    /// </summary>
    public static TContext CreateInMemoryContext<TContext>(Action<DbContextOptionsBuilder>? configure = null)
        where TContext : DbContext
    {
        var optionsBuilder = new DbContextOptionsBuilder<TContext>();
        optionsBuilder.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString());

        configure?.Invoke(optionsBuilder);

        return (TContext)Activator.CreateInstance(typeof(TContext), optionsBuilder.Options)!;
    }

    /// <summary>
    /// Seeds test data into a database context
    /// </summary>
    public static async Task SeedDatabaseAsync<TContext>(TContext context, Action<TContext> seedAction)
        where TContext : DbContext
    {
        seedAction(context);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Clears all data from a database context
    /// </summary>
    public static async Task ClearDatabaseAsync<TContext>(TContext context)
        where TContext : DbContext
    {
        var entityTypes = context.Model.GetEntityTypes();
        foreach (var entityType in entityTypes)
        {
            var method = typeof(DatabaseTestHelper).GetMethod(nameof(ClearSet), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var genericMethod = method!.MakeGenericMethod(entityType.ClrType);
            genericMethod.Invoke(null, new object[] { context });
        }
        await context.SaveChangesAsync();
    }

    private static void ClearSet<T>(DbContext context) where T : class
    {
        var set = context.Set<T>();
        set.RemoveRange(set);
    }
}





