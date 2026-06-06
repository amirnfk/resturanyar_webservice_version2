using Microsoft.EntityFrameworkCore;
using resturanyar.Models;
using resturanyar.Models.CustomerModels;


namespace Resturanyar.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<Owner> Owners { get; set; }
        public DbSet<Restaurant> Restaurants { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<FoodItem> FoodItems { get; set; }
         public DbSet<OrderStatus> OrderStatus { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<OrderUpdate> OrderUpdates { get; set; }
        public DbSet<Category> Categories { get; set; }

        public DbSet<OtpEntry> OtpEntries { get; set; }
        public DbSet<RestaurantTable> RestaurantTables { get; set; }
        public DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }

        public DbSet<Subscription> Subscriptions { get; set; }

        public DbSet<Customer> Customers { get; set; }
        public DbSet<CustomerAddress> CustomerAddresses { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {


            base.OnModelCreating(modelBuilder);

            // 📌 Unique constraint on Owner.Phone
            modelBuilder.Entity<Owner>()
                .HasIndex(o => o.Phone)
                .IsUnique();

            
            modelBuilder.Entity<Category>()
        .HasOne<Restaurant>()
        .WithMany()
        .HasForeignKey(c => c.RestaurantId)
        .OnDelete(DeleteBehavior.Cascade);   // اگر رستوران حذف شود، دسته‌بندی‌هایش هم حذف شوند

            // 📌 Relationship: Order -> OrderStatus
            modelBuilder.Entity<Order>()
                .HasOne(o => o.Status)
                .WithMany(s => s.Orders)
                .HasForeignKey(o => o.StatusId);

            // 📌 Relationship: Order -> OrderItems
            modelBuilder.Entity<Order>()
                .HasMany(o => o.OrderItems)
                .WithOne(oi => oi.Order)
                .HasForeignKey(oi => oi.OrderId);

            // 📌 Relationship: Order -> Restaurant
            modelBuilder.Entity<Order>()
                .HasOne<Restaurant>()
                .WithMany()
                .HasForeignKey(o => o.RestaurantId)
                .OnDelete(DeleteBehavior.Restrict);

            // 📌 Relationship: User -> Role
            modelBuilder.Entity<User>()
                .HasOne(u => u.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(u => u.role_id);

            // 📌 Relationship: Owner -> Role
            modelBuilder.Entity<Owner>()
                .HasOne(o => o.Role)
                .WithMany(r => r.Owners)
                .HasForeignKey(o => o.role_id);
            modelBuilder.Entity<Category>()
    .HasOne(c => c.Restaurant)
    .WithMany(r => r.Categories)
    .HasForeignKey(c => c.RestaurantId)
    .OnDelete(DeleteBehavior.Cascade);

            // 📌 Composite Unique constraint on (User.name, User.restaurant_id)
            modelBuilder.Entity<User>()
                .HasIndex(u => new { u.name, u.restaurant_id })
                .IsUnique();
            modelBuilder.Entity<OrderUpdate>()
       .HasIndex(o => new { o.RestaurantId, o.TargetRoleId, o.UpdateTime });

            modelBuilder.Entity<OtpEntry>()
       .Property(e => e.CreatedAt)
       .ValueGeneratedOnAdd();


            // ========== تنظیمات جدول Customers ==========
            modelBuilder.Entity<Customer>(entity =>
            {
                entity.HasKey(c => c.CustomerId);

                entity.Property(c => c.RestaurantId)
                    .HasColumnName("RestaurantId");

                entity.HasOne(c => c.Restaurant)
                    .WithMany()
                    .HasForeignKey(c => c.RestaurantId)
                    .HasPrincipalKey(r => r.restaurant_id)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(c => new { c.RestaurantId, c.Mobile })
                    .IsUnique();

                entity.Property(c => c.IsActive)
                    .HasDefaultValue(true);

                entity.Property(c => c.CreatedAt)
                    .HasDefaultValueSql("GETDATE()");

                entity.Property(c => c.UpdatedAt)
                    .HasDefaultValueSql("GETDATE()");
            });


            // ========== تنظیمات جدول CustomerAddresses ==========
            modelBuilder.Entity<CustomerAddress>(entity =>
            {
                entity.HasKey(a => a.AddressId);

                // رابطه با مشتری
                entity.HasOne<Customer>()
                    .WithMany()
                    .HasForeignKey(a => a.CustomerId)
                    .OnDelete(DeleteBehavior.Cascade); // با حذف مشتری، آدرس‌هایش هم حذف شوند

                // ایندکس برای جستجوی سریع آدرس‌های یک مشتری
                entity.HasIndex(a => a.CustomerId);

                // مقادیر پیش‌فرض
                entity.Property(a => a.IsDefault)
                    .HasDefaultValue(false);
                entity.Property(a => a.CreatedAt)
                    .HasDefaultValueSql("GETDATE()");
                entity.Property(a => a.UpdatedAt)
                    .HasDefaultValueSql("GETDATE()");
            });

            ConfigureSubscriptionEntities(modelBuilder);

         

    }
     private void ConfigureSubscriptionEntities(ModelBuilder modelBuilder)
        {
            // 📌 Configuration for Subscription entity
            modelBuilder.Entity<Subscription>(entity =>
            {
                // Primary Key
                entity.HasKey(s => s.Id);

                // Relationships
                entity.HasOne(s => s.Restaurant)
                    .WithMany(r => r.Subscriptions)
                    .HasForeignKey(s => s.RestaurantId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(s => s.Owner)
                    .WithMany(o => o.Subscriptions)
                    .HasForeignKey(s => s.OwnerId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(s => s.SubscriptionPlan)
                    .WithMany(sp => sp.Subscriptions)
                    .HasForeignKey(s => s.SubscriptionPlanId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Indexes for better performance
                entity.HasIndex(s => s.RestaurantId);
                entity.HasIndex(s => s.OwnerId);
                entity.HasIndex(s => s.SubscriptionPlanId);
                entity.HasIndex(s => s.Status);
                entity.HasIndex(s => s.EndDate);
                entity.HasIndex(s => new { s.RestaurantId, s.Status });

                // Default values
                entity.Property(s => s.CreatedAt)
                    .HasDefaultValueSql("GETDATE()");

                entity.Property(s => s.UpdatedAt)
                    .HasDefaultValueSql("GETDATE()");

                entity.Property(s => s.IsPaid)
                    .HasDefaultValue(false);

                entity.Property(s => s.AutoRenew)
                    .HasDefaultValue(false);

                // Check constraints for valid values
                entity.HasCheckConstraint("CK_Subscription_Status",
                    "[Status] IN ('Active', 'Expired', 'Canceled', 'Pending', 'Suspended')");

                entity.HasCheckConstraint("CK_Subscription_Period",
                    "[SubscriptionPeriod] IN ('Monthly', '3Monthly', '6Monthly', '12Monthly')");

                // Ensure EndDate is after StartDate
                entity.HasCheckConstraint("CK_Subscription_Dates",
                    "[EndDate] > [StartDate]");
            });

            // 📌 Configuration for SubscriptionPlan entity (اگر قبلاً نبود)
            modelBuilder.Entity<SubscriptionPlan>(entity =>
            {
                entity.HasKey(sp => sp.Id);

                entity.HasIndex(sp => sp.Code)
                    .IsUnique();

                entity.HasIndex(sp => sp.IsActive);

                // Default values for SubscriptionPlan
                entity.Property(sp => sp.IsActive)
                    .HasDefaultValue(true);

                entity.Property(sp => sp.CreatedAt)
                    .HasDefaultValueSql("GETDATE()");

                entity.Property(sp => sp.UpdatedAt)
                    .HasDefaultValueSql("GETDATE()");
            });
        }
    }
}
