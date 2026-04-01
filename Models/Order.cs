using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace LapTopBD.Models
{
    public class Order
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("User")]
        public int UserId { get; set; }
        public User? User { get; set; }

        [Required]
        [ForeignKey("Product")]
        public int ProductId { get; set; }
        public Product? Product { get; set; }

        [Required]
        public int Quantity { get; set; }

        [Required]
        public string? PaymentMethod { get; set; }

        [Required]
        public string? OrderStatus { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPrice { get; set; } 
        public DateTime OrderDate { get; set; } = DateTime.Now;

        // Thông tin địa chỉ
        [StringLength(255)]
        public string? City { get; set; }  // Thành phố

        [StringLength(255)]
        public string? District { get; set; } // Quận/Huyện

        [StringLength(255)]
        public string? Ward { get; set; } // Phường/Xã

        [StringLength(500)]
        public string? Address { get; set; } // Địa chỉ cụ thể (số nhà, tên đường, v.v.)

        public virtual ICollection<OrderTrackHistory> OrderTrackHistories { get; set; } = new List<OrderTrackHistory>();

    }
}
