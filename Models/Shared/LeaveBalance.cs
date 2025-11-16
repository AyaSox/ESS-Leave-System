using System.ComponentModel.DataAnnotations;

namespace HRManagement.Shared.Models
{
    public class LeaveBalance
    {
        public int LeaveBalanceId { get; set; }

        [Required]
        [Display(Name = "Employee")]
        public int EmployeeId { get; set; }

        [Required]
        [Display(Name = "Leave Type")]
        public int LeaveTypeId { get; set; }

        [Required]
        [Display(Name = "Year")]
        public int Year { get; set; }

        [Required]
        [Range(0, 365)]
        [Display(Name = "Total Allocated Days")]
        public decimal TotalDays { get; set; }

        [Required] 
        [Range(0, 365)]
        [Display(Name = "Used Days")]
        public decimal UsedDays { get; set; }

        [Required]
        [Range(0, 365)]
        [Display(Name = "Pending Days")]
        public decimal PendingDays { get; set; }

        [Display(Name = "Available Days")]
        public decimal AvailableDays => TotalDays - UsedDays - PendingDays;

        [Display(Name = "Carry Forward Days")]
        public decimal CarryForwardDays { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? LastModifiedDate { get; set; }

        // Navigation properties for API responses
        public EmployeeDto? Employee { get; set; }
        public LeaveType? LeaveType { get; set; }
    }
}
