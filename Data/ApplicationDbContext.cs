using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using EcommerceApp.Models;

namespace EcommerceApp.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Tablas
        public DbSet<Product> Products { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<HomeBanner> HomeBanners { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // =========================
            // Product
            // =========================
            modelBuilder.Entity<Product>(entity =>
            {
                entity.Property(p => p.Price)
                      .HasColumnType("numeric(18,2)"); // PostgreSQL compatible

                entity.Property(p => p.Name)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.Property(p => p.Description)
                      .IsRequired()
                      .HasMaxLength(500);

                entity.HasOne(p => p.Category)
                      .WithMany(c => c.Products)
                      .HasForeignKey(p => p.CategoryId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(p => p.CartItems)
                      .WithOne(c => c.Product)
                      .HasForeignKey(c => c.ProductId);

                entity.HasMany(p => p.OrderItems)
                      .WithOne(oi => oi.Product)
                      .HasForeignKey(oi => oi.ProductId);
            });

            // =========================
            // Category
            // =========================
            modelBuilder.Entity<Category>(entity =>
            {
                entity.Property(c => c.Name)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.HasMany(c => c.Products)
                      .WithOne(p => p.Category)
                      .HasForeignKey(p => p.CategoryId);
            });

            // =========================
            // CartItem
            // =========================
            modelBuilder.Entity<CartItem>(entity =>
            {
                entity.HasKey(c => c.Id);

                entity.HasOne(c => c.User)
                      .WithMany(u => u.CartItems)
                      .HasForeignKey(c => c.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(c => c.Product)
                      .WithMany(p => p.CartItems)
                      .HasForeignKey(c => c.ProductId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // =========================
            // Order
            // =========================
            modelBuilder.Entity<Order>(entity =>
            {
                entity.HasKey(o => o.Id);

                entity.HasOne(o => o.User)
                      .WithMany(u => u.Orders)
                      .HasForeignKey(o => o.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(o => o.OrderItems)
                      .WithOne(oi => oi.Order)
                      .HasForeignKey(oi => oi.OrderId);
            });

            // =========================
            // OrderItem
            // =========================
            modelBuilder.Entity<OrderItem>(entity =>
            {
                entity.HasKey(oi => oi.Id);

                entity.Property(oi => oi.Price)
                      .HasColumnType("numeric(18,2)");

                entity.HasOne(oi => oi.Product)
                      .WithMany(p => p.OrderItems)
                      .HasForeignKey(oi => oi.ProductId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(oi => oi.Order)
                      .WithMany(o => o.OrderItems)
                      .HasForeignKey(oi => oi.OrderId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // =========================
            // ApplicationUser (Identity)
            // =========================
            modelBuilder.Entity<ApplicationUser>(entity =>
            {
                // Ajuste para PostgreSQL boolean type
                entity.Property(u => u.EmailConfirmed).HasColumnType("boolean");
                entity.Property(u => u.PhoneNumberConfirmed).HasColumnType("boolean");
                entity.Property(u => u.TwoFactorEnabled).HasColumnType("boolean");
                entity.Property(u => u.LockoutEnabled).HasColumnType("boolean");

                entity.HasMany(u => u.CartItems)
                      .WithOne(c => c.User)
                      .HasForeignKey(c => c.UserId);

                entity.HasMany(u => u.Orders)
                      .WithOne(o => o.User)
                      .HasForeignKey(o => o.UserId);
            });

            // =========================
            // HomeBanner
            // =========================
            modelBuilder.Entity<HomeBanner>(entity =>
            {
                entity.Property(h => h.Title).HasMaxLength(150);
                entity.Property(h => h.Subtitle).HasMaxLength(300);
            });
        }
    }
}
