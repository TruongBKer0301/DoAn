namespace LapTopBD.Models.ViewModels.Admin
{
    public class OrderViewModel
    {
        public int Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public DateTime OrderDate { get; set; }
        public string? OrderStatus { get; set; }
        public decimal TotalPrice { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public string? Ward { get; set; }
        public string? Address { get; set; }

        public string FullAddress => string.Join(", ", new[] { Address, Ward, District, City }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }
}