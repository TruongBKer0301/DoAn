using System.ComponentModel.DataAnnotations;

namespace LapTopBD.Models.ViewModels.Admin
{
    public class AdminLoginViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập tài khoản")]
        public string? Username { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
        [DataType(DataType.Password)]
        public string? Password { get; set; }

        public bool RememberMe { get; set; }
    }
}
