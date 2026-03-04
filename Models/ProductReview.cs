using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LapTopBD.Models
{
    public class ProductReview
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Product")]
        public int ProductId { get; set; }
        public Product? Product { get; set; }
        [Required]
        [Range(1, 5, ErrorMessage = "Đánh giá phải từ 1 đến 5 sao.")]
        public int Rating { get; set; } 

        public string? Summary { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập nội dung đánh giá.")]
        public string? Review { get; set; }

        public DateTime ReviewDate { get; set; } = DateTime.Now;

        public string? UserId { get; set; }
        public string? UserName { get; set; }
        public string? Email { get; set; }


    }
}