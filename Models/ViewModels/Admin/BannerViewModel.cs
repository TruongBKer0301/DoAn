using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LapTopBD.ViewModels
{
    public class BannerViewModel
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? ImageUrl { get; set; }
        public bool Status { get; set; } // Phải cùng kiểu với trong model
        public int Position { get; set; }
        public DateTime CreationDate { get; set; }
        public DateTime UpdationDate { get; set; }
        [NotMapped]
        public IFormFile? ImageFile { get; set; }
    }
}
