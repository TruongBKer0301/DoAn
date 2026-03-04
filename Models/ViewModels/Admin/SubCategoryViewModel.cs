using System.ComponentModel.DataAnnotations;

namespace LapTopBD.ViewModels
{
    public class SubCategoryViewModel
    {
        public int Id { get; set; }

        [Required]
        public int CategoryId { get; set; }

        [Required, StringLength(255)]
        public string? SubCategoryName { get; set; }

        public string? CreationDate { get; set; }

        public string? UpdationDate { get; set; }

        // Tùy chọn: Bao gồm tên danh mục để hiển thị
        public string? CategoryName { get; set; }
    }
}
