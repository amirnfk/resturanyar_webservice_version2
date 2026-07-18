using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace resturanyar.Models.ViewModels.Admin
{
    public class ArticleAdminViewModel
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "عنوان مقاله الزامی است")]
        [MaxLength(300, ErrorMessage = "عنوان نباید بیشتر از 300 کاراکتر باشد")]
        [Display(Name = "عنوان")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "اسلاگ (آدرس URL) الزامی است")]
        [MaxLength(200, ErrorMessage = "اسلاگ نباید بیشتر از 200 کاراکتر باشد")]
        [RegularExpression(@"^[a-z0-9]+(?:-[a-z0-9]+)*$", ErrorMessage = "اسلاگ فقط می‌تواند شامل حروف انگلیسی کوچک، اعداد و خط تیره باشد")]
        [Display(Name = "اسلاگ (URL)")]
        public string Slug { get; set; } = string.Empty;

        [Required(ErrorMessage = "توضیح متا الزامی است")]
        [MaxLength(500, ErrorMessage = "توضیح متا نباید بیشتر از 500 کاراکتر باشد")]
        [Display(Name = "توضیح متا (SEO)")]
        public string MetaDescription { get; set; } = string.Empty;

        [MaxLength(500, ErrorMessage = "کلمات کلیدی نباید بیشتر از 500 کاراکتر باشد")]
        [Display(Name = "کلمات کلیدی")]
        public string? Keywords { get; set; }

        [Required(ErrorMessage = "متن مقاله الزامی است")]
        [Display(Name = "متن مقاله")]
        public string Content { get; set; } = string.Empty;

        [Required(ErrorMessage = "تاریخ انتشار الزامی است")]
        [Display(Name = "تاریخ انتشار")]
        public DateTime PublishedAt { get; set; } = DateTime.Now;

        [Display(Name = "منتشر شده")]
        public bool IsPublished { get; set; } = true;

        [Required(ErrorMessage = "نام نویسنده الزامی است")]
        [MaxLength(100, ErrorMessage = "نام نویسنده نباید بیشتر از 100 کاراکتر باشد")]
        [Display(Name = "نویسنده")]
        public string Author { get; set; } = "رستورانیار";

        [MaxLength(500, ErrorMessage = "آدرس تصویر نباید بیشتر از 500 کاراکتر باشد")]
        [Display(Name = "آدرس تصویر شاخص")]
        public string? FeaturedImageUrl { get; set; }

        [Display(Name = "آپلود تصویر شاخص")]
        public IFormFile? FeaturedImage { get; set; }
    }
}
