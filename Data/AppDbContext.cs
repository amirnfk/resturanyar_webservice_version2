using Microsoft.EntityFrameworkCore;
using resturanyar.Models;
using resturanyar.Models.AdminMessage;
using resturanyar.Models.AuthorizationModels;
using resturanyar.Models.Copoun;
using resturanyar.Models.CustomerModels;
using resturanyar.Models.Receipt;


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
        public DbSet<RestaurantSetting> RestaurantSettings { get; set; }
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
        public DbSet<Coupon> Coupons { get; set; }
        public DbSet<CouponUsage> CouponUsages { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<CustomerAddress> CustomerAddresses { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<AdminMessage> AdminMessages { get; set; }
        public DbSet<AdminMessageRecipient> AdminMessageRecipients { get; set; }
        public DbSet<AdminMessageRead> AdminMessageReads { get; set; }
        public DbSet<Article> Articles { get; set; }
        public DbSet<RestaurantChargeDefinition> RestaurantChargeDefinitions { get; set; }
        public DbSet<OrderReceiptSnapshot> OrderReceiptSnapshots { get; set; }
        public DbSet<ReceiptPrintHistory> ReceiptPrintHistories { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {


            base.OnModelCreating(modelBuilder);

            // 📌 Unique constraint on Owner.Phone
            modelBuilder.Entity<Owner>()
                .HasIndex(o => o.Phone)
                .IsUnique();

            modelBuilder.Entity<Restaurant>(entity =>
            {
                entity.ToTable("Restaurants", tb => tb.UseSqlOutputClause(false));

                entity.Property(r => r.CreatedAt)
                    .HasColumnName("created_at")
                    .HasDefaultValueSql("GETDATE()")
                    .ValueGeneratedOnAdd();

                entity.Property(r => r.UpdatedAt)
                    .HasColumnName("updated_at")
                    .HasDefaultValueSql("GETDATE()")
                    .ValueGeneratedOnAddOrUpdate();

                entity.Property(r => r.ReceiptChargesEnabled)
                    .HasDefaultValue(true);

                entity.Property(r => r.ReceiptChargesEnabledAt);
            });

            
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

            modelBuilder.Entity<Order>()
                .HasIndex(o => new { o.RestaurantId, o.CreatedAt })
                .HasDatabaseName("IX_Orders_RestaurantId_CreatedAt");

            modelBuilder.Entity<Order>()
                .Property(o => o.OrderType)
                .HasDefaultValue(OrderTypeKind.DineIn);

            modelBuilder.Entity<RestaurantChargeDefinition>(entity =>
            {
                entity.HasKey(d => d.Id);
                entity.HasIndex(d => d.RestaurantId);
                entity.HasIndex(d => new { d.RestaurantId, d.Code }).IsUnique();
                entity.Property(d => d.Code).HasMaxLength(50);
                entity.Property(d => d.Title).HasMaxLength(100);
                entity.Property(d => d.Value).HasColumnType("decimal(18,4)");
                entity.Property(d => d.IsEnabled).HasDefaultValue(false);
                entity.Property(d => d.IsTaxable).HasDefaultValue(false);
                entity.Property(d => d.PercentageBase).HasDefaultValue(PercentageBaseKind.ItemsNet);
                entity.Property(d => d.DisplayOrder).HasDefaultValue(0);
                entity.Property(d => d.AppliesToOrderTypes).HasDefaultValue(OrderTypeFlags.All);
                entity.Property(d => d.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
                entity.Property(d => d.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            });

            modelBuilder.Entity<OrderReceiptSnapshot>(entity =>
            {
                entity.HasKey(s => s.Id);
                entity.HasIndex(s => s.OrderId).IsUnique();
                entity.HasIndex(s => s.RestaurantId);
                entity.Property(s => s.ItemsSubtotal).HasColumnType("decimal(18,2)");
                entity.Property(s => s.GrandTotal).HasColumnType("decimal(18,2)");
                entity.Property(s => s.IssuedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            });

            modelBuilder.Entity<ReceiptPrintHistory>(entity =>
            {
                entity.ToTable("ReceiptPrintHistory");
                entity.HasKey(h => h.Id);
                entity.HasIndex(h => new { h.OrderId, h.PrintedAt });
                entity.Property(h => h.Channel).HasMaxLength(20).HasDefaultValue("Web");
                entity.Property(h => h.PrintedAt).HasDefaultValueSql("SYSUTCDATETIME()");
                entity.Property(h => h.ItemsSubtotal).HasColumnType("decimal(18,2)");
                entity.Property(h => h.GrandTotal).HasColumnType("decimal(18,2)");
            });

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

            // ========== تنظیمات جدول Coupons ==========
            modelBuilder.Entity<Coupon>(entity =>
            {
                entity.HasKey(c => c.Id);

                // ایندکس یکتا روی Code
                entity.HasIndex(c => c.Code)
                      .IsUnique()
                      .HasDatabaseName("IX_Coupons_Code");

                // روابط با Owner و Restaurant (اختیاری)
                entity.HasOne(c => c.SpecificOwner)
                      .WithMany()  // اگر Owner مجموعه‌ای از Coupon نداشته باشد
                      .HasForeignKey(c => c.SpecificOwnerId)
                      .OnDelete(DeleteBehavior.Restrict); // جلوگیری از حذف Owner در صورت وجود Coupon

                entity.HasOne(c => c.SpecificRestaurant)
                      .WithMany()  // اگر Restaurant مجموعه‌ای از Coupon نداشته باشد
                      .HasForeignKey(c => c.SpecificRestaurantId)
                      .OnDelete(DeleteBehavior.Restrict);

                // مقادیر پیش‌فرض (هماهنگ با دیتابیس)
                entity.Property(c => c.IsActive)
                      .HasDefaultValue(true);
                entity.Property(c => c.UsedCount)
                      .HasDefaultValue(0);
                entity.Property(c => c.LimitPerOwner)
                      .HasDefaultValue(1);
                entity.Property(c => c.CreatedAt)
                      .HasDefaultValueSql("GETDATE()");
                entity.Property(c => c.UpdatedAt)
                      .HasDefaultValueSql("GETDATE()");

                // اعتبارسنجی نوع تخفیف
                entity.HasCheckConstraint("CK_Coupon_DiscountType",
                    "[DiscountType] IN ('Percentage', 'FixedAmount')");
            });

            // ========== تنظیمات جدول CouponUsages ==========
            modelBuilder.Entity<CouponUsage>(entity =>
            {
                entity.HasKey(u => u.Id);

                // روابط
                entity.HasOne(u => u.Coupon)
                      .WithMany(c => c.Usages)  // اگر در Coupon مجموعه‌ای تعریف کرده‌اید
                      .HasForeignKey(u => u.CouponId)
                      .OnDelete(DeleteBehavior.Restrict); // جلوگیری از حذف Coupon در صورت وجود استفاده

                entity.HasOne(u => u.Subscription)
                      .WithMany()  // اگر Subscription مجموعه‌ای از CouponUsage نداشته باشد
                      .HasForeignKey(u => u.SubscriptionId)
                      .OnDelete(DeleteBehavior.Cascade); // با حذف اشتراک، استفاده‌ها هم حذف شوند

                entity.HasOne(u => u.Owner)
                      .WithMany()
                      .HasForeignKey(u => u.OwnerId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(u => u.Restaurant)
                      .WithMany()
                      .HasForeignKey(u => u.RestaurantId)
                      .OnDelete(DeleteBehavior.Restrict);

                // ایندکس‌ها
                entity.HasIndex(u => new { u.CouponId, u.OwnerId })
                      .HasDatabaseName("IX_CouponUsages_CouponId_OwnerId");

                entity.HasIndex(u => u.SubscriptionId)
                      .HasDatabaseName("IX_CouponUsages_SubscriptionId");

                // ایندکس یکتا برای جلوگیری از استفاده‌ی تکراری توسط یک مالک (در صورت Success)
                entity.HasIndex(u => new { u.CouponId, u.OwnerId })
                      .HasDatabaseName("IX_CouponUsages_UniquePerOwner")
                      .HasFilter("[Status] = 'Success' AND [CouponId] IS NOT NULL AND [OwnerId] IS NOT NULL")
                      .IsUnique(); // این ایندکس یکتا تضمین می‌کند هر مالک فقط یک بار از هر کد استفاده کند

                // مقادیر پیش‌فرض
                entity.Property(u => u.UsedAt)
                      .HasDefaultValueSql("GETDATE()");
                entity.Property(u => u.Status)
                      .HasDefaultValue("Success");
            });


            modelBuilder.Entity<RestaurantSetting>(entity =>
            {
                entity.HasKey(s => s.RestaurantId);

                entity.HasOne(s => s.Restaurant)
                    .WithOne(r => r.Setting)
                    .HasForeignKey<RestaurantSetting>(s => s.RestaurantId)
                    .HasPrincipalKey<Restaurant>(r => r.restaurant_id)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(s => s.PrimaryColor)
                    .HasMaxLength(9)
                    .HasDefaultValue("#f97316");

                entity.Property(s => s.SecondaryColor)
                    .HasMaxLength(9)
                    .HasDefaultValue("#fff7ed");

                entity.Property(s => s.BackgroundImageUrl)
                    .HasMaxLength(500);

                entity.Property(s => s.LogoUrl)
                    .HasMaxLength(500);

                entity.Property(s => s.MenuHeroBadge)
                    .HasMaxLength(80);

                entity.Property(s => s.MenuTagline)
                    .HasMaxLength(160);

                entity.HasCheckConstraint("CK_RestaurantSettings_PrimaryColor",
                    "[PrimaryColor] LIKE '#[0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f]'");

                entity.HasCheckConstraint("CK_RestaurantSettings_SecondaryColor",
                    "[SecondaryColor] LIKE '#[0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f]'");
            });

            ConfigureSubscriptionEntities(modelBuilder);
            ConfigureAdminMessageEntities(modelBuilder);

            modelBuilder.Entity<Article>(entity =>
            {
                entity.HasKey(a => a.Id);
                entity.HasIndex(a => a.Slug).IsUnique();
                entity.Property(a => a.PublishedAt).HasDefaultValueSql("GETDATE()");
                entity.Property(a => a.IsPublished).HasDefaultValue(true);
                entity.Property(a => a.Author).HasDefaultValue("رستورانیار");
            });

    }
     private void ConfigureAdminMessageEntities(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AdminMessage>(entity =>
            {
                entity.HasKey(m => m.Id);
                entity.Property(m => m.CreatedAt).HasDefaultValueSql("GETDATE()");
                entity.Property(m => m.IsActive).HasDefaultValue(true);
                entity.HasCheckConstraint("CK_AdminMessages_MessageType", "[MessageType] IN (0, 1)");
            });

            modelBuilder.Entity<AdminMessageRecipient>(entity =>
            {
                entity.HasKey(r => r.Id);
                entity.HasIndex(r => r.RestaurantId);
                entity.HasIndex(r => new { r.MessageId, r.RestaurantId }).IsUnique();

                entity.HasOne(r => r.Message)
                    .WithMany(m => m.Recipients)
                    .HasForeignKey(r => r.MessageId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne<Restaurant>()
                    .WithMany()
                    .HasForeignKey(r => r.RestaurantId)
                    .HasPrincipalKey(r => r.restaurant_id)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<AdminMessageRead>(entity =>
            {
                entity.HasKey(r => r.Id);
                entity.HasIndex(r => r.RestaurantId);
                entity.HasIndex(r => new { r.MessageId, r.RestaurantId }).IsUnique();
                entity.Property(r => r.ReadAt).HasDefaultValueSql("GETDATE()");

                entity.HasOne(r => r.Message)
                    .WithMany(m => m.Reads)
                    .HasForeignKey(r => r.MessageId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne<Restaurant>()
                    .WithMany()
                    .HasForeignKey(r => r.RestaurantId)
                    .HasPrincipalKey(r => r.restaurant_id)
                    .OnDelete(DeleteBehavior.Cascade);
            });
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


                entity.Property(s => s.CouponId)
             .IsRequired(false); // قابل‌تهی (برای سازگاری با داده‌های قبلی)

                entity.HasOne(s => s.Coupon)
                      .WithMany()  // اگر Coupon مجموعه‌ای از Subscription نداشته باشد
                      .HasForeignKey(s => s.CouponId)
                      .OnDelete(DeleteBehavior.SetNull); // اگر کوپن حذف شود، مقدار NULL شود

                // ایندکس روی CouponId برای گزارش‌گیری سریع
                entity.HasIndex(s => s.CouponId)
                      .HasDatabaseName("IX_Subscriptions_CouponId");

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
