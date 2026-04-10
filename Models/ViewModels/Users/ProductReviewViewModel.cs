using System.ComponentModel.DataAnnotations;

namespace LapTopBD.ViewModels
{
    public class ProductReviewViewModel
    {
        public int ProductId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn số sao để đánh giá.")]
        [Range(1, 5, ErrorMessage = "Đánh giá phải từ 1 đến 5 sao.")]
        public int Rating { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập nội dung đánh giá.")]
        public string? Review { get; set; }

        // Các trường này chỉ cần thiết nếu người dùng chưa đăng nhập
        public string? UserName { get; set; }
        public string? Email { get; set; }
    }
}