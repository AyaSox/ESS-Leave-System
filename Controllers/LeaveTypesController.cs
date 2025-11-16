using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using ESSLeaveSystem.Data;
using ESSLeaveSystem.Services;
using HRManagement.Shared.Models;
using HRManagement.Shared.DTOs;

namespace ESSLeaveSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LeaveTypesController : ControllerBase
    {
        private readonly LeaveDbContext _context;
        private readonly ICacheService _cacheService;
        private readonly ILogger<LeaveTypesController> _logger;

        public LeaveTypesController(
            LeaveDbContext context, 
            ICacheService cacheService, 
            ILogger<LeaveTypesController> logger)
        {
            _context = context;
            _cacheService = cacheService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<LeaveType>>>> GetLeaveTypes([FromQuery] bool includeInactive = false)
        {
            try
            {
                _logger.LogInformation("Retrieving leave types, includeInactive: {IncludeInactive}", includeInactive);

                var cacheKey = includeInactive ? CacheKeys.LEAVE_TYPES : CacheKeys.ACTIVE_LEAVE_TYPES;
                
                var leaveTypes = await _cacheService.GetOrSetAsync(
                    cacheKey,
                    async () => await GetLeaveTypesFromDatabase(includeInactive),
                    TimeSpan.FromMinutes(30)
                );

                _logger.LogInformation("Retrieved {Count} leave types", leaveTypes.Count);

                return Ok(new ApiResponse<List<LeaveType>>
                {
                    Success = true,
                    Data = leaveTypes,
                    Message = $"Retrieved {leaveTypes.Count} leave types successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving leave types");
                return StatusCode(500, new ApiResponse<List<LeaveType>>
                {
                    Success = false,
                    Message = "An error occurred while retrieving leave types",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<LeaveType>>> GetLeaveType(int id)
        {
            try
            {
                _logger.LogInformation("Retrieving leave type with ID: {LeaveTypeId}", id);

                var cacheKey = $"leave_type_{id}";
                var leaveType = await _cacheService.GetOrSetAsync(
                    cacheKey,
                    async () => await _context.LeaveTypes.FindAsync(id),
                    TimeSpan.FromMinutes(15)
                );

                if (leaveType == null)
                {
                    _logger.LogWarning("Leave type not found with ID: {LeaveTypeId}", id);
                    return NotFound(new ApiResponse<LeaveType>
                    {
                        Success = false,
                        Message = "Leave type not found"
                    });
                }

                return Ok(new ApiResponse<LeaveType>
                {
                    Success = true,
                    Data = leaveType,
                    Message = "Leave type retrieved successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving leave type with ID: {LeaveTypeId}", id);
                return StatusCode(500, new ApiResponse<LeaveType>
                {
                    Success = false,
                    Message = "An error occurred while retrieving the leave type",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin,HR")]
        public async Task<ActionResult<ApiResponse<LeaveType>>> CreateLeaveType(LeaveType leaveType)
        {
            try
            {
                _logger.LogInformation("Creating new leave type: {LeaveTypeName}", leaveType.Name);

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).ToList();
                    _logger.LogWarning("Invalid leave type data: {Errors}", string.Join(", ", errors));
                    
                    return BadRequest(new ApiResponse<LeaveType>
                    {
                        Success = false,
                        Message = "Invalid leave type data",
                        Errors = errors
                    });
                }

                // Check for duplicate names
                var existingLeaveType = await _context.LeaveTypes
                    .FirstOrDefaultAsync(lt => lt.Name.ToLower() == leaveType.Name.ToLower());

                if (existingLeaveType != null)
                {
                    return BadRequest(new ApiResponse<LeaveType>
                    {
                        Success = false,
                        Message = "A leave type with this name already exists"
                    });
                }

                // Set default values
                if (string.IsNullOrEmpty(leaveType.Color))
                    leaveType.Color = "bg-primary";

                _context.LeaveTypes.Add(leaveType);
                await _context.SaveChangesAsync();

                // Clear cache after creating new leave type
                await _cacheService.RemoveAsync(CacheKeys.LEAVE_TYPES);
                await _cacheService.RemoveAsync(CacheKeys.ACTIVE_LEAVE_TYPES);

                _logger.LogInformation("Successfully created leave type: {LeaveTypeName} with ID: {LeaveTypeId}", 
                    leaveType.Name, leaveType.LeaveTypeId);

                return CreatedAtAction(nameof(GetLeaveType), new { id = leaveType.LeaveTypeId }, 
                    new ApiResponse<LeaveType>
                    {
                        Success = true,
                        Data = leaveType,
                        Message = "Leave type created successfully"
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating leave type: {LeaveTypeName}", leaveType?.Name);
                return StatusCode(500, new ApiResponse<LeaveType>
                {
                    Success = false,
                    Message = "An error occurred while creating the leave type",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,HR")]
        public async Task<ActionResult<ApiResponse<LeaveType>>> UpdateLeaveType(int id, LeaveType leaveType)
        {
            try
            {
                _logger.LogInformation("Updating leave type with ID: {LeaveTypeId}", id);

                if (id != leaveType.LeaveTypeId)
                {
                    return BadRequest(new ApiResponse<LeaveType>
                    {
                        Success = false,
                        Message = "Leave type ID mismatch"
                    });
                }

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).ToList();
                    _logger.LogWarning("Invalid leave type data for ID {LeaveTypeId}: {Errors}", id, string.Join(", ", errors));
                    
                    return BadRequest(new ApiResponse<LeaveType>
                    {
                        Success = false,
                        Message = "Invalid leave type data",
                        Errors = errors
                    });
                }

                // Check if the leave type exists
                var existingLeaveType = await _context.LeaveTypes.FindAsync(id);
                if (existingLeaveType == null)
                {
                    return NotFound(new ApiResponse<LeaveType>
                    {
                        Success = false,
                        Message = "Leave type not found"
                    });
                }

                // Check for duplicate names (excluding current record)
                var duplicateLeaveType = await _context.LeaveTypes
                    .FirstOrDefaultAsync(lt => lt.Name.ToLower() == leaveType.Name.ToLower() && lt.LeaveTypeId != id);

                if (duplicateLeaveType != null)
                {
                    return BadRequest(new ApiResponse<LeaveType>
                    {
                        Success = false,
                        Message = "A leave type with this name already exists"
                    });
                }

                // Update properties
                existingLeaveType.Name = leaveType.Name;
                existingLeaveType.Description = leaveType.Description;
                existingLeaveType.DefaultDaysPerYear = leaveType.DefaultDaysPerYear;
                existingLeaveType.RequiresApproval = leaveType.RequiresApproval;
                existingLeaveType.IsPaid = leaveType.IsPaid;
                existingLeaveType.IsActive = leaveType.IsActive;
                existingLeaveType.Color = leaveType.Color ?? "bg-primary";

                try
                {
                    await _context.SaveChangesAsync();
                    
                    // Clear cache after updating
                    await _cacheService.RemoveAsync(CacheKeys.LEAVE_TYPES);
                    await _cacheService.RemoveAsync(CacheKeys.ACTIVE_LEAVE_TYPES);
                    await _cacheService.RemoveAsync($"leave_type_{id}");

                    _logger.LogInformation("Successfully updated leave type: {LeaveTypeName} with ID: {LeaveTypeId}", 
                        leaveType.Name, id);
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    _logger.LogError(ex, "Concurrency error while updating leave type with ID: {LeaveTypeId}", id);
                    
                    if (!LeaveTypeExists(id))
                    {
                        return NotFound(new ApiResponse<LeaveType>
                        {
                            Success = false,
                            Message = "Leave type not found"
                        });
                    }
                    throw;
                }

                return Ok(new ApiResponse<LeaveType>
                {
                    Success = true,
                    Data = existingLeaveType,
                    Message = "Leave type updated successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating leave type with ID: {LeaveTypeId}", id);
                return StatusCode(500, new ApiResponse<LeaveType>
                {
                    Success = false,
                    Message = "An error occurred while updating the leave type",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteLeaveType(int id)
        {
            try
            {
                _logger.LogInformation("Attempting to delete leave type with ID: {LeaveTypeId}", id);

                var leaveType = await _context.LeaveTypes.FindAsync(id);
                if (leaveType == null)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Leave type not found"
                    });
                }

                // Check if leave type is being used in leave applications
                var hasApplications = await _context.LeaveApplications
                    .AnyAsync(la => la.LeaveTypeId == id);

                if (hasApplications)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Cannot delete leave type as it is being used in leave applications. Consider deactivating instead."
                    });
                }

                // Check if leave type is being used in leave balances
                var hasBalances = await _context.LeaveBalances
                    .AnyAsync(lb => lb.LeaveTypeId == id);

                if (hasBalances)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Cannot delete leave type as it is being used in leave balances. Consider deactivating instead."
                    });
                }

                _context.LeaveTypes.Remove(leaveType);
                await _context.SaveChangesAsync();

                // Clear cache
                await _cacheService.RemoveAsync(CacheKeys.LEAVE_TYPES);
                await _cacheService.RemoveAsync(CacheKeys.ACTIVE_LEAVE_TYPES);
                await _cacheService.RemoveAsync($"leave_type_{id}");

                _logger.LogInformation("Successfully deleted leave type: {LeaveTypeName} with ID: {LeaveTypeId}", 
                    leaveType.Name, id);

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Leave type deleted successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting leave type with ID: {LeaveTypeId}", id);
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "An error occurred while deleting the leave type",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpPatch("{id}/toggle-status")]
        [Authorize(Roles = "Admin,HR")]
        public async Task<ActionResult<ApiResponse<LeaveType>>> ToggleLeaveTypeStatus(int id)
        {
            try
            {
                _logger.LogInformation("Toggling status for leave type with ID: {LeaveTypeId}", id);

                var leaveType = await _context.LeaveTypes.FindAsync(id);
                if (leaveType == null)
                {
                    return NotFound(new ApiResponse<LeaveType>
                    {
                        Success = false,
                        Message = "Leave type not found"
                    });
                }

                leaveType.IsActive = !leaveType.IsActive;
                await _context.SaveChangesAsync();

                // Clear cache
                await _cacheService.RemoveAsync(CacheKeys.LEAVE_TYPES);
                await _cacheService.RemoveAsync(CacheKeys.ACTIVE_LEAVE_TYPES);
                await _cacheService.RemoveAsync($"leave_type_{id}");

                var statusText = leaveType.IsActive ? "activated" : "deactivated";
                _logger.LogInformation("Successfully {StatusText} leave type: {LeaveTypeName} with ID: {LeaveTypeId}", 
                    statusText, leaveType.Name, id);

                return Ok(new ApiResponse<LeaveType>
                {
                    Success = true,
                    Data = leaveType,
                    Message = $"Leave type {statusText} successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while toggling status for leave type with ID: {LeaveTypeId}", id);
                return StatusCode(500, new ApiResponse<LeaveType>
                {
                    Success = false,
                    Message = "An error occurred while updating the leave type status",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpGet("statistics")]
        [Authorize(Roles = "Admin,HR")]
        public async Task<ActionResult<ApiResponse<LeaveTypeStatistics>>> GetLeaveTypeStatistics()
        {
            try
            {
                _logger.LogInformation("Retrieving leave type statistics");

                var statistics = await _cacheService.GetOrSetAsync(
                    "leave_type_statistics",
                    async () => await CalculateLeaveTypeStatistics(),
                    TimeSpan.FromMinutes(10)
                );

                return Ok(new ApiResponse<LeaveTypeStatistics>
                {
                    Success = true,
                    Data = statistics,
                    Message = "Leave type statistics retrieved successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving leave type statistics");
                return StatusCode(500, new ApiResponse<LeaveTypeStatistics>
                {
                    Success = false,
                    Message = "An error occurred while retrieving statistics",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        private async Task<List<LeaveType>> GetLeaveTypesFromDatabase(bool includeInactive)
        {
            var query = _context.LeaveTypes.AsQueryable();
            
            if (!includeInactive)
            {
                query = query.Where(lt => lt.IsActive);
            }
            
            return await query.OrderBy(lt => lt.Name).ToListAsync();
        }

        private async Task<LeaveTypeStatistics> CalculateLeaveTypeStatistics()
        {
            var totalLeaveTypes = await _context.LeaveTypes.CountAsync();
            var activeLeaveTypes = await _context.LeaveTypes.CountAsync(lt => lt.IsActive);
            var paidLeaveTypes = await _context.LeaveTypes.CountAsync(lt => lt.IsPaid);
            var approvalRequiredTypes = await _context.LeaveTypes.CountAsync(lt => lt.RequiresApproval);

            var mostUsedLeaveType = await _context.LeaveApplications
                .GroupBy(la => la.LeaveTypeId)
                .Select(g => new { LeaveTypeId = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .FirstOrDefaultAsync();

            string mostUsedLeaveTypeName = null;
            if (mostUsedLeaveType != null)
            {
                var leaveType = await _context.LeaveTypes.FindAsync(mostUsedLeaveType.LeaveTypeId);
                mostUsedLeaveTypeName = leaveType?.Name;
            }

            return new LeaveTypeStatistics
            {
                TotalLeaveTypes = totalLeaveTypes,
                ActiveLeaveTypes = activeLeaveTypes,
                InactiveLeaveTypes = totalLeaveTypes - activeLeaveTypes,
                PaidLeaveTypes = paidLeaveTypes,
                UnpaidLeaveTypes = totalLeaveTypes - paidLeaveTypes,
                ApprovalRequiredTypes = approvalRequiredTypes,
                AutoApprovedTypes = totalLeaveTypes - approvalRequiredTypes,
                MostUsedLeaveType = mostUsedLeaveTypeName,
                MostUsedLeaveTypeApplications = mostUsedLeaveType?.Count ?? 0
            };
        }

        private bool LeaveTypeExists(int id)
        {
            return _context.LeaveTypes.Any(e => e.LeaveTypeId == id);
        }
    }

    public class LeaveTypeStatistics
    {
        public int TotalLeaveTypes { get; set; }
        public int ActiveLeaveTypes { get; set; }
        public int InactiveLeaveTypes { get; set; }
        public int PaidLeaveTypes { get; set; }
        public int UnpaidLeaveTypes { get; set; }
        public int ApprovalRequiredTypes { get; set; }
        public int AutoApprovedTypes { get; set; }
        public string? MostUsedLeaveType { get; set; }
        public int MostUsedLeaveTypeApplications { get; set; }
    }
}