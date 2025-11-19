using Microsoft.EntityFrameworkCore;
using NotificationService.Domain.Entities;
using NotificationService.Domain.ValueObjects;
using System.Text.Json;

namespace NotificationService.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Notification> Notifications { get; set; } = null!;
    public DbSet<NotificationPreference> NotificationPreferences { get; set; } = null!;
    public DbSet<EmailTemplate> EmailTemplates { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Notification entity
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("Notifications");
            entity.HasKey(n => n.NotificationId);

            entity.Property(n => n.RecipientId)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(n => n.RecipientEmail)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(n => n.Type)
                .IsRequired()
                .HasConversion(
                    v => v.ToString(),
                    v => Enum.Parse<NotificationType>(v));

            entity.Property(n => n.Subject)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(n => n.Message)
                .IsRequired()
                .HasMaxLength(5000);

            entity.Property(n => n.Status)
                .IsRequired()
                .HasConversion(
                    v => v.ToString(),
                    v => Enum.Parse<NotificationStatus>(v));

            entity.Property(n => n.CreatedDate)
                .IsRequired();

            entity.Property(n => n.ErrorMessage)
                .HasMaxLength(1000);

            entity.Property(n => n.RetryCount)
                .IsRequired();

            // Store metadata as JSON
            entity.Property(n => n.Metadata)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, string>())
                .HasColumnType("nvarchar(max)");

            entity.HasIndex(n => n.RecipientId);
            entity.HasIndex(n => n.Status);
            entity.HasIndex(n => n.CreatedDate);
        });

        // Configure NotificationPreference entity
        modelBuilder.Entity<NotificationPreference>(entity =>
        {
            entity.ToTable("NotificationPreferences");
            entity.HasKey(np => np.PreferenceId);

            entity.Property(np => np.UserId)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(np => np.EmailEnabled)
                .IsRequired();

            entity.Property(np => np.CreatedDate)
                .IsRequired();

            entity.HasIndex(np => np.UserId)
                .IsUnique();
        });

        // Configure EmailTemplate entity
        modelBuilder.Entity<EmailTemplate>(entity =>
        {
            entity.ToTable("EmailTemplates");
            entity.HasKey(et => et.TemplateId);

            entity.Property(et => et.TemplateName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(et => et.Type)
                .IsRequired()
                .HasConversion(
                    v => v.ToString(),
                    v => Enum.Parse<NotificationType>(v));

            entity.Property(et => et.Subject)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(et => et.HtmlBody)
                .IsRequired()
                .HasColumnType("nvarchar(max)");

            entity.Property(et => et.TextBody)
                .IsRequired()
                .HasColumnType("nvarchar(max)");

            entity.Property(et => et.IsActive)
                .IsRequired();

            entity.Property(et => et.CreatedDate)
                .IsRequired();

            entity.HasIndex(et => et.TemplateName)
                .IsUnique();

            entity.HasIndex(et => et.Type);
        });
    }
}

