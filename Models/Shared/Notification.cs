using System.ComponentModel.DataAnnotations;

namespace HRManagement.Shared.Models
{
    public enum NotificationType
    {
        LeaveSubmitted,
        LeaveApproved,
        LeaveRejected,
        LeaveCancelled,
        LeaveRequiresApproval,
        LeaveUrgentApproval,
        LeaveAutoApproved,
        System
    }

    public class Notification
    {
        public int NotificationId { get; set; }

        [Required]
        public int EmployeeId { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(1000)]
        public string Message { get; set; } = string.Empty;

        [StringLength(500)]
        public string? ActionUrl { get; set; }

        public NotificationType NotificationType { get; set; }

        public bool IsRead { get; set; }

        public DateTime CreatedDate { get; set; }

        public DateTime? ReadDate { get; set; }
    }
}
