using System.ComponentModel.DataAnnotations;

namespace LapTopBD.Models.ViewModels.User
{
    public class ResetPasswordWithOtpViewModel
    {
        [Required(ErrorMessage = "Vui long nhap email")]
        [EmailAddress(ErrorMessage = "Email khong hop le")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "Vui long nhap ma OTP")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP gom 6 chu so")]
        public string? Otp { get; set; }

        [Required(ErrorMessage = "Vui long nhap mat khau moi")]
        [MinLength(8, ErrorMessage = "Mat khau toi thieu 8 ky tu")]
        [RegularExpression("^(?=.*[a-z])(?=.*[A-Z])(?=.*\\d).+$", ErrorMessage = "Mat khau phai co chu hoa, chu thuong va so")]
        public string? NewPassword { get; set; }

        [Required(ErrorMessage = "Vui long xac nhan mat khau")]
        [Compare(nameof(NewPassword), ErrorMessage = "Mat khau xac nhan khong khop")]
        public string? ConfirmPassword { get; set; }
    }
}
