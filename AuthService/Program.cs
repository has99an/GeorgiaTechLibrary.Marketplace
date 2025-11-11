using AuthService.Data;
using AuthService.Repositories;
using AuthService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using System.Text;

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
builder.Services.TryAddScoped<IAuthUserRepository, AuthUserRepository>();

// Add message producer and consumer
builder.Services.TryAddSingleton<IMessageProducer, RabbitMQProducer>();
builder.Services.TryAddSingleton<IMessageConsumer, RabbitMQConsumer>();
builder.Services.AddHostedService<RabbitMQConsumer>();

// Add AutoMapper
builder.Services.AddAutoMapper(typeof(Program));

// Add JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(builder.Configuration["Jwt:Key"] ?? "defaultkey")),
            ValidateIssuer = false,
            ValidateAudience = false
        };
    });

builder.Services.AddAuthorization();

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
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

// Try to migrate database and seed data in background
_ = Task.Run(async () =>
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var retries = 30; // More retries for Docker startup

        while (retries > 0)
        {
            try
            {
                logger.LogInformation("Attempting database migration...");
                await dbContext.Database.MigrateAsync();
                logger.LogInformation("Migration successful. Starting seed data...");
                await SeedData.Initialize(dbContext);
                logger.LogInformation("Seed data completed successfully");
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Database not ready: {Message}. Retries left: {Retries}", ex.Message, retries);
                retries--;
                await Task.Delay(5000);
            }
        }

        if (retries == 0)
        {
            logger.LogError("Failed to initialize database after all retries");
        }
    }
});

await app.RunAsync();
