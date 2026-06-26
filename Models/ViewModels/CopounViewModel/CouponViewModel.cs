using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.ComponentModel.DataAnnotations;

namespace resturanyar.Models.ViewModels.CopounViewModel
{



    public class CouponViewModel
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "کد تخفیف الزامی است")]
        [Display(Name = "کد تخفیف")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "کد تخفیف باید بین 3 تا 50 کاراکتر باشد")]
        public string Code { get; set; }

        [Required(ErrorMessage = "نوع تخفیف الزامی است")]
        [Display(Name = "نوع تخفیف")]
        public string DiscountType { get; set; }

        [Required(ErrorMessage = "مقدار تخفیف الزامی است")]
        [Display(Name = "مقدار تخفیف")]
        [Range(0.01, 100000000, ErrorMessage = "مقدار تخفیف باید بیشتر از صفر باشد")]
        public decimal DiscountValue { get; set; }

        [Display(Name = "حداکثر مبلغ تخفیف")]
        [Range(0, 100000000, ErrorMessage = "مقدار نامعتبر")]
        public decimal? MaxDiscountAmount { get; set; }

        [Display(Name = "حداقل مبلغ خرید")]
        [Range(0, 100000000, ErrorMessage = "مقدار نامعتبر")]
        public decimal? MinPurchaseAmount { get; set; }

        [Required(ErrorMessage = "تاریخ شروع الزامی است")]
        [Display(Name = "تاریخ شروع")]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "تاریخ پایان الزامی است")]
        [Display(Name = "تاریخ پایان")]
        public DateTime EndDate { get; set; }

        [Display(Name = "فعال")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "محدودیت استفاده")]
        [Range(0, 10000, ErrorMessage = "مقدار نامعتبر")]
        public int? UsageLimit { get; set; }

        [Display(Name = "محدودیت استفاده برای هر مالک")]
        [Range(1, 100, ErrorMessage = "مقدار باید بین 1 تا 100 باشد")]
        public int LimitPerOwner { get; set; } = 1;

        [Display(Name = "مالک خاص")]
        public int? SpecificOwnerId { get; set; }

        [Display(Name = "رستوران خاص")]
        public int? SpecificRestaurantId { get; set; }

        [Display(Name = "نوع کد تخفیف")]
        public string CouponScope { get; set; } = "General";

        // لیست‌های انتخاب برای Dropdown
        public List<SelectListItem> Owners { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> Restaurants { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> DiscountTypes { get; set; } = new List<SelectListItem>();
    }
}

