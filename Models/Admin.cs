using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LapTopBD.Models;

namespace LapTopBD.Models
{
    [Table("admin")]
    public class Admin
    {
        [Key]
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string? Username { get; set; }

        [Required, StringLength(20)]
        public string? Password { get; set; }

        public string? FullName { get; set; }
        public string? Avatar { get; set; }
        public string? Roles { get; set; }
        public DateTime? CreationDate { get; set; } = DateTime.Now;
        public DateTime? UpdationDate { get; set; }

        // Thêm trạng thái (chỉ có "Hoạt động" hoặc "Không hoạt động")
        [Required, StringLength(20)]
        public string Status { get; set; } = "Hoạt động";

        public virtual ICollection<Product> Products { get; set; } = new List<Product>();
        public virtual ICollection<Category> Categories { get; set; } = new List<Category>();
    }
}
