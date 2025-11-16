using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace HRManagement.Shared.DTOs
{
    public class LeaveApplicationRequest
    {
        [Required]
        public int EmployeeId { get; set; }

        [Required]
        public int LeaveTypeId { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; }

        [Required]
        [StringLength(1000, MinimumLength = 10)]
        public string Reason { get; set; } = string.Empty;

        [StringLength(500)]
        public string? ContactDuringLeave { get; set; }

        public string? SupportingDocumentPath { get; set; }
    }

    public class LeaveApprovalRequest
    {
        [Required]
        public int LeaveApplicationId { get; set; }

        [Required]
        public int ReviewerId { get; set; }

        [StringLength(1000)]
        public string? Comments { get; set; }

        [Required]
        public bool IsApproved { get; set; }
    }

    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
        public List<string>? Errors { get; set; }
    }

    public class PaginatedResponse<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasNextPage => PageNumber < TotalPages;
        public bool HasPreviousPage => PageNumber > 1;
    }

    public class LeaveBalanceInitializationRequest
    {
        [Required]
        public int EmployeeId { get; set; }

        [Required]
        public int Year { get; set; }
    }

    public class LeaveApplicationWithFileRequest
    {
        [Required]
        public int EmployeeId { get; set; }

        [Required]
        public int LeaveTypeId { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; }

        [Required]
        [StringLength(1000, MinimumLength = 10)]
        public string Reason { get; set; } = string.Empty;

        [StringLength(500)]
        public string? ContactDuringLeave { get; set; }

        public IFormFile? SupportingDocument { get; set; }
    }
}
