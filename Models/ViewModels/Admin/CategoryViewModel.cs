namespace LapTopBD.ViewModels
{
    public class CategoryViewModel
    {
        public int Id { get; set; }
        public string? CategoryName { get; set; }
        public string? CategoryDescription { get; set; }
        public string? CreationDate { get; set; }
        public string? UpdationDate { get; set; }

        public int AdminId { get; set; }  // Thêm AdminId vào đây
        public string? AdminName { get; set; }
    }

}
