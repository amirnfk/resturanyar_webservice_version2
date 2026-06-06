namespace resturanyar.Models

{

    using System.ComponentModel.DataAnnotations;

    public class FoodItemCreateRequest
    {
        [Required(ErrorMessage = "نام آیتم غذایی الزامی است")]
        [StringLength(100, ErrorMessage = "نام نمی‌تواند بیشتر از 100 کاراکتر باشد")]
        public string Name { get; set; }

        [StringLength(500, ErrorMessage = "توضیحات نمی‌تواند بیشتر از 500 کاراکتر باشد")]
        public string? Description { get; set; }

        public IFormFile? Image { get; set; }

        
        public int CategoryId { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "قیمت باید عدد مثبت باشد")]
        public decimal Price { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "قیمت تخفیف باید عدد مثبت باشد")]
        public decimal? DiscountPrice { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "قیمت تمام شده باید عدد مثبت باشد")]
        public decimal? CostPrice { get; set; }

        public bool? isAvailable { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "رستوران معتبر نیست")]
        public int RestaurantId { get; set; }

        public int? RemoveImage { get; set; } // 1 برای حذف، null یا 0 برای عدم حذف
    }

    //namespace resturanyar.Models
    //{
    //    public class FoodItemCreateRequest
    //    {
    //        public string Name { get; set; }
    //        public string? Description { get; set; }
    //        public IFormFile? Image { get; set; }

    //        // ✅ تغییر داده شد به CategoryId
    //        public int CategoryId { get; set; }

    //        // اگر بعداً نیاز شد، می‌تونی SubCategory اضافه کنی
    //        // public string? SubCategory { get; set; }

    //        public decimal Price { get; set; }
    //        public decimal? DiscountPrice { get; set; }
    //        public decimal? CostPrice { get; set; }

    //        // nullable با پیش‌فرض true در متد
    //        public bool? isAvailable { get; set; }

    //        public int RestaurantId { get; set; }

    //        // اگر میخوای امکان حذف تصویر باشه
    //        public int? RemoveImage { get; set; }
    //    }
    //}
}