using System.ComponentModel.DataAnnotations;

namespace LapTopBD.Models
{
    public class ContactRequest
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui long nhap ho va ten.")]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui long nhap email.")]
        [EmailAddress(ErrorMessage = "Email khong hop le.")]
        [StringLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui long nhap so dien thoai.")]
        [StringLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui long nhap noi dung.")]
        [StringLength(2000, MinimumLength = 10, ErrorMessage = "Noi dung can it nhat 10 ky tu.")]
        public string Message { get; set; } = string.Empty;

        public bool IsRead { get; set; }

        public DateTime? ReadAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}