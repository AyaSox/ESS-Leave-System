using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using ESSLeaveSystem.Data;
using ESSLeaveSystem.Services;
using ESSLeaveSystem.Models;
using HRManagement.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace ESSLeaveSystem.Pages.Admin
{
    [Authorize(Roles = "Admin,HR")]
    public class AdjustLeaveBalanceModel : PageModel
    {
        private readonly LeaveDbContext _context;
        private readonly ILeaveBalanceInitializationService _balanceInitService;

        public AdjustLeaveBalanceModel(
            LeaveDbContext context,
            ILeaveBalanceInitializationService balanceInitService)
        {
            _context = context;
            _balanceInitService = balanceInitService;
        }

        public List<Employee> AllEmployees { get; set; } = new();
        public List<LeaveBalance> CurrentBalances { get; set; } = new();
        public List<LeaveType> MissingLeaveTypes { get; set; } = new();
        public Dictionary<int, LeaveType> LeaveTypeLookup { get; set; } = new();
        public Employee? SelectedEmployee { get; set; }
        public int? SelectedEmployeeId { get; set; }
        public int SelectedYear { get; set; } = DateTime.Now.Year;

        public async Task<IActionResult> OnGetAsync(int? employeeId = null, int? year = null)
        {
            SelectedEmployeeId = employeeId;
            SelectedYear = year ?? DateTime.Now.Year;

            // Load all active employees
            AllEmployees = await _context.Employees
                .Where(e => !e.IsDeleted)
                .OrderBy(e => e.FullName)
                .ToListAsync();

            if (SelectedEmployeeId.HasValue)
            {
                // Load selected employee details
                SelectedEmployee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.EmployeeId == SelectedEmployeeId.Value && !e.IsDeleted);

                if (SelectedEmployee != null)
                {
                    // Load current balances for selected year
                    CurrentBalances = await _context.LeaveBalances
                        .Where(lb => lb.EmployeeId == SelectedEmployeeId.Value && lb.Year == SelectedYear)
                        .ToListAsync();

                    // Load leave types for balances and create lookup dictionary
                    var leaveTypeIds = CurrentBalances.Select(cb => cb.LeaveTypeId).ToList();
                    var leaveTypes = await _context.LeaveTypes
                        .Where(lt => leaveTypeIds.Contains(lt.LeaveTypeId))
                        .ToListAsync();

                    // Create lookup dictionary instead of assigning to navigation property (which is ignored in EF)
                    LeaveTypeLookup = leaveTypes.ToDictionary(lt => lt.LeaveTypeId, lt => lt);

                    // Find missing leave types
                    var existingLeaveTypeIds = CurrentBalances.Select(cb => cb.LeaveTypeId).ToList();
                    MissingLeaveTypes = await _context.LeaveTypes
                        .Where(lt => lt.IsActive && !existingLeaveTypeIds.Contains(lt.LeaveTypeId))
                        .ToListAsync();
                }
            }

            return Page();
        }

        public async Task<IActionResult> OnPostInitializeBalancesAsync(int employeeId, int year)
        {
            try
            {
                var balances = await _balanceInitService.InitializeEmployeeLeaveBalancesAsync(employeeId, year);
                
                var employee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.EmployeeId == employeeId);
                
                TempData["SuccessMessage"] = $"Leave balances initialized successfully for {employee?.FullName} ({year}). Created {balances.Count} leave balance records.";

                // Log the action
                Console.WriteLine($"HR Admin initialized leave balances for Employee {employeeId}, Year {year}. Created {balances.Count} records.");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Failed to initialize leave balances: {ex.Message}";
                Console.WriteLine($"Failed to initialize leave balances for Employee {employeeId}: {ex.Message}");
            }

            return RedirectToPage(new { employeeId, year });
        }

        public async Task<IActionResult> OnPostAddLeaveTypeAsync(int employeeId, int year, int leaveTypeId, decimal totalDays)
        {
            try
            {
                // Check if balance already exists
                var existingBalance = await _context.LeaveBalances
                    .FirstOrDefaultAsync(lb => lb.EmployeeId == employeeId 
                                            && lb.LeaveTypeId == leaveTypeId 
                                            && lb.Year == year);

                if (existingBalance != null)
                {
                    TempData["ErrorMessage"] = "Leave balance already exists for this leave type and year.";
                    return RedirectToPage(new { employeeId, year });
                }

                // Create new leave balance
                var leaveBalance = new LeaveBalance
                {
                    EmployeeId = employeeId,
                    LeaveTypeId = leaveTypeId,
                    Year = year,
                    TotalDays = totalDays,
                    UsedDays = 0,
                    PendingDays = 0,
                    CarryForwardDays = 0,
                    CreatedDate = DateTime.Now,
                    LastModifiedDate = DateTime.Now
                };

                _context.LeaveBalances.Add(leaveBalance);
                await _context.SaveChangesAsync();

                var employee = await _context.Employees.FirstOrDefaultAsync(e => e.EmployeeId == employeeId);
                var leaveType = await _context.LeaveTypes.FirstOrDefaultAsync(lt => lt.LeaveTypeId == leaveTypeId);

                TempData["SuccessMessage"] = $"Added {leaveType?.Name} balance ({totalDays} days) for {employee?.FullName}.";

                // Log the action
                Console.WriteLine($"HR Admin added {leaveType?.Name} balance for Employee {employeeId}: {totalDays} days");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Failed to add leave balance: {ex.Message}";
            }

            return RedirectToPage(new { employeeId, year });
        }

        public async Task<IActionResult> OnPostAdjustBalanceAsync(int balanceId, decimal totalDays, decimal usedDays, string reason)
        {
            try
            {
                // Do not Include(lb => lb.LeaveType) because navigation is ignored in the model to keep entities lightweight
                var balance = await _context.LeaveBalances
                    .FirstOrDefaultAsync(lb => lb.LeaveBalanceId == balanceId);

                if (balance == null)
                {
                    TempData["ErrorMessage"] = "Leave balance not found.";
                    return RedirectToPage();
                }

                // Validate adjustment
                if (usedDays > totalDays)
                {
                    TempData["ErrorMessage"] = "Used days cannot exceed total days.";
                    return RedirectToPage(new { employeeId = balance.EmployeeId, year = balance.Year });
                }

                if (usedDays < 0 || totalDays < 0)
                {
                    TempData["ErrorMessage"] = "Days cannot be negative.";
                    return RedirectToPage(new { employeeId = balance.EmployeeId, year = balance.Year });
                }

                // Store original values for logging
                var originalTotal = balance.TotalDays;
                var originalUsed = balance.UsedDays;

                // Apply adjustment
                balance.TotalDays = totalDays;
                balance.UsedDays = usedDays;
                balance.LastModifiedDate = DateTime.Now;

                await _context.SaveChangesAsync();

                var employee = await _context.Employees.FirstOrDefaultAsync(e => e.EmployeeId == balance.EmployeeId);
                var leaveType = await _context.LeaveTypes.FirstOrDefaultAsync(lt => lt.LeaveTypeId == balance.LeaveTypeId);

                // Build a smart success message showing only what changed
                var changes = new List<string>();
                if (originalTotal != totalDays)
                {
                    changes.Add($"Total: {originalTotal} to {totalDays}");
                }
                if (originalUsed != usedDays)
                {
                    changes.Add($"Used: {originalUsed} to {usedDays}");
                }

                var changesText = changes.Any() ? string.Join(", ", changes) : "No changes made";
                TempData["SuccessMessage"] = $"Leave balance adjusted for {employee?.FullName} - {leaveType?.Name}: {changesText}";

                // Log the adjustment with reason
                Console.WriteLine($"HR Admin adjusted leave balance for Employee {balance.EmployeeId} ({employee?.FullName}): " +
                                $"{leaveType?.Name} - {changesText}. Reason: {reason}");

                return RedirectToPage(new { employeeId = balance.EmployeeId, year = balance.Year });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Failed to adjust leave balance: {ex.Message}";
                Console.WriteLine($"Failed to adjust leave balance {balanceId}: {ex.Message}");
                return RedirectToPage();
            }
        }
    }
}