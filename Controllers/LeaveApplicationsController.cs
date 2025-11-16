using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ESSLeaveSystem.Data;
using ESSLeaveSystem.Services;
using HRManagement.Shared.Models;
using HRManagement.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;

namespace ESSLeaveSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LeaveApplicationsController : ControllerBase
    {
        private readonly LeaveDbContext _context;
        private readonly IFileUploadService _fileUploadService;
        private readonly ILogger<LeaveApplicationsController> _logger;

        public LeaveApplicationsController(
            LeaveDbContext context, 
            IFileUploadService fileUploadService,
            ILogger<LeaveApplicationsController> logger)
        {
            _context = context;
            _fileUploadService = fileUploadService;
            _logger = logger;
        }

        [HttpGet("employee/{employeeId}")]
        public async Task<ActionResult<ApiResponse<List<LeaveApplication>>>> GetEmployeeLeaveApplications(int employeeId)
        {
            try
            {
                var applications = await _context.LeaveApplications
                    .Where(la => la.EmployeeId == employeeId)
                    .OrderByDescending(la => la.AppliedDate)
                    .ToListAsync();

                return Ok(new ApiResponse<List<LeaveApplication>>
                {
                    Success = true,
                    Data = applications,
                    Message = "Leave applications retrieved successfully"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<List<LeaveApplication>>
                {
                    Success = false,
                    Message = "An error occurred while retrieving leave applications",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpGet("manager/{managerId}/pending")]
        public async Task<ActionResult<ApiResponse<List<LeaveApplication>>>> GetPendingLeaveApplicationsForManager(int managerId)
        {
            try
            {
                // This would need to be enhanced with employee hierarchy data from HR system
                var applications = await _context.LeaveApplications
                    .Where(la => la.Status == LeaveStatus.Pending)
                    .OrderBy(la => la.AppliedDate)
                    .ToListAsync();

                return Ok(new ApiResponse<List<LeaveApplication>>
                {
                    Success = true,
                    Data = applications,
                    Message = "Pending leave applications retrieved successfully"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<List<LeaveApplication>>
                {
                    Success = false,
                    Message = "An error occurred while retrieving pending applications",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<LeaveApplication>>> GetLeaveApplication(int id)
        {
            try
            {
                var application = await _context.LeaveApplications.FindAsync(id);

                if (application == null)
                {
                    return NotFound(new ApiResponse<LeaveApplication>
                    {
                        Success = false,
                        Message = "Leave application not found"
                    });
                }

                return Ok(new ApiResponse<LeaveApplication>
                {
                    Success = true,
                    Data = application,
                    Message = "Leave application retrieved successfully"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<LeaveApplication>
                {
                    Success = false,
                    Message = "An error occurred while retrieving the leave application",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<LeaveApplication>>> CreateLeaveApplication([FromForm] LeaveApplicationWithFileRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new ApiResponse<LeaveApplication>
                    {
                        Success = false,
                        Message = "Invalid leave application data",
                        Errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).ToList()
                    });
                }

                // Handle file upload if present
                string? supportingDocumentPath = null;
                if (request.SupportingDocument != null)
                {
                    try
                    {
                        supportingDocumentPath = await _fileUploadService.UploadFileAsync(
                            request.SupportingDocument, 
                            $"leave-documents/{request.EmployeeId}");
                        _logger.LogInformation("Supporting document uploaded for leave application: {FilePath}", supportingDocumentPath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to upload supporting document");
                        return BadRequest(new ApiResponse<LeaveApplication>
                        {
                            Success = false,
                            Message = "Failed to upload supporting document: " + ex.Message
                        });
                    }
                }

                // Calculate total days (excluding weekends)
                var totalDays = CalculateWeekdaysBetween(request.StartDate, request.EndDate);

                // Check for overlapping applications
                var hasOverlapping = await _context.LeaveApplications
                    .Where(la => la.EmployeeId == request.EmployeeId
                        && (la.Status == LeaveStatus.Pending || la.Status == LeaveStatus.Approved)
                        && ((la.StartDate <= request.EndDate && la.EndDate >= request.StartDate)))
                    .AnyAsync();

                if (hasOverlapping)
                {
                    // Clean up uploaded file if validation fails
                    if (supportingDocumentPath != null)
                    {
                        await _fileUploadService.DeleteFileAsync(supportingDocumentPath);
                    }

                    return BadRequest(new ApiResponse<LeaveApplication>
                    {
                        Success = false,
                        Message = "You have overlapping leave applications for the selected dates"
                    });
                }

                // Check leave balance
                var leaveBalance = await _context.LeaveBalances
                    .FirstOrDefaultAsync(lb => lb.EmployeeId == request.EmployeeId 
                        && lb.LeaveTypeId == request.LeaveTypeId 
                        && lb.Year == request.StartDate.Year);

                if (leaveBalance != null && leaveBalance.AvailableDays < totalDays)
                {
                    // Clean up uploaded file if validation fails
                    if (supportingDocumentPath != null)
                    {
                        await _fileUploadService.DeleteFileAsync(supportingDocumentPath);
                    }

                    return BadRequest(new ApiResponse<LeaveApplication>
                    {
                        Success = false,
                        Message = $"Insufficient leave balance. Available: {leaveBalance.AvailableDays} days, Requested: {totalDays} days"
                    });
                }

                var leaveApplication = new LeaveApplication
                {
                    EmployeeId = request.EmployeeId,
                    LeaveTypeId = request.LeaveTypeId,
                    StartDate = request.StartDate,
                    EndDate = request.EndDate,
                    TotalDays = totalDays,
                    Reason = request.Reason,
                    ContactDuringLeave = request.ContactDuringLeave,
                    SupportingDocumentPath = supportingDocumentPath,
                    Status = LeaveStatus.Pending,
                    AppliedDate = DateTime.Now
                };

                _context.LeaveApplications.Add(leaveApplication);
                await _context.SaveChangesAsync();

                // Update pending days in leave balance
                if (leaveBalance != null)
                {
                    leaveBalance.PendingDays += totalDays;
                    leaveBalance.LastModifiedDate = DateTime.Now;
                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();

                return CreatedAtAction(nameof(GetLeaveApplication), new { id = leaveApplication.LeaveApplicationId },
                    new ApiResponse<LeaveApplication>
                    {
                        Success = true,
                        Data = leaveApplication,
                        Message = "Leave application submitted successfully"
                    });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new ApiResponse<LeaveApplication>
                {
                    Success = false,
                    Message = "An error occurred while creating the leave application",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpPost("{id}/approve")]
        public async Task<ActionResult<ApiResponse<LeaveApplication>>> ApproveLeaveApplication(int id, LeaveApprovalRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (id != request.LeaveApplicationId)
                {
                    return BadRequest(new ApiResponse<LeaveApplication>
                    {
                        Success = false,
                        Message = "Leave application ID mismatch"
                    });
                }

                var leaveApplication = await _context.LeaveApplications.FindAsync(id);
                if (leaveApplication == null)
                {
                    return NotFound(new ApiResponse<LeaveApplication>
                    {
                        Success = false,
                        Message = "Leave application not found"
                    });
                }

                if (leaveApplication.Status != LeaveStatus.Pending)
                {
                    return BadRequest(new ApiResponse<LeaveApplication>
                    {
                        Success = false,
                        Message = "Only pending applications can be approved"
                    });
                }

                if (request.IsApproved)
                {
                    leaveApplication.Status = LeaveStatus.Approved;
                }
                else
                {
                    leaveApplication.Status = LeaveStatus.Rejected;
                }

                leaveApplication.ReviewedById = request.ReviewerId;
                leaveApplication.ReviewedDate = DateTime.Now;
                leaveApplication.ReviewComments = request.Comments;

                // Update leave balance
                var leaveBalance = await _context.LeaveBalances
                    .FirstOrDefaultAsync(lb => lb.EmployeeId == leaveApplication.EmployeeId 
                        && lb.LeaveTypeId == leaveApplication.LeaveTypeId 
                        && lb.Year == leaveApplication.StartDate.Year);

                if (leaveBalance != null)
                {
                    leaveBalance.PendingDays -= leaveApplication.TotalDays;
                    
                    if (request.IsApproved)
                    {
                        leaveBalance.UsedDays += leaveApplication.TotalDays;
                    }
                    
                    leaveBalance.LastModifiedDate = DateTime.Now;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new ApiResponse<LeaveApplication>
                {
                    Success = true,
                    Data = leaveApplication,
                    Message = request.IsApproved ? "Leave application approved successfully" : "Leave application rejected"
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new ApiResponse<LeaveApplication>
                {
                    Success = false,
                    Message = "An error occurred while processing the leave application",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpPost("{id}/cancel")]
        public async Task<ActionResult<ApiResponse<LeaveApplication>>> CancelLeaveApplication(int id, int employeeId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var leaveApplication = await _context.LeaveApplications.FindAsync(id);
                if (leaveApplication == null)
                {
                    return NotFound(new ApiResponse<LeaveApplication>
                    {
                        Success = false,
                        Message = "Leave application not found"
                    });
                }

                if (leaveApplication.EmployeeId != employeeId)
                {
                    return Forbid();
                }

                if (!leaveApplication.CanBeCancelled)
                {
                    return BadRequest(new ApiResponse<LeaveApplication>
                    {
                        Success = false,
                        Message = "This leave application cannot be cancelled"
                    });
                }

                var previousStatus = leaveApplication.Status;
                leaveApplication.Status = LeaveStatus.Cancelled;
                leaveApplication.ReviewedDate = DateTime.Now;

                // Update leave balance
                var leaveBalance = await _context.LeaveBalances
                    .FirstOrDefaultAsync(lb => lb.EmployeeId == leaveApplication.EmployeeId 
                        && lb.LeaveTypeId == leaveApplication.LeaveTypeId 
                        && lb.Year == leaveApplication.StartDate.Year);

                if (leaveBalance != null)
                {
                    if (previousStatus == LeaveStatus.Pending)
                    {
                        leaveBalance.PendingDays -= leaveApplication.TotalDays;
                    }
                    else if (previousStatus == LeaveStatus.Approved)
                    {
                        leaveBalance.UsedDays -= leaveApplication.TotalDays;
                    }
                    
                    leaveBalance.LastModifiedDate = DateTime.Now;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new ApiResponse<LeaveApplication>
                {
                    Success = true,
                    Data = leaveApplication,
                    Message = "Leave application cancelled successfully"
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new ApiResponse<LeaveApplication>
                {
                    Success = false,
                    Message = "An error occurred while cancelling the leave application",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        private static decimal CalculateWeekdaysBetween(DateTime startDate, DateTime endDate)
        {
            decimal totalDays = 0;
            var currentDate = startDate;

            while (currentDate <= endDate)
            {
                // Skip weekends (Saturday and Sunday)
                if (currentDate.DayOfWeek != DayOfWeek.Saturday && currentDate.DayOfWeek != DayOfWeek.Sunday)
                {
                    totalDays++;
                }
                currentDate = currentDate.AddDays(1);
            }

            return totalDays;
        }
    }
}