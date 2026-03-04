using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace LapTopBD.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required, StringLength(255)]
        public string? Name { get; set; }

        [Required, StringLength(255), EmailAddress]
        public string? Email { get; set; }

        [Required]
        public string? ContactNo { get; set; }

        [Required, StringLength(255)]
        public string? Password { get; set; }

        public DateTime RegDate { get; set; } = DateTime.Now;

        public DateTime? UpdationDate { get; set; }

        // Thông tin địa chỉ
        [ StringLength(255)]
        public string? City { get; set; }  // Thành phố

        [ StringLength(255)]
        public string? District { get; set; } // Quận/Huyện

        [StringLength(255)]
        public string? Ward { get; set; } // Phường/Xã

        [StringLength(500)]
        public string? Address { get; set; } // Địa chỉ cụ thể (số nhà, tên đường, v.v.)

        // Quan hệ với đơn hàng & danh sách yêu thích
        public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
        public virtual ICollection<Wishlist> Wishlists { get; set; } = new List<Wishlist>();
        public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();

    }
}
