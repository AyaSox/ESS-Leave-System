using Microsoft.Extensions.Caching.Memory;

namespace ESSLeaveSystem.Services
{
    public interface ICacheService
    {
        Task<T?> GetAsync<T>(string key) where T : class;
        Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class;
        Task RemoveAsync(string key);
        Task RemoveByPatternAsync(string pattern);
        Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> getItem, TimeSpan? expiration = null) where T : class;
    }

    public class MemoryCacheService : ICacheService
    {
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<MemoryCacheService> _logger;
        private readonly MemoryCacheEntryOptions _defaultOptions;

        public MemoryCacheService(IMemoryCache memoryCache, ILogger<MemoryCacheService> logger)
        {
            _memoryCache = memoryCache;
            _logger = logger;
            _defaultOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30), // Default 30 minutes
                SlidingExpiration = TimeSpan.FromMinutes(5), // Extend by 5 minutes on access
                Priority = CacheItemPriority.Normal
            };
        }

        public Task<T?> GetAsync<T>(string key) where T : class
        {
            try
            {
                var cached = _memoryCache.Get<T>(key);
                if (cached != null)
                {
                    _logger.LogDebug("Cache hit for key: {CacheKey}", key);
                }
                else
                {
                    _logger.LogDebug("Cache miss for key: {CacheKey}", key);
                }
                return Task.FromResult(cached);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving from cache with key: {CacheKey}", key);
                return Task.FromResult<T?>(null);
            }
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
        {
            try
            {
                var options = _defaultOptions;
                if (expiration.HasValue)
                {
                    options = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = expiration.Value,
                        Priority = CacheItemPriority.Normal
                    };
                }

                _memoryCache.Set(key, value, options);
                _logger.LogDebug("Cached item with key: {CacheKey}, expiration: {Expiration}", 
                    key, expiration?.ToString() ?? "default");
                
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting cache with key: {CacheKey}", key);
                return Task.CompletedTask;
            }
        }

        public Task RemoveAsync(string key)
        {
            try
            {
                _memoryCache.Remove(key);
                _logger.LogDebug("Removed cache item with key: {CacheKey}", key);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache with key: {CacheKey}", key);
                return Task.CompletedTask;
            }
        }

        public Task RemoveByPatternAsync(string pattern)
        {
            try
            {
                // MemoryCache doesn't support pattern removal out of the box
                // This is a limitation - for pattern removal, consider using Redis
                _logger.LogWarning("Pattern-based cache removal not supported in MemoryCache. Pattern: {Pattern}", pattern);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache by pattern: {Pattern}", pattern);
                return Task.CompletedTask;
            }
        }

        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> getItem, TimeSpan? expiration = null) where T : class
        {
            try
            {
                var cached = await GetAsync<T>(key);
                if (cached != null)
                {
                    return cached;
                }

                _logger.LogDebug("Cache miss for key: {CacheKey}, fetching from source", key);
                var item = await getItem();
                
                if (item != null)
                {
                    await SetAsync(key, item, expiration);
                }

                return item;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetOrSet for key: {CacheKey}", key);
                // Fallback to getting fresh data
                return await getItem();
            }
        }
    }

    // Cache key constants
    public static class CacheKeys
    {
        public const string LEAVE_TYPES = "leave_types";
        public const string ACTIVE_LEAVE_TYPES = "active_leave_types";
        public const string EMPLOYEE_BALANCES = "employee_balances_{0}_{1}"; // employeeId, year
        public const string EMPLOYEE_APPLICATIONS = "employee_applications_{0}"; // employeeId
        public const string PENDING_APPROVALS = "pending_approvals_{0}"; // managerId
        
        public static string EmployeeBalances(int employeeId, int year) => 
            string.Format(EMPLOYEE_BALANCES, employeeId, year);
        
        public static string EmployeeApplications(int employeeId) => 
            string.Format(EMPLOYEE_APPLICATIONS, employeeId);
        
        public static string PendingApprovals(int managerId) => 
            string.Format(PENDING_APPROVALS, managerId);
    }
}