using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ESSLeaveSystem.Data;
using HRManagement.Shared.Models;
using HRManagement.Shared.DTOs;

namespace ESSLeaveSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmployeesController : ControllerBase
    {
        private readonly LeaveDbContext _context;
        private readonly ILogger<EmployeesController> _logger;

        public EmployeesController(LeaveDbContext context, ILogger<EmployeesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpPost("sync")]
        public async Task<ActionResult<ApiResponse<List<EmployeeDto>>>> SyncEmployees(List<EmployeeDto> employees)
        {
            try
            {
                _logger.LogInformation("Syncing {Count} employees from HR system", employees.Count);

                // This would typically store employee data in a local cache table
                // For now, we'll return success to indicate sync capability
                
                var syncedEmployees = new List<EmployeeDto>();
                
                foreach (var employee in employees)
                {
                    // Initialize leave balances for new employees
                    await InitializeEmployeeLeaveBalances(employee.EmployeeId, DateTime.Now.Year);
                    syncedEmployees.Add(employee);
                }

                return Ok(new ApiResponse<List<EmployeeDto>>
                {
                    Success = true,
                    Data = syncedEmployees,
                    Message = $"Successfully synced {syncedEmployees.Count} employees"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while syncing employees");
                return StatusCode(500, new ApiResponse<List<EmployeeDto>>
                {
                    Success = false,
                    Message = "An error occurred while syncing employees",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpGet("{employeeId}/hierarchy")]
        public async Task<ActionResult<ApiResponse<EmployeeHierarchyDto>>> GetEmployeeHierarchy(int employeeId)
        {
            try
            {
                // This would typically query a local employee cache or call HR API
                // For now, return a placeholder structure
                var hierarchy = new EmployeeHierarchyDto
                {
                    EmployeeId = employeeId,
                    DirectReports = new List<int>(),
                    LineManagerId = null,
                    CanApproveLeave = false
                };

                return Ok(new ApiResponse<EmployeeHierarchyDto>
                {
                    Success = true,
                    Data = hierarchy,
                    Message = "Employee hierarchy retrieved successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving employee hierarchy for {EmployeeId}", employeeId);
                return StatusCode(500, new ApiResponse<EmployeeHierarchyDto>
                {
                    Success = false,
                    Message = "An error occurred while retrieving employee hierarchy",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        private async Task InitializeEmployeeLeaveBalances(int employeeId, int year)
        {
            var existingBalances = await _context.LeaveBalances
                .Where(lb => lb.EmployeeId == employeeId && lb.Year == year)
                .ToListAsync();

            if (!existingBalances.Any())
            {
                var activeLeaveTypes = await _context.LeaveTypes
                    .Where(lt => lt.IsActive)
                    .ToListAsync();

                foreach (var leaveType in activeLeaveTypes)
                {
                    var balance = new LeaveBalance
                    {
                        EmployeeId = employeeId,
                        LeaveTypeId = leaveType.LeaveTypeId,
                        Year = year,
                        TotalDays = leaveType.DefaultDaysPerYear,
                        UsedDays = 0,
                        PendingDays = 0,
                        CarryForwardDays = 0,
                        CreatedDate = DateTime.Now
                    };

                    _context.LeaveBalances.Add(balance);
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Initialized leave balances for employee {EmployeeId}", employeeId);
            }
        }
    }

    public class EmployeeHierarchyDto
    {
        public int EmployeeId { get; set; }
        public int? LineManagerId { get; set; }
        public List<int> DirectReports { get; set; } = new();
        public bool CanApproveLeave { get; set; }
    }
}