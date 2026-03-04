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

        public virtual ICollection<OrderTrackHistory> OrderTrackHistories { get; set; } = new List<OrderTrackHistory>();

    }
}
