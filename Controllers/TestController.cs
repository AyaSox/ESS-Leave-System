using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ESSLeaveSystem.Services;
using ESSLeaveSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ESSLeaveSystem.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class TestController : ControllerBase
    {
        private readonly ILeaveBalanceInitializationService _initService;
        private readonly IEmployeeLookupService _employeeLookup;
        private readonly LeaveDbContext _context;

        public TestController(
            ILeaveBalanceInitializationService initService,
            IEmployeeLookupService employeeLookup,
            LeaveDbContext context)
        {
            _initService = initService;
            _employeeLookup = employeeLookup;
            _context = context;
        }

        [HttpGet("initialize-balances")]
        public async Task<IActionResult> InitializeBalances()
        {
            try
            {
                var userEmail = User.Identity?.Name;
                if (string.IsNullOrEmpty(userEmail))
                {
                    return BadRequest("User not authenticated");
                }

                var employeeId = await _employeeLookup.GetEmployeeIdByEmailAsync(userEmail);
                if (!employeeId.HasValue)
                {
                    return BadRequest($"Employee not found for email: {userEmail}");
                }

                // Get employee details
                var employee = await _context.Employees
                    .Where(e => e.EmployeeId == employeeId.Value && !e.IsDeleted)
                    .Select(e => new { e.EmployeeId, e.FullName, e.Email, e.DateHired })
                    .FirstOrDefaultAsync();

                if (employee == null)
                {
                    return BadRequest($"Employee {employeeId.Value} not found in database");
                }

                // Check current balance status
                var currentYear = DateTime.Now.Year;
                var hireYear = employee.DateHired.Year;
                var expectedYears = currentYear - hireYear + 1;

                var yearsWithBalances = await _context.LeaveBalances
                    .Where(lb => lb.EmployeeId == employeeId.Value)
                    .Select(lb => lb.Year)
                    .Distinct()
                    .CountAsync();

                var result = new
                {
                    employee = new
                    {
                        employee.EmployeeId,
                        employee.FullName,
                        employee.Email,
                        employee.DateHired
                    },
                    balanceStatus = new
                    {
                        hireYear,
                        currentYear,
                        expectedYears,
                        yearsWithBalances,
                        needsInitialization = yearsWithBalances < expectedYears
                    }
                };

                // If missing balances, initialize them
                if (yearsWithBalances < expectedYears)
                {
                    var allBalances = await _initService.InitializeAllHistoricalLeaveBalancesAsync(employeeId.Value);
                    
                    return Ok(new
                    {
                        success = true,
                        message = "Leave balances initialized successfully",
                        result,
                        balancesCreated = allBalances.Count,
                        balancesByYear = allBalances.GroupBy(b => b.Year)
                            .OrderBy(g => g.Key)
                            .Select(g => new { year = g.Key, count = g.Count() })
                    });
                }
                else
                {
                    return Ok(new
                    {
                        success = true,
                        message = "Leave balances already complete",
                        result
                    });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        [HttpGet("employee-info")]
        public async Task<IActionResult> GetEmployeeInfo()
        {
            try
            {
                var userEmail = User.Identity?.Name;
                if (string.IsNullOrEmpty(userEmail))
                {
                    return BadRequest("User not authenticated");
                }

                var employeeId = await _employeeLookup.GetEmployeeIdByEmailAsync(userEmail);
                if (!employeeId.HasValue)
                {
                    return BadRequest($"Employee not found for email: {userEmail}");
                }

                var employee = await _context.Employees
                    .Where(e => e.EmployeeId == employeeId.Value)
                    .FirstOrDefaultAsync();

                var balances = await _context.LeaveBalances
                    .Where(lb => lb.EmployeeId == employeeId.Value)
                    .ToListAsync();

                return Ok(new
                {
                    employee,
                    balanceCount = balances.Count,
                    balancesByYear = balances.GroupBy(b => b.Year)
                        .OrderBy(g => g.Key)
                        .Select(g => new { year = g.Key, count = g.Count() })
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }
    }
}