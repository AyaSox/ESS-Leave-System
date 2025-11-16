using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ESSLeaveSystem.Data;
using HRManagement.Shared.Models;
using HRManagement.Shared.DTOs;

namespace ESSLeaveSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LeaveBalancesController : ControllerBase
    {
        private readonly LeaveDbContext _context;

        public LeaveBalancesController(LeaveDbContext context)
        {
            _context = context;
        }

        [HttpGet("employee/{employeeId}/year/{year}")]
        public async Task<ActionResult<ApiResponse<List<LeaveBalance>>>> GetEmployeeLeaveBalances(int employeeId, int year)
        {
            try
            {
                var balances = await _context.LeaveBalances
                    .Where(lb => lb.EmployeeId == employeeId && lb.Year == year)
                    .OrderBy(lb => lb.LeaveTypeId)
                    .ToListAsync();

                return Ok(new ApiResponse<List<LeaveBalance>>
                {
                    Success = true,
                    Data = balances,
                    Message = "Leave balances retrieved successfully"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<List<LeaveBalance>>
                {
                    Success = false,
                    Message = "An error occurred while retrieving leave balances",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpGet("employee/{employeeId}/leavetype/{leaveTypeId}/year/{year}")]
        public async Task<ActionResult<ApiResponse<LeaveBalance>>> GetLeaveBalance(int employeeId, int leaveTypeId, int year)
        {
            try
            {
                var balance = await _context.LeaveBalances
                    .FirstOrDefaultAsync(lb => lb.EmployeeId == employeeId 
                        && lb.LeaveTypeId == leaveTypeId 
                        && lb.Year == year);

                if (balance == null)
                {
                    return NotFound(new ApiResponse<LeaveBalance>
                    {
                        Success = false,
                        Message = "Leave balance not found"
                    });
                }

                return Ok(new ApiResponse<LeaveBalance>
                {
                    Success = true,
                    Data = balance,
                    Message = "Leave balance retrieved successfully"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<LeaveBalance>
                {
                    Success = false,
                    Message = "An error occurred while retrieving the leave balance",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpPost("initialize")]
        public async Task<ActionResult<ApiResponse<List<LeaveBalance>>>> InitializeLeaveBalances(LeaveBalanceInitializationRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new ApiResponse<List<LeaveBalance>>
                    {
                        Success = false,
                        Message = "Invalid request data",
                        Errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).ToList()
                    });
                }

                // Get all active leave types
                var activeLeaveTypes = await _context.LeaveTypes
                    .Where(lt => lt.IsActive)
                    .ToListAsync();

                var createdBalances = new List<LeaveBalance>();

                foreach (var leaveType in activeLeaveTypes)
                {
                    var existingBalance = await _context.LeaveBalances
                        .FirstOrDefaultAsync(lb => lb.EmployeeId == request.EmployeeId 
                            && lb.LeaveTypeId == leaveType.LeaveTypeId 
                            && lb.Year == request.Year);

                    if (existingBalance == null)
                    {
                        var newBalance = new LeaveBalance
                        {
                            EmployeeId = request.EmployeeId,
                            LeaveTypeId = leaveType.LeaveTypeId,
                            Year = request.Year,
                            TotalDays = leaveType.DefaultDaysPerYear,
                            UsedDays = 0,
                            PendingDays = 0,
                            CarryForwardDays = 0,
                            CreatedDate = DateTime.Now
                        };

                        _context.LeaveBalances.Add(newBalance);
                        createdBalances.Add(newBalance);
                    }
                }

                await _context.SaveChangesAsync();

                return Ok(new ApiResponse<List<LeaveBalance>>
                {
                    Success = true,
                    Data = createdBalances,
                    Message = $"Initialized {createdBalances.Count} leave balances for employee {request.EmployeeId}"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<List<LeaveBalance>>
                {
                    Success = false,
                    Message = "An error occurred while initializing leave balances",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ApiResponse<LeaveBalance>>> UpdateLeaveBalance(int id, LeaveBalance leaveBalance)
        {
            try
            {
                if (id != leaveBalance.LeaveBalanceId)
                {
                    return BadRequest(new ApiResponse<LeaveBalance>
                    {
                        Success = false,
                        Message = "Leave balance ID mismatch"
                    });
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(new ApiResponse<LeaveBalance>
                    {
                        Success = false,
                        Message = "Invalid leave balance data",
                        Errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).ToList()
                    });
                }

                leaveBalance.LastModifiedDate = DateTime.Now;
                _context.Entry(leaveBalance).State = EntityState.Modified;

                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!LeaveBalanceExists(id))
                    {
                        return NotFound(new ApiResponse<LeaveBalance>
                        {
                            Success = false,
                            Message = "Leave balance not found"
                        });
                    }
                    throw;
                }

                return Ok(new ApiResponse<LeaveBalance>
                {
                    Success = true,
                    Data = leaveBalance,
                    Message = "Leave balance updated successfully"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<LeaveBalance>
                {
                    Success = false,
                    Message = "An error occurred while updating the leave balance",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        private bool LeaveBalanceExists(int id)
        {
            return _context.LeaveBalances.Any(e => e.LeaveBalanceId == id);
        }
    }
}