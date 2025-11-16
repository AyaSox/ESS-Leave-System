using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using ESSLeaveSystem.Data;
using ESSLeaveSystem.Services;
using HRManagement.Shared.Models;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace ESSLeaveSystem.Pages.Leave
{
    [Authorize]
    public class ApplyModel : PageModel
    {
        private readonly LeaveDbContext _context;
        private readonly IEmployeeLookupService _employeeLookup;
        private readonly ILeaveBalanceInitializationService _balanceInitService;
        private readonly IPublicHolidayService _holidayService;
        private readonly ILeaveApprovalService _approvalService;
        private readonly INotificationService _notificationService;

        public ApplyModel(
            LeaveDbContext context, 
            IEmployeeLookupService employeeLookup,
            ILeaveBalanceInitializationService balanceInitService,
            IPublicHolidayService holidayService,
            ILeaveApprovalService approvalService,
            INotificationService notificationService)
        {
            _context = context;
            _employeeLookup = employeeLookup;
            _balanceInitService = balanceInitService;
            _holidayService = holidayService;
            _approvalService = approvalService;
            _notificationService = notificationService;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public List<LeaveType> LeaveTypes { get; set; } = new();
        public List<LeaveBalance> LeaveBalances { get; set; } = new();
        public EmployeeInfo? LineManager { get; set; }

        public class InputModel
        {
            [Required]
            [Display(Name = "Leave Type")]
            public int LeaveTypeId { get; set; }

            [Required]
            [DataType(DataType.Date)]
            [Display(Name = "Start Date")]
            public DateTime StartDate { get; set; } = DateTime.Today.AddDays(1);

            [Required]
            [DataType(DataType.Date)]
            [Display(Name = "End Date")]
            public DateTime EndDate { get; set; } = DateTime.Today.AddDays(1);

            [Required]
            [StringLength(1000, MinimumLength = 10)]
            [Display(Name = "Reason for Leave")]
            public string Reason { get; set; } = string.Empty;

            [StringLength(500)]
            [Display(Name = "Emergency Contact (Optional)")]
            public string? ContactDuringLeave { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadDataAsync();
            
            // Load notifications for header and get line manager info
            if (User.Identity?.Name != null)
            {
                var employeeId = await _employeeLookup.GetEmployeeIdByEmailAsync(User.Identity.Name);
                if (employeeId.HasValue)
                {
                    var notifications = await _notificationService.GetAllNotificationsAsync(employeeId.Value, 10);
                    ViewData["Notifications"] = notifications;
                    
                    // 1) Leave for calendar visualization (all statuses)
                    var calendarLeave = await _context.LeaveApplications
                        .Where(la => la.EmployeeId == employeeId.Value 
                                  && la.EndDate >= DateTime.Today.AddMonths(-2))
                        .Select(la => new { 
                            la.StartDate, 
                            la.EndDate, 
                            la.LeaveTypeId,
                            Status = la.Status.ToString()
                        })
                        .ToListAsync();

                    // 2) Leave that should block booking (Approved or Pending only)
                    var existingLeave = await _context.LeaveApplications
                        .Where(la => la.EmployeeId == employeeId.Value 
                                  && la.EndDate >= DateTime.Today.AddMonths(-2)
                                  && (la.Status == LeaveStatus.Approved || la.Status == LeaveStatus.Pending))
                        .Select(la => new { 
                            la.StartDate, 
                            la.EndDate, 
                            la.LeaveTypeId,
                            Status = la.Status.ToString()
                        })
                        .ToListAsync();
                    
                    ViewData["ExistingLeaveApplications"] = calendarLeave; // for calendar
                    ViewData["BlockingLeaveApplications"] = existingLeave; // for overlap check
                    
                    // Get public holidays for a broader window (previous, current, next two years)
                    var currentYear = DateTime.Today.Year;
                    var holidaysAgg = new List<object>();
                    for (var y = currentYear - 1; y <= currentYear + 2; y++)
                    {
                        holidaysAgg.AddRange(
                            _holidayService.GetPublicHolidays(y)
                                .Select(h => new { Date = h.Date.ToString("yyyy-MM-dd"), h.Name })
                        );
                    }
                    ViewData["PublicHolidays"] = holidaysAgg;
                    
                    // Get line manager information
                    try
                    {
                        LineManager = await _approvalService.GetApproverForEmployeeAsync(employeeId.Value);
                    }
                    catch (Exception)
                    {
                        LineManager = null; // Will show "No manager assigned" message
                    }
                }
            }
            
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await LoadDataAsync();
                return Page();
            }

            if (User.Identity?.Name == null)
            {
                return RedirectToPage("/Account/Login");
            }

            var employeeId = await _employeeLookup.GetEmployeeIdByEmailAsync(User.Identity.Name);
            if (!employeeId.HasValue)
            {
                ModelState.AddModelError(string.Empty, "Unable to identify employee. Please contact HR.");
                await LoadDataAsync();
                return Page();
            }

            // Validate dates
            if (Input.EndDate < Input.StartDate)
            {
                ModelState.AddModelError(nameof(Input.EndDate), "End date must be after start date.");
                await LoadDataAsync();
                return Page();
            }

            // Past dates are allowed for retrospective applications

            // Calculate total days (excluding weekends AND public holidays!)
            var totalDays = CalculateLeaveDays(Input.StartDate, Input.EndDate);
            
            if (totalDays <= 0)
            {
                ModelState.AddModelError(string.Empty, "Invalid leave period selected. No working days in the selected range.");
                await LoadDataAsync();
                return Page();
            }

            // Get or create leave balance (auto-initialize if doesn't exist)
            LeaveBalance leaveBalance;
            try
            {
                leaveBalance = await _balanceInitService.GetOrCreateLeaveBalanceAsync(
                    employeeId.Value, 
                    Input.LeaveTypeId, 
                    // Use the year of the leave period rather than current year
                    Input.StartDate.Year);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Unable to retrieve leave balance: {ex.Message}");
                await LoadDataAsync();
                return Page();
            }

            // Check eligibility based on leave type and BCEA rules
            var leaveType = await _context.LeaveTypes.FindAsync(Input.LeaveTypeId);
            if (leaveType != null)
            {
                var employee = await _context.Employees
                    .Where(e => e.EmployeeId == employeeId.Value && !e.IsDeleted)
                    .Select(e => new { e.EmployeeId, e.DateHired, e.IsDeleted })
                    .FirstOrDefaultAsync();

                if (employee != null)
                {
                    // Check eligibility
                    string? eligibilityError = null;
                    
                    if (leaveType.Name == "Sick Leave" && !_balanceInitService.IsEligibleForSickLeave(employee.DateHired, Input.StartDate))
                    {
                        var eligibleDate = employee.DateHired.AddDays(180);
                        eligibilityError = $"You are not eligible for sick leave yet. Eligibility date: {eligibleDate:MMM dd, yyyy} (after 6 months employment).";
                    }
                    else if (leaveType.Name == "Family Responsibility Leave" && !_balanceInitService.IsEligibleForFamilyLeave(employee.DateHired, Input.StartDate))
                    {
                        var eligibleDate = employee.DateHired.AddDays(120);
                        eligibilityError = $"You are not eligible for family responsibility leave yet. Eligibility date: {eligibleDate:MMM dd, yyyy} (after 4 months employment).";
                    }
                    else if (leaveType.Name == "Paternity Leave" && !_balanceInitService.IsEligibleForPaternityLeave(employee.DateHired, Input.StartDate))
                    {
                        var eligibleDate = employee.DateHired.AddYears(1);
                        eligibilityError = $"You are not eligible for paternity leave yet. Eligibility date: {eligibleDate:MMM dd, yyyy} (after 1 year employment).";
                    }

                    if (eligibilityError != null)
                    {
                        ModelState.AddModelError(string.Empty, eligibilityError);
                        await LoadDataAsync();
                        return Page();
                    }
                }
            }

            // Check for date conflicts with existing approved or pending leave
            var conflictingLeave = await CheckForDateConflictsAsync(employeeId.Value, Input.StartDate, Input.EndDate);
            if (conflictingLeave != null)
            {
                var conflictLeaveType = await _context.LeaveTypes.FindAsync(conflictingLeave.LeaveTypeId);
                var statusText = conflictingLeave.Status == LeaveStatus.Approved ? "approved" : "pending";
                ModelState.AddModelError(string.Empty, 
                    $"You already have {statusText} {conflictLeaveType?.Name ?? "leave"} from {conflictingLeave.StartDate:MMM dd, yyyy} to {conflictingLeave.EndDate:MMM dd, yyyy}. " +
                    "You cannot apply for overlapping leave dates.");
                await LoadDataAsync();
                return Page();
            }

            // Check if employee has sufficient leave balance
            if (leaveBalance.AvailableDays < totalDays)
            {
                ModelState.AddModelError(string.Empty, 
                    $"Insufficient leave balance. Available: {leaveBalance.AvailableDays} days, Requested: {totalDays} days.");
                await LoadDataAsync();
                return Page();
            }

            // Check for duplicate submission (same employee, dates, and type within last 2 minutes)
            var duplicateCheck = await _context.LeaveApplications
                .Where(la => la.EmployeeId == employeeId.Value 
                          && la.LeaveTypeId == Input.LeaveTypeId
                          && la.StartDate == Input.StartDate
                          && la.EndDate == Input.EndDate
                          && la.AppliedDate > DateTime.Now.AddMinutes(-2))
                .FirstOrDefaultAsync();

            if (duplicateCheck != null)
            {
                TempData["ErrorMessage"] = "Duplicate submission detected. This leave application was already submitted.";
                return RedirectToPage("/Leave/MyApplications");
            }

            // Create leave application
            var application = new LeaveApplication
            {
                EmployeeId = employeeId.Value,
                LeaveTypeId = Input.LeaveTypeId,
                StartDate = Input.StartDate,
                EndDate = Input.EndDate,
                TotalDays = totalDays,
                Reason = Input.Reason,
                ContactDuringLeave = Input.ContactDuringLeave,
                Status = LeaveStatus.Pending,
                AppliedDate = DateTime.Now
            };

            _context.LeaveApplications.Add(application);

            // Update leave balance (increase pending days)
            leaveBalance.PendingDays += totalDays;
            leaveBalance.LastModifiedDate = DateTime.Now;

            await _context.SaveChangesAsync();

            // Validate manager assignment before processing application
            EmployeeInfo? approver;
            try
            {
                approver = await _approvalService.GetApproverForEmployeeAsync(employeeId.Value);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                await LoadDataAsync();
                return Page();
            }
            
            // Send notification to employee (confirmation)
            await _notificationService.CreateNotificationAsync(
                employeeId.Value,
                "Leave Application Submitted",
                $"Your {leaveType?.Name} request for {totalDays} days has been submitted successfully. " +
                (approver != null ? $"It will be reviewed by {approver.FullName}." : "It will be reviewed by HR."),
                "/Leave/MyApplications",
                NotificationType.LeaveSubmitted
            );

            // Send notification to manager
            await _notificationService.NotifyManagerOfLeaveSubmissionAsync(
                employeeId.Value, 
                application.LeaveApplicationId);

            if (approver != null)
            {
                TempData["SuccessMessage"] = $"Leave application submitted successfully! It will be reviewed by {approver.FullName}. You will be notified of the decision.";
            }
            else
            {
                TempData["SuccessMessage"] = "Leave application submitted successfully! It will be reviewed by HR. You will be notified of the decision.";
            }

            return RedirectToPage("/Leave/MyApplications");
        }

        private async Task LoadDataAsync()
        {
            LeaveTypes = await _context.LeaveTypes
                .Where(lt => lt.IsActive)
                .OrderBy(lt => lt.Name)
                .ToListAsync();

            if (User.Identity?.Name != null)
            {
                var employeeId = await _employeeLookup.GetEmployeeIdByEmailAsync(User.Identity.Name);
                if (employeeId.HasValue)
                {
                    LeaveBalances = await _context.LeaveBalances
                        .Where(lb => lb.EmployeeId == employeeId.Value && lb.Year == DateTime.Now.Year)
                        .ToListAsync();

                    // Manually populate LeaveType names since navigation properties are ignored
                    if (LeaveBalances.Any())
                    {
                        var leaveTypeIds = LeaveBalances.Select(lb => lb.LeaveTypeId).Distinct().ToList();
                        var leaveTypesDict = await _context.LeaveTypes
                            .Where(lt => leaveTypeIds.Contains(lt.LeaveTypeId))
                            .ToDictionaryAsync(lt => lt.LeaveTypeId, lt => lt);

                        foreach (var balance in LeaveBalances)
                        {
                            if (leaveTypesDict.TryGetValue(balance.LeaveTypeId, out var leaveType))
                            {
                                balance.LeaveType = leaveType;
                            }
                        }
                    }
                    
                    // Load calendar data (for POST requests)
                    var calendarLeave = await _context.LeaveApplications
                        .Where(la => la.EmployeeId == employeeId.Value 
                                  && la.EndDate >= DateTime.Today.AddMonths(-2))
                        .Select(la => new { 
                            la.StartDate, 
                            la.EndDate, 
                            la.LeaveTypeId,
                            Status = la.Status.ToString()
                        })
                        .ToListAsync();

                    var blockingLeave = await _context.LeaveApplications
                        .Where(la => la.EmployeeId == employeeId.Value 
                                  && la.EndDate >= DateTime.Today.AddMonths(-2)
                                  && (la.Status == LeaveStatus.Approved || la.Status == LeaveStatus.Pending))
                        .Select(la => new { 
                            la.StartDate, 
                            la.EndDate, 
                            la.LeaveTypeId,
                            Status = la.Status.ToString()
                        })
                        .ToListAsync();
                    
                    ViewData["ExistingLeaveApplications"] = calendarLeave;
                    ViewData["BlockingLeaveApplications"] = blockingLeave;
                    
                    // Get public holidays
                    var currentYear = DateTime.Today.Year;
                    var publicHolidays = _holidayService.GetPublicHolidays(currentYear)
                        .Concat(_holidayService.GetPublicHolidays(currentYear + 1))
                        .Select(h => new { 
                            Date = h.Date.ToString("yyyy-MM-dd"), 
                            h.Name 
                        })
                        .ToList();
                    
                    ViewData["PublicHolidays"] = publicHolidays;
                    
                    Console.WriteLine($"?? Calendar data loaded: {publicHolidays.Count} holidays, {calendarLeave.Count} calendar items, {blockingLeave.Count} blocking items");
                }
                else
                {
                    // Ensure ViewData is always set
                    ViewData["ExistingLeaveApplications"] = new List<object>();
                    ViewData["BlockingLeaveApplications"] = new List<object>();
                }
            }
            else
            {
                // Ensure ViewData is always set
                ViewData["ExistingLeaveApplications"] = new List<object>();
                ViewData["BlockingLeaveApplications"] = new List<object>();
            }

            // Always load public holidays for a broader window
            var cy = DateTime.Today.Year;
            var holidayList = new List<object>();
            for (var y = cy - 1; y <= cy + 2; y++)
            {
                holidayList.AddRange(
                    _holidayService.GetPublicHolidays(y)
                        .Select(h => new { Date = h.Date.ToString("yyyy-MM-dd"), h.Name })
                );
            }
            ViewData["PublicHolidays"] = holidayList;
        }

        private decimal CalculateLeaveDays(DateTime startDate, DateTime endDate)
        {
            // Use public holiday service to count working days
            return _holidayService.CountWorkingDays(startDate, endDate);
        }

        /// <summary>
        /// Check if the requested leave dates conflict with existing approved or pending leave
        /// </summary>
        private async Task<LeaveApplication?> CheckForDateConflictsAsync(int employeeId, DateTime startDate, DateTime endDate)
        {
            return await _context.LeaveApplications
                .Where(la => la.EmployeeId == employeeId 
                          && (la.Status == LeaveStatus.Approved || la.Status == LeaveStatus.Pending)
                          && (
                              // New leave starts during existing leave
                              (startDate >= la.StartDate && startDate <= la.EndDate) ||
                              // New leave ends during existing leave
                              (endDate >= la.StartDate && endDate <= la.EndDate) ||
                              // New leave completely encompasses existing leave
                              (startDate <= la.StartDate && endDate >= la.EndDate)
                          ))
                .FirstOrDefaultAsync();
        }
    }
}