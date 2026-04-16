using System.ComponentModel.DataAnnotations;

namespace LapTopBD.Models.ViewModels.User
{
    public class ForgotPasswordRequestViewModel
    {
        [Required(ErrorMessage = "Vui long nhap email")]
        [EmailAddress(ErrorMessage = "Email khong hop le")]
        public string? Email { get; set; }
    }
}
