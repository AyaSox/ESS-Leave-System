using System.ComponentModel.DataAnnotations;

namespace ESSLeaveSystem.Models
{
    public class ChatbotQueryLog
    {
        public int ChatbotQueryLogId { get; set; }
        [StringLength(256)]
        public string? Email { get; set; }
        [Required]
        [StringLength(1000)]
        public string Question { get; set; } = string.Empty;
        [Required]
        public string Answer { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}
