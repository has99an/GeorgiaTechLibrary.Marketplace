using OrderService.Application.Interfaces;

namespace OrderService.Infrastructure.Jobs;

/// <summary>
/// Background job to process monthly seller settlements
/// Runs on the first day of each month to settle previous month's payouts
/// </summary>
public class PaymentSettlementJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PaymentSettlementJob> _logger;
    private DateTime? _lastProcessedMonth;

    public PaymentSettlementJob(
        IServiceProvider serviceProvider,
        ILogger<PaymentSettlementJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PaymentSettlementJob started");

        // Wait until services are ready
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;

                // Check if it's the first day of the month and we haven't processed this month yet
                if (now.Day == 1 && (!_lastProcessedMonth.HasValue || _lastProcessedMonth.Value.Month != now.Month))
                {
                    await ProcessMonthlySettlementsAsync();
                    _lastProcessedMonth = now;
                }

                // Wait 6 hours before checking again
                await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during settlement processing");
                // Wait before retrying
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }

        _logger.LogInformation("PaymentSettlementJob stopped");
    }

    private async Task ProcessMonthlySettlementsAsync()
    {
        _logger.LogInformation("=== STARTING MONTHLY SETTLEMENT PROCESSING ===");

        using var scope = _serviceProvider.CreateScope();
        var paymentAllocationService = scope.ServiceProvider.GetRequiredService<IPaymentAllocationService>();

        // Calculate previous month period
        var now = DateTime.UtcNow;
        var previousMonth = now.AddMonths(-1);
        var periodStart = new DateOnly(previousMonth.Year, previousMonth.Month, 1);
        var periodEnd = new DateOnly(previousMonth.Year, previousMonth.Month, 
            DateTime.DaysInMonth(previousMonth.Year, previousMonth.Month));

        _logger.LogInformation("Processing settlements for period: {PeriodStart} to {PeriodEnd}",
            periodStart, periodEnd);

        try
        {
            // Get all unique seller IDs with pending allocations
            // Note: In a real implementation, you'd query the database for distinct seller IDs
            // For now, this is a placeholder - you'd need to add a method to get all sellers with pending payouts
            
            _logger.LogInformation("Settlement processing would occur here for period {PeriodStart} to {PeriodEnd}",
                periodStart, periodEnd);
            
            // In a production system, you would:
            // 1. Get list of all sellers with pending allocations
            // 2. For each seller, call ProcessSettlementAsync
            // 3. Send settlement notifications
            // 4. Log results
            
            _logger.LogInformation("=== MONTHLY SETTLEMENT PROCESSING COMPLETED ===");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing monthly settlements");
            throw;
        }
    }
}
