using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace LapTopBD.Models
{
    [Table("Product")]
    public class Product
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Admin")]
        public int AdminId { get; set; }
        public Admin? Admin { get; set; }

        [Required]
        [ForeignKey("Category")]
        [Column("category")]
        public int CategoryId { get; set; }
        public Category? Category { get; set; }

        [ForeignKey("SubCategory")]
        [Column("subcategory")]
        public int? SubCategoryId { get; set; }
        public SubCategory? SubCategory { get; set; }

        [Required, StringLength(255)]
        public string? ProductName { get; set; }

        [Required]
        public decimal ProductPrice { get; set; }

        public decimal? ProductPriceBeforeDiscount { get; set; }

        public string? ProductDescription { get; set; }

        public string? ProductImage1 { get; set; }
        public string? ProductImage2 { get; set; }
        public string? ProductImage3 { get; set; }

        [Required]
        public int ShippingCharge { get; set; }

        [Required, StringLength(255)]
        public int quantity { get; set; }
        public DateTime PostingDate { get; set; } = DateTime.Now;

        public DateTime? UpdationDate { get; set; }

        public string? Brand { get; set; }
        public string? CPU { get; set; }
        public string? RAM { get; set; }
        public string? Storage { get; set; }
        public string? GPU { get; set; }

        public string? VGA { get; set; }

        public string? SIZE { get; set; }

        public string? WEIGHT { get; set; }

        public string? PIN { get; set; }
        public string? BONUS { get; set; }
        public string? Promotion { get; set; }
        public string? Slug { get; set; }

        public virtual ICollection<Wishlist> Wishlist { get; set; } = new List<Wishlist>();
        public virtual ICollection<ProductReview> ProductReviews { get; set; } = new List<ProductReview>();
        public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
        public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();


    }
}
