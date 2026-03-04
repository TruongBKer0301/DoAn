using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
namespace LapTopBD.Models
{
    [Table("banner")]
    public class Banner
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? ImageUrl { get; set; }
        public bool Status { get; set; }
        public int Position { get; set; } // Thêm trường Position kiểu int
        public DateTime CreationDate { get; set; }
        public DateTime UpdationDate { get; set; }
        public int AdminId { get; set; }
    }
}
