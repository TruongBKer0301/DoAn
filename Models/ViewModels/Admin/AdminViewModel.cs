namespace LapTopBD.ViewModels
{
    public class AdminViewModel
    {
        public int Id { get; set; }
        public string? Username { get; set; }
        public DateTime? CreationDate { get; set; }
        public DateTime? UpdationDate { get; set; }
        public string? FullName { get; set; }
        public string? Avatar { get; set; }
        public string? Roles { get; set; }
        public string? Status { get; set; } // "Hoạt động" hoặc "Không hoạt động"

        public string? OldPassword { get; set; }
        public string? NewPassword { get; set; }

        public string? Password { get; set; } 

        // Trả về class CSS tương ứng
        public string StatusClass
        {
            get
            {
                return Status == "Hoạt động" ? "active" : "inactive";
            }
        }
    }
}
