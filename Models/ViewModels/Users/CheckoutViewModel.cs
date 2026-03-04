namespace LapTopBD.Models.ViewModels
{
    public class CheckoutViewModel
    {
        // Thông tin giao hàng
        public string? Name { get; set; }
        public string? ContactNo { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public string? Ward { get; set; }
        public string? Address { get; set; }

        // Phương thức thanh toán
        public string? PaymentMethod { get; set; }

        // Danh sách sản phẩm trong giỏ hàng
        public List<CartItem> CartItems { get; set; } = new List<CartItem>();

        // Tổng tiền
        public decimal TotalPrice { get; set; }
    }

    public class CartItem
    {
        public int ProductId { get; set; }
        public string? ProductName { get; set; }
        public decimal ProductPrice { get; set; }
        public int Quantity { get; set; }
        public string? ProductImage { get; set; } // Thêm thuộc tính để lưu đường dẫn ảnh
        public decimal Subtotal => ProductPrice * Quantity;
    }
}