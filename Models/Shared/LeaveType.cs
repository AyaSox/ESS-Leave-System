using System.ComponentModel.DataAnnotations;

namespace HRManagement.Shared.Models
{
    public class LeaveType
    {
        public int LeaveTypeId { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Leave Type Name")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        [Range(0, 365)]
        [Display(Name = "Default Days Per Year")]
        public int DefaultDaysPerYear { get; set; }

        [Display(Name = "Requires Approval")]
        public bool RequiresApproval { get; set; } = true;

        [Display(Name = "Is Paid Leave")]
        public bool IsPaid { get; set; } = true;

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        [StringLength(50)]
        public string? Color { get; set; } = "bg-primary";
    }
}
