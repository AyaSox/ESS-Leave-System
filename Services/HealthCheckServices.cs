using Microsoft.Extensions.Diagnostics.HealthChecks;
using ESSLeaveSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ESSLeaveSystem.Services
{
    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly LeaveDbContext _context;
        private readonly ILogger<DatabaseHealthCheck> _logger;

        public DatabaseHealthCheck(LeaveDbContext context, ILogger<DatabaseHealthCheck> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Try to execute a simple query
                var canConnect = await _context.Database.CanConnectAsync(cancellationToken);
                
                if (!canConnect)
                {
                    return HealthCheckResult.Unhealthy("Cannot connect to database");
                }

                // Check if we can query leave types (basic functionality test)
                var leaveTypesCount = await _context.LeaveTypes.CountAsync(cancellationToken);
                
                var data = new Dictionary<string, object>
                {
                    ["leave_types_count"] = leaveTypesCount,
                    ["database_provider"] = _context.Database.ProviderName ?? "Unknown",
                    ["connection_string"] = _context.Database.GetConnectionString() ?? "Not available"
                };

                return HealthCheckResult.Healthy("Database is accessible", data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database health check failed");
                return HealthCheckResult.Unhealthy("Database health check failed", ex);
            }
        }
    }

    public class FileSystemHealthCheck : IHealthCheck
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<FileSystemHealthCheck> _logger;

        public FileSystemHealthCheck(IWebHostEnvironment environment, ILogger<FileSystemHealthCheck> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Check if we can write to the documents directory
                var documentsPath = Path.Combine(_environment.WebRootPath ?? _environment.ContentRootPath, "documents");
                Directory.CreateDirectory(documentsPath);

                var testFile = Path.Combine(documentsPath, "health-check.tmp");
                
                // Test write
                await File.WriteAllTextAsync(testFile, "health-check", cancellationToken);
                
                // Test read
                var content = await File.ReadAllTextAsync(testFile, cancellationToken);
                
                // Cleanup
                File.Delete(testFile);

                if (content != "health-check")
                {
                    return HealthCheckResult.Unhealthy("File system read/write test failed");
                }

                var data = new Dictionary<string, object>
                {
                    ["documents_path"] = documentsPath,
                    ["environment"] = _environment.EnvironmentName,
                    ["web_root_path"] = _environment.WebRootPath ?? "Not set"
                };

                return HealthCheckResult.Healthy("File system is accessible", data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "File system health check failed");
                return HealthCheckResult.Unhealthy("File system health check failed", ex);
            }
        }
    }

    public class ExternalApiHealthCheck : IHealthCheck
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ExternalApiHealthCheck> _logger;

        public ExternalApiHealthCheck(
            IHttpClientFactory httpClientFactory, 
            IConfiguration configuration,
            ILogger<ExternalApiHealthCheck> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                var hrSystemUrl = _configuration.GetValue<string>("HRSystem:BaseUrl") ?? "http://localhost:5000";
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(10);

                // Try to reach the HR system
                var response = await httpClient.GetAsync($"{hrSystemUrl}/health", cancellationToken);
                
                var data = new Dictionary<string, object>
                {
                    ["hr_system_url"] = hrSystemUrl,
                    ["response_status"] = response.StatusCode.ToString(),
                    ["response_time"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC")
                };

                if (response.IsSuccessStatusCode)
                {
                    return HealthCheckResult.Healthy("HR System is accessible", data);
                }
                else
                {
                    return HealthCheckResult.Degraded($"HR System returned {response.StatusCode}", null, data);
                }
            }
            catch (TaskCanceledException)
            {
                return HealthCheckResult.Unhealthy("HR System health check timed out");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "External API health check failed");
                return HealthCheckResult.Unhealthy("HR System is not accessible", ex);
            }
        }
    }
}