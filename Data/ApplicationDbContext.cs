using LapTopBD.Models;
using Microsoft.EntityFrameworkCore;

namespace LapTopBD.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        // Các DbSet đại diện cho các bảng trong cơ sở dữ liệu
        public DbSet<Admin> admin { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<SubCategory> SubCategories { get; set; }
        public DbSet<Product> Product { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Order> Order { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<OrderTrackHistory> OrderTrackHistories { get; set; }
        public DbSet<ProductReview> ProductReviews { get; set; }
        public DbSet<Wishlist> Wishlist { get; set; }
        public DbSet<Banner> Banner { get; set; }
        public DbSet<VisitLog> VisitLogs { get; set; }
        public DbSet<ContactRequest> ContactRequests { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Thiết lập ánh xạ tên bảng
            modelBuilder.Entity<ProductReview>()
                .ToTable("productreviews"); // Thêm ánh xạ tên bảng

            modelBuilder.Entity<CartItem>().ToTable("CartItems");

            // Thiết lập các ràng buộc quan hệ
            modelBuilder.Entity<Product>()
                .HasOne(p => p.Category)
                .WithMany(c => c.Product)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Product>()
                .HasOne(p => p.SubCategory)
                .WithMany(sc => sc.Product)
                .HasForeignKey(p => p.SubCategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<SubCategory>()
                .HasOne(sc => sc.Category)
                .WithMany(c => c.SubCategories)
                .HasForeignKey(sc => sc.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Order>()
                .HasOne(o => o.User)
                .WithMany(u => u.Orders)
                .HasForeignKey(o => o.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Order>()
                .HasOne(o => o.Product)
                .WithMany(p => p.Orders)
                .HasForeignKey(o => o.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<OrderTrackHistory>()
                .HasOne(oth => oth.Order)
                .WithMany(o => o.OrderTrackHistories)
                .HasForeignKey(oth => oth.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProductReview>()
                .HasOne(pr => pr.Product)
                .WithMany(p => p.ProductReviews)
                .HasForeignKey(pr => pr.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Wishlist>()
                .HasOne(w => w.User)
                .WithMany(u => u.Wishlist)
                .HasForeignKey(w => w.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Banner>()
                .Property(b => b.Status)
                .HasColumnType("bit");

            modelBuilder.Entity<Wishlist>()
                .HasOne(w => w.Product)
                .WithMany(p => p.Wishlist
                )
                .HasForeignKey(w => w.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<VisitLog>()
                .HasIndex(v => v.VisitedAtUtc);

            modelBuilder.Entity<VisitLog>()
                .HasIndex(v => v.VisitorId);

            modelBuilder.Entity<ContactRequest>()
                .ToTable("ContactRequests");

         
        }
    }
}