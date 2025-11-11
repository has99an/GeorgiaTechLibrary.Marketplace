using BookService.Data;
using BookService.Repositories;
using BookService.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>();

// Add Entity Framework
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add repositories
builder.Services.AddScoped<IBookRepository, BookRepository>();

// Add message producer
builder.Services.AddSingleton<IMessageProducer, RabbitMQProducer>();

// Add AutoMapper
builder.Services.AddAutoMapper(typeof(Program));

// Add logging
builder.Services.AddLogging();

await using var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

// Wait for SQL Server to be ready and seed data
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var retries = 10;

    while (retries > 0)
    {
        try
        {
            Console.WriteLine("Attempting database migration...");
            await dbContext.Database.MigrateAsync();
            Console.WriteLine("Migration successful. Starting seed data...");
            await SeedData.Initialize(dbContext);
            Console.WriteLine("Seed data completed successfully");
            break;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Database not ready: {ex.Message}. Retries left: {retries}");
            retries--;
            await Task.Delay(5000);
        }
    }
}

await app.RunAsync();
