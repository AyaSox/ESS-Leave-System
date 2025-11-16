using System.ComponentModel.DataAnnotations;

namespace HRManagement.Shared.Models
{
    public enum LeaveStatus
    {
        Pending,
        Approved,
        Rejected,
        Cancelled
    }

    public class LeaveApplication
    {
        public int LeaveApplicationId { get; set; }

        [Required]
        [Display(Name = "Employee")]
        public int EmployeeId { get; set; }

        [Required]
        [Display(Name = "Leave Type")]
        public int LeaveTypeId { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Start Date")]
        public DateTime StartDate { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "End Date")]
        public DateTime EndDate { get; set; }

        [Required]
        [Range(0.5, 365)]
        [Display(Name = "Total Days")]
        public decimal TotalDays { get; set; }

        [Required]
        [StringLength(1000)]
        [Display(Name = "Reason")]
        public string Reason { get; set; } = string.Empty;

        [Display(Name = "Status")]
        public LeaveStatus Status { get; set; } = LeaveStatus.Pending;

        [Display(Name = "Applied Date")]
        public DateTime AppliedDate { get; set; } = DateTime.Now;

        [Display(Name = "Reviewed By")]
        public int? ReviewedById { get; set; }

        [Display(Name = "Reviewed Date")]
        public DateTime? ReviewedDate { get; set; }

        [StringLength(1000)]
        [Display(Name = "Review Comments")]
        public string? ReviewComments { get; set; }

        [StringLength(500)]
        [Display(Name = "Contact During Leave")]
        public string? ContactDuringLeave { get; set; }

        [Display(Name = "Supporting Document")]
        public string? SupportingDocumentPath { get; set; }

        // Navigation properties for API responses
        public EmployeeDto? Employee { get; set; }
        public LeaveType? LeaveType { get; set; }
        public EmployeeDto? ReviewedBy { get; set; }

        // Helper properties
        public bool CanBeApproved => Status == LeaveStatus.Pending;
        public bool CanBeCancelled => Status == LeaveStatus.Pending || Status == LeaveStatus.Approved;

        public string StatusBadgeClass => Status switch
        {
            LeaveStatus.Pending => "bg-warning",
            LeaveStatus.Approved => "bg-success", 
            LeaveStatus.Rejected => "bg-danger",
            LeaveStatus.Cancelled => "bg-secondary",
            _ => "bg-secondary"
        };

        public string StatusIcon => Status switch
        {
            LeaveStatus.Pending => "fa-clock",
            LeaveStatus.Approved => "fa-check-circle",
            LeaveStatus.Rejected => "fa-times-circle", 
            LeaveStatus.Cancelled => "fa-ban",
            _ => "fa-question"
        };
    }
}
