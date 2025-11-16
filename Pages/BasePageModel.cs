using Microsoft.AspNetCore.Mvc.RazorPages;
using ESSLeaveSystem.Services;
using HRManagement.Shared.Models;

namespace ESSLeaveSystem.Pages
{
    public class BasePageModel : PageModel
    {
        protected readonly IEmployeeLookupService _employeeLookup;
        protected readonly INotificationService _notificationService;

        public BasePageModel(
            IEmployeeLookupService employeeLookup,
            INotificationService notificationService)
        {
            _employeeLookup = employeeLookup;
            _notificationService = notificationService;
        }

        protected async Task LoadNotificationsAsync()
        {
            if (User.Identity?.Name != null)
            {
                var employeeId = await _employeeLookup.GetEmployeeIdByEmailAsync(User.Identity.Name);
                if (employeeId.HasValue)
                {
                    // Only load the 5 most recent notifications for the top bar dropdown
                    var notifications = await _notificationService.GetAllNotificationsAsync(employeeId.Value, 5);
                    ViewData["Notifications"] = notifications;
                }
            }
        }
    }
}