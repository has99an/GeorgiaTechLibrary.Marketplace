using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Tests.Shared;

/// <summary>
/// Base factory for creating test web applications with mocked dependencies
/// </summary>
public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram>
    where TProgram : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the real database context if it exists
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Add in-memory database for testing
            services.AddDbContext<DbContext>(options =>
            {
                options.UseInMemoryDatabase("TestDb");
            });

            // Build service provider to ensure services are registered
            var sp = services.BuildServiceProvider();

            // Create a scope to obtain a reference to the database context
            using (var scope = sp.CreateScope())
            {
                var scopedServices = scope.ServiceProvider;
                var db = scopedServices.GetRequiredService<DbContext>();
                var logger = scopedServices.GetRequiredService<ILogger<CustomWebApplicationFactory<TProgram>>>();

                // Ensure the database is created
                db.Database.EnsureCreated();
            }
        });

        builder.UseEnvironment("Testing");
    }
}




