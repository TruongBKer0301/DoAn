namespace LapTopBD.ViewModels
{
    public class ProductsUserViewModel
    {
        public int Id { get; set; }
        public int AdminId { get; set; }
        public string? AdminName { get; set; }

        public int CategoryId { get; set; }  
        public string? CategoryName { get; set; }

        public int? SubCategoryId { get; set; }  
        public string? SubCategoryName { get; set; }


        public string? ProductName { get; set; }
        public string? ProductCompany { get; set; }
        public decimal ProductPrice { get; set; }
        public decimal? ProductPriceBeforeDiscount { get; set; }
        public string? ProductDescription { get; set; }

        public string? ProductImage1 { get; set; }
        public string? ProductImage2 { get; set; }
        public string? ProductImage3 { get; set; }

        public int ShippingCharge { get; set; }
        public string? ProductAvailability { get; set; }

        public DateTime PostingDate { get; set; }
        public DateTime? UpdationDate { get; set; }

        public string? Brand { get; set; }
        public string? CPU { get; set; }
        public string? RAM { get; set; }
        public string? Storage { get; set; }
        public string? GPU { get; set; }
        public string? VGA { get; set; }
        public string? Promotion { get; set; }
    }
}
