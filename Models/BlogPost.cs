using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LapTopBD.Models
{
    [Table("BlogPosts")]
    public class BlogPost
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Admin")]
        public int AdminId { get; set; }

        public Admin? Admin { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(220)]
        public string Slug { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Summary { get; set; }

        [Required]
        public string ContentHtml { get; set; } = string.Empty;

        [StringLength(300)]
        public string? CoverImageUrl { get; set; }

        public bool IsPublished { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        public DateTime? PublishedAt { get; set; }
    }
}
