using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace LapTopBD.Models
{
    public class OrderTrackHistory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int OrderId { get; set; }

        [Required]
        [StringLength(255)]
        public string? Status { get; set; }

        public string? Remark { get; set; }

        [Required]
        public DateTime PostingDate { get; set; } = DateTime.Now;

        // Thiết lập quan hệ với bảng Orders
        [ForeignKey("OrderId")]
        public virtual Order? Order { get; set; }
    }
}
