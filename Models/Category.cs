using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace LapTopBD.Models
{
    [Table("category")]
    public class Category
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Admin")]
        public int AdminId { get; set; }
        public Admin? Admin { get; set; }

        [Required, StringLength(255)]
        public string? CategoryName { get; set; }

        public string? CategoryDescription { get; set; }

        public DateTime? CreationDate { get; set; } = DateTime.Now;
        public DateTime? UpdationDate { get; set; }

        public virtual ICollection<Product> Products { get; set; } = new List<Product>();
        public virtual ICollection<SubCategory> SubCategories { get; set; } = new List<SubCategory>();
    }
}
