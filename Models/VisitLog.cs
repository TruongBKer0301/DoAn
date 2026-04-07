using System.ComponentModel.DataAnnotations;

namespace LapTopBD.Models
{
    public class VisitLog
    {
        [Key]
        public long Id { get; set; }

        [Required]
        [MaxLength(64)]
        public string VisitorId { get; set; } = string.Empty;

        [Required]
        public DateTime VisitedAtUtc { get; set; }

        [MaxLength(200)]
        public string Path { get; set; } = string.Empty;

        [MaxLength(64)]
        public string Browser { get; set; } = string.Empty;

        [MaxLength(32)]
        public string Device { get; set; } = string.Empty;

        [MaxLength(64)]
        public string IpAddress { get; set; } = string.Empty;

        [MaxLength(512)]
        public string UserAgent { get; set; } = string.Empty;
    }
}
