using System.ComponentModel.DataAnnotations;

namespace LapTopBD.Models.ViewModels.User
{
    public class VerifyEmailOtpViewModel
    {
        [Required(ErrorMessage = "Vui long nhap email")]
        [EmailAddress(ErrorMessage = "Email khong hop le")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "Vui long nhap ma OTP")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP gom 6 chu so")]
        public string? Otp { get; set; }
    }
}
