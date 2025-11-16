using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ESSLeaveSystem.Models
{
    public enum ProfileChangeStatus
    {
        Pending = 0,
        Approved = 1,
        Rejected = 2
    }

    [Table("EmployeeProfileChangeRequests")]
    public class EmployeeProfileChangeRequest
    {
        [Key]
        public int RequestId { get; set; }

        [Required]
        public int EmployeeId { get; set; }

        public int? ManagerId { get; set; }

        // JSON payload with proposed values
        [Required]
        [StringLength(4000)]
        public string NewValuesJson { get; set; } = string.Empty;

        // Snapshot of current values when request was created
        [StringLength(4000)]
        public string? OriginalValuesJson { get; set; }

        [Required]
        public ProfileChangeStatus Status { get; set; } = ProfileChangeStatus.Pending;

        [Required]
        public DateTime RequestedAt { get; set; } = DateTime.Now;

        public DateTime? ReviewedAt { get; set; }

        [StringLength(1000)]
        public string? ManagerComment { get; set; }

        // Navigation property
        [NotMapped]
        public Employee? Employee { get; set; }
    }
}
