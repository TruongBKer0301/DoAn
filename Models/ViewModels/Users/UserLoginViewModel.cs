using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace LapTopBD.Models.ViewModels.User
{
    public class UserLoginViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập Email")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string? Email { get; set; }
        public string? Name { get; set; } 
        [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
        [DataType(DataType.Password)]
        public string? Password { get; set; }
        public bool RememberMe { get; set; }
    }
}
