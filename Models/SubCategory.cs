using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace LapTopBD.Models
{
    [Table("subcategory")]
    public class SubCategory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Category")]
        public int CategoryId { get; set; }
        public Category? Category { get; set; }

        [Required, StringLength(255)]
        public string? SubCategoryName { get; set; }

        public DateTime? CreationDate { get; set; } = DateTime.Now;

        public DateTime? UpdationDate { get; set; }

        public virtual ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
