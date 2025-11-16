using ESSLeaveSystem.Services;
using HRManagement.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ESSLeaveSystem.Pages.Notifications
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly INotificationService _notifications;
        private readonly IEmployeeLookupService _employeeLookup;

        public IndexModel(INotificationService notifications, IEmployeeLookupService employeeLookup)
        {
            _notifications = notifications;
            _employeeLookup = employeeLookup;
        }

        public List<Notification> Items { get; set; } = new();
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
        public int UnreadCount { get; set; }

        public async Task<IActionResult> OnGetAsync(int pageNumber = 1)
        {
            if (User.Identity?.Name == null) return RedirectToPage("/Account/Login");
            var employeeId = await _employeeLookup.GetEmployeeIdByEmailAsync(User.Identity.Name);
            if (!employeeId.HasValue) return RedirectToPage("/Account/Login");

            PageNumber = Math.Max(1, pageNumber);

            // Fetch page of notifications
            var all = await _notifications.GetAllNotificationsAsync(employeeId.Value, int.MaxValue);
            TotalCount = all.Count;
            UnreadCount = all.Count(n => !n.IsRead);
            Items = all
                .OrderByDescending(n => n.CreatedDate)
                .Skip((PageNumber - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            return Page();
        }

        public async Task<IActionResult> OnPostMarkAsReadAsync(int id)
        {
            await _notifications.MarkAsReadAsync(id);
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostMarkAllAsReadAsync()
        {
            if (User.Identity?.Name == null) return RedirectToPage("/Account/Login");
            var employeeId = await _employeeLookup.GetEmployeeIdByEmailAsync(User.Identity.Name);
            if (employeeId.HasValue)
            {
                await _notifications.MarkAllAsReadAsync(employeeId.Value);
            }
            return RedirectToPage();
        }
    }
}
