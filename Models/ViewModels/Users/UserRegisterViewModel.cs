using System.ComponentModel.DataAnnotations;

namespace LapTopBD.Models.ViewModels.User
{
    public class UserRegisterViewModel
    {
        [Required(ErrorMessage = "Vui long nhap email")]
        [EmailAddress(ErrorMessage = "Email khong hop le")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "Vui long nhap ho ten")]
        public string? Username { get; set; }

        [Required(ErrorMessage = "Vui long nhap so dien thoai")]
        public string? Phone { get; set; } 

        [Required(ErrorMessage = "Vui long nhap mat khau")]
        [MinLength(8, ErrorMessage = "Mat khau toi thieu 8 ky tu")]
        [RegularExpression("^(?=.*[a-z])(?=.*[A-Z])(?=.*\\d).+$", ErrorMessage = "Mat khau phai co chu hoa, chu thuong va so")]
        public string? Password { get; set; }

        [Required(ErrorMessage = "Vui long xac nhan mat khau")]
        [Compare(nameof(Password), ErrorMessage = "Mat khau xac nhan khong khop")]
        public string? Password2 { get; set; }
    }
}