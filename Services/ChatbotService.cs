using System.Security.Claims;
using System.Text.RegularExpressions;
using ESSLeaveSystem.Data;
using HRManagement.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace ESSLeaveSystem.Services
{
    public class ChatbotService : IChatbotService
    {
        private readonly LeaveDbContext _context;
        public ChatbotService(LeaveDbContext context)
        {
            _context = context;
        }

        public async Task<ChatbotAnswer> GetAnswerAsync(string message, ClaimsPrincipal user)
        {
            message = (message ?? string.Empty).Trim();
            var lower = message.ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(lower) || lower is "help" or "hi" or "hello" or "?" )
            {
                return Intro(user);
            }

            if (ContainsAny(lower, "apply", "request") && lower.Contains("leave"))
            {
                return new ChatbotAnswer(
                    "To apply for leave: go to Apply Leave, select a type, choose start/end dates (weekends/holidays are excluded), add a reason, and submit. Your manager will be notified.",
                    new []
                    {
                        new ChatAction("Open Apply Leave", "/Leave/Apply", "fas fa-plus-circle"),
                        new ChatAction("View My Applications", "/Leave/MyApplications", "fas fa-list")
                    });
            }

            if (lower.Contains("balance") || lower.Contains("days left") || lower.Contains("entitlement"))
            {
                return new ChatbotAnswer(
                    "View your current leave balances and pending days in Leave Balance. Balances are tracked per year and update when applications are approved or pending.",
                    new [] { new ChatAction("Open Leave Balance", "/Leave/Balance", "fas fa-chart-bar") });
            }

            if (lower.Contains("pending approvals for my team") || lower.Contains("team pending") || lower.Contains("my team approvals"))
            {
                // Manager-only: count team's pending items
                var email = user.Identity?.Name ?? string.Empty;
                var employeeId = await _context.Employees
                    .Where(e => e.Email == email && !e.IsDeleted)
                    .Select(e => e.EmployeeId)
                    .FirstOrDefaultAsync();

                int count = 0;
                if (employeeId != 0)
                {
                    // direct reports have ManagerId == current employeeId
                    var teamIds = await _context.Employees
                        .Where(e => e.LineManagerId == employeeId && !e.IsDeleted)
                        .Select(e => e.EmployeeId)
                        .ToListAsync();
                    if (teamIds.Count > 0)
                    {
                        count = await _context.LeaveApplications
                            .CountAsync(l => teamIds.Contains(l.EmployeeId) && l.Status == LeaveStatus.Pending);
                    }
                }

                var ans = count == 0
                    ? "You currently have no pending approvals for your team."
                    : $"You have {count} pending approval(s) for your team.";

                return new ChatbotAnswer(ans, new []{ new ChatAction("Pending Approvals", "/Manager/PendingApprovals", "fas fa-tasks") });
            }

            if (lower.Contains("top leave type this year") || (lower.Contains("top") && lower.Contains("leave type") && lower.Contains("year")))
            {
                var year = DateTime.Now.Year;
                var top = await _context.LeaveApplications
                    .Where(l => l.AppliedDate.Year == year)
                    .GroupBy(l => l.LeaveTypeId)
                    .Select(g => new { LeaveTypeId = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .FirstOrDefaultAsync();

                if (top == null)
                {
                    return new ChatbotAnswer("No leave applications found for this year.", Array.Empty<ChatAction>());
                }
                var lt = await _context.LeaveTypes.FindAsync(top.LeaveTypeId);
                var name = lt?.Name ?? "(unknown)";
                return new ChatbotAnswer($"Top leave type this year: {name} ({top.Count} applications).", new [] { new ChatAction("Leave Statistics", "/Admin/LeaveStatistics", "fas fa-chart-line") });
            }

            if (lower.Contains("approve") || lower.Contains("manager"))
            {
                return new ChatbotAnswer(
                    "Managers can review, approve, or reject pending applications in the Manager area. Employees see the approver on the Apply page.",
                    new []
                    {
                        new ChatAction("Pending Approvals", "/Manager/PendingApprovals", "fas fa-tasks"),
                        new ChatAction("Team Leave Calendar", "/Manager/TeamLeave", "fas fa-users")
                    });
            }

            if (lower.Contains("policy") || lower.Contains("bcea") || lower.Contains("rules"))
            {
                return new ChatbotAnswer(
                    "This system follows South African BCEA guidelines: annual leave requires approval, sick leave may require a medical certificate, public holidays/weekends are excluded, and eligibility depends on employment length.",
                    Array.Empty<ChatAction>());
            }

            if (lower.Contains("past") || lower.Contains("backdate"))
            {
                return new ChatbotAnswer(
                    "You can submit retrospective leave requests. The system still checks overlaps, balances, and BCEA eligibility based on the selected start date.",
                    new [] { new ChatAction("Apply Leave", "/Leave/Apply", "fas fa-calendar-plus") });
            }

            if (Regex.IsMatch(lower, @"^(how|where|what) .*notifications?"))
            {
                return new ChatbotAnswer(
                    "Notifications appear in the top bar. You will receive updates for submissions, approvals, rejections, and reminders.",
                    new [] { new ChatAction("View Notifications", "/Notifications", "fas fa-bell") });
            }

            if (lower.Contains("stats") || lower.Contains("statistics") || lower.Contains("admin") || lower.Contains("report"))
            {
                // Summarize some live stats for admins and managers
                var totalPending = await _context.LeaveApplications.CountAsync(l => l.Status == LeaveStatus.Pending);
                var totalApprovedThisMonth = await _context.LeaveApplications
                    .CountAsync(l => l.Status == LeaveStatus.Approved && l.AppliedDate.Month == DateTime.Now.Month && l.AppliedDate.Year == DateTime.Now.Year);
                var topLeaveType = await _context.LeaveApplications
                    .GroupBy(l => l.LeaveTypeId)
                    .Select(g => new { LeaveTypeId = g.Key, Cnt = g.Count() })
                    .OrderByDescending(x => x.Cnt)
                    .FirstOrDefaultAsync();

                string topTypeName = "(none)";
                if (topLeaveType != null)
                {
                    var lt = await _context.LeaveTypes.FindAsync(topLeaveType.LeaveTypeId);
                    topTypeName = lt?.Name ?? topTypeName;
                }

                var answer = $"System stats: Pending {totalPending} • Approved this month {totalApprovedThisMonth} • Most requested: {topTypeName}.";
                var actions = new List<ChatAction>();
                // Role-based: only show admin links to Admin/HR
                if (user.IsInRole("Admin") || user.IsInRole("HR"))
                {
                    actions.Add(new ChatAction("Admin Dashboard", "/Admin/Index", "fas fa-tachometer-alt"));
                    actions.Add(new ChatAction("Leave Statistics", "/Admin/LeaveStatistics", "fas fa-chart-line"));
                }
                // Always helpful for managers
                if (user.IsInRole("Manager") || user.IsInRole("Admin") || user.IsInRole("HR"))
                {
                    actions.Add(new ChatAction("Pending Approvals", "/Manager/PendingApprovals", "fas fa-tasks"));
                }
                return new ChatbotAnswer(answer, actions);
            }

            // Fallback
            var fallbackActions = new List<ChatAction>
            {
                new ChatAction("Apply Leave", "/Leave/Apply", "fas fa-plus-circle"),
                new ChatAction("My Applications", "/Leave/MyApplications", "fas fa-list"),
                new ChatAction("Leave Balance", "/Leave/Balance", "fas fa-chart-bar")
            };
            if (user.IsInRole("Admin") || user.IsInRole("HR"))
            {
                fallbackActions.Add(new ChatAction("Admin Stats", "/Admin/LeaveStatistics", "fas fa-chart-line"));
            }
            return new ChatbotAnswer(
                "I can help with applying for leave, balances, approvals, policies, and admin stats. What would you like to do?",
                fallbackActions);
        }

        private static ChatbotAnswer Intro(ClaimsPrincipal user)
        {
            var name = user?.Identity?.Name ?? "there";
            var actions = new List<ChatAction>
            {
                new ChatAction("Apply Leave", "/Leave/Apply", "fas fa-plus-circle"),
                new ChatAction("My Applications", "/Leave/MyApplications", "fas fa-list"),
                new ChatAction("Leave Balance", "/Leave/Balance", "fas fa-chart-bar")
            };
            if (user.IsInRole("Admin") || user.IsInRole("HR"))
            {
                actions.Add(new ChatAction("Admin Dashboard", "/Admin/Index", "fas fa-tachometer-alt"));
            }
            return new ChatbotAnswer($"Hi {name}! I’m your ESS assistant. Ask me about applying for leave, balances, approvals, policies, or admin stats.", actions);
        }

        private static bool ContainsAny(string text, params string[] terms)
        {
            foreach (var t in terms)
            {
                if (text.Contains(t)) return true;
            }
            return false;
        }
    }
}
