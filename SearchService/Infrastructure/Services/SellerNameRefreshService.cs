using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SearchService.Application.Common.Models;
using StackExchange.Redis;
using System.Text.Json;

namespace SearchService.Infrastructure.Services;

/// <summary>
/// Service that refreshes seller names for existing seller data in Redis
/// Runs on startup to update seller names that were added before sellerName was included in events
/// </summary>
public class SellerNameRefreshService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SellerNameRefreshService> _logger;
    private readonly IConnectionMultiplexer _redis;
    private readonly IConfiguration _configuration;

    public SellerNameRefreshService(
        IServiceProvider serviceProvider,
        ILogger<SellerNameRefreshService> logger,
        IConnectionMultiplexer redis,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _redis = redis;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for services to be ready
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        try
        {
            _logger.LogInformation("SellerNameRefreshService: Starting seller name refresh...");

            var database = _redis.GetDatabase();
            var server = _redis.GetServer(_redis.GetEndPoints().First());

            // Get all sellers:{ISBN} keys
            var sellersKeys = new List<string>();
            await foreach (var key in server.KeysAsync(pattern: "sellers:*", pageSize: 1000))
            {
                if (stoppingToken.IsCancellationRequested)
                    break;

                sellersKeys.Add(key.ToString());
            }

            _logger.LogInformation("SellerNameRefreshService: Found {Count} seller keys to process", sellersKeys.Count);

            if (sellersKeys.Count == 0)
            {
                _logger.LogInformation("SellerNameRefreshService: No seller keys found. Nothing to refresh.");
                return;
            }

            // Collect all unique seller IDs from all seller keys
            var sellerIdsToRefresh = new HashSet<string>();
            var sellersByKey = new Dictionary<string, List<SellerInfoDto>>();

            foreach (var key in sellersKeys)
            {
                if (stoppingToken.IsCancellationRequested)
                    break;

                try
                {
                    var sellersJson = await database.StringGetAsync(key);
                    if (!sellersJson.IsNullOrEmpty)
                    {
                        var sellers = JsonSerializer.Deserialize<List<SellerInfoDto>>(sellersJson!) 
                            ?? new List<SellerInfoDto>();

                        // Check if any seller is missing sellerName
                        var needsRefresh = sellers.Any(s => string.IsNullOrEmpty(s.SellerName) && !string.IsNullOrEmpty(s.SellerId));
                        
                        if (needsRefresh)
                        {
                            sellersByKey[key] = sellers;
                            foreach (var seller in sellers)
                            {
                                if (!string.IsNullOrEmpty(seller.SellerId) && string.IsNullOrEmpty(seller.SellerName))
                                {
                                    sellerIdsToRefresh.Add(seller.SellerId);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "SellerNameRefreshService: Error reading sellers from key {Key}", key);
                }
            }

            _logger.LogInformation("SellerNameRefreshService: Found {Count} unique seller IDs that need name refresh", sellerIdsToRefresh.Count);

            if (sellerIdsToRefresh.Count == 0)
            {
                _logger.LogInformation("SellerNameRefreshService: All sellers already have names. No refresh needed.");
                return;
            }

            // Get seller names from UserService
            var sellerNames = await GetSellerNamesFromUserServiceAsync(sellerIdsToRefresh, stoppingToken);

            if (sellerNames.Count == 0)
            {
                _logger.LogWarning("SellerNameRefreshService: No seller names retrieved from UserService");
                return;
            }

            _logger.LogInformation("SellerNameRefreshService: Retrieved {Count} seller names from UserService", sellerNames.Count);

            // Update Redis cache with seller names
            var updatedCount = 0;
            foreach (var kvp in sellersByKey)
            {
                if (stoppingToken.IsCancellationRequested)
                    break;

                try
                {
                    var key = kvp.Key;
                    var sellers = kvp.Value;
                    var updated = false;

                    foreach (var seller in sellers)
                    {
                        if (string.IsNullOrEmpty(seller.SellerName) && sellerNames.TryGetValue(seller.SellerId, out var sellerName))
                        {
                            seller.SellerName = sellerName;
                            updated = true;
                        }
                    }

                    if (updated)
                    {
                        var updatedJson = JsonSerializer.Serialize(sellers);
                        await database.StringSetAsync(key, updatedJson);
                        updatedCount++;
                        _logger.LogDebug("SellerNameRefreshService: Updated sellers in key {Key}", key);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "SellerNameRefreshService: Error updating sellers in key {Key}", kvp.Key);
                }
            }

            _logger.LogInformation("SellerNameRefreshService: Completed refresh. Updated {UpdatedCount} seller keys with seller names", updatedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SellerNameRefreshService: Error during seller name refresh");
        }
    }

    private async Task<Dictionary<string, string>> GetSellerNamesFromUserServiceAsync(
        HashSet<string> sellerIds, 
        CancellationToken cancellationToken)
    {
        var sellerNames = new Dictionary<string, string>();

        try
        {
            var httpClientFactory = _serviceProvider.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromMinutes(5);

            var userServiceUrl = _configuration["UserService:BaseUrl"] 
                ?? _configuration["Services:UserService"] 
                ?? "http://userservice:5005";

            _logger.LogInformation("SellerNameRefreshService: Fetching seller names from UserService at {Url}", userServiceUrl);

            // Fetch seller names in parallel (with limit to avoid overwhelming the service)
            var semaphore = new SemaphoreSlim(10, 10); // Max 10 concurrent requests
            var tasks = sellerIds.Select(async sellerId =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    if (!Guid.TryParse(sellerId, out var sellerGuid))
                    {
                        _logger.LogWarning("SellerNameRefreshService: Invalid seller ID format: {SellerId}", sellerId);
                        return;
                    }

                    var profileUrl = $"{userServiceUrl}/api/sellers/{sellerGuid}/profile";
                    var response = await httpClient.GetAsync(profileUrl, cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync(cancellationToken);
                        var profile = JsonSerializer.Deserialize<SellerProfileResponse>(json, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        if (profile != null && !string.IsNullOrEmpty(profile.Name))
                        {
                            lock (sellerNames)
                            {
                                sellerNames[sellerId] = profile.Name;
                            }
                            _logger.LogDebug("SellerNameRefreshService: Retrieved name for seller {SellerId}: {Name}", sellerId, profile.Name);
                        }
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        _logger.LogDebug("SellerNameRefreshService: Seller {SellerId} not found in UserService", sellerId);
                    }
                    else
                    {
                        _logger.LogWarning("SellerNameRefreshService: Failed to get seller profile for {SellerId}: {StatusCode}", 
                            sellerId, response.StatusCode);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "SellerNameRefreshService: Error fetching seller name for {SellerId}", sellerId);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            _logger.LogInformation("SellerNameRefreshService: Successfully retrieved {Count} seller names out of {Total} requested", 
                sellerNames.Count, sellerIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SellerNameRefreshService: Error fetching seller names from UserService");
        }

        return sellerNames;
    }

    private class SellerProfileResponse
    {
        public string Name { get; set; } = string.Empty;
    }
}

