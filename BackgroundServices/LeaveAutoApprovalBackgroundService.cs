using ESSLeaveSystem.Services;

namespace ESSLeaveSystem.BackgroundServices
{
    public class LeaveAutoApprovalBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<LeaveAutoApprovalBackgroundService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1); // Check every hour

        public LeaveAutoApprovalBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<LeaveAutoApprovalBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Leave Auto-Approval Background Service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessLeaveApprovals();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Leave Auto-Approval Background Service");
                }

                // Wait before next check
                await Task.Delay(_checkInterval, stoppingToken);
            }

            _logger.LogInformation("Leave Auto-Approval Background Service stopped");
        }

        private async Task ProcessLeaveApprovals()
        {
            using var scope = _serviceProvider.CreateScope();
            
            var approvalService = scope.ServiceProvider.GetRequiredService<ILeaveApprovalService>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

            try
            {
                // Step 1: Send urgent reminders (4 days pending - 1 day before auto-approval)
                var urgentLeave = await approvalService.GetLeaveRequiringUrgentApprovalAsync();
                
                foreach (var application in urgentLeave)
                {
                    await notificationService.SendUrgentApprovalReminderAsync(application.LeaveApplicationId);
                }

                if (urgentLeave.Any())
                {
                    _logger.LogInformation($"Sent {urgentLeave.Count} urgent approval reminder(s)");
                }

                // Step 2: Auto-approve leave pending for 5+ days
                var autoApprovedCount = await approvalService.AutoApprovePendingLeaveAsync();
                
                if (autoApprovedCount > 0)
                {
                    _logger.LogInformation($"Auto-approved {autoApprovedCount} leave application(s)");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing leave approvals");
            }
        }
    }
}