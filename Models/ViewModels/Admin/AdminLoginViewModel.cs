using System.ComponentModel.DataAnnotations;
namespace resturanyar.Models.ViewModels.Admin
{
    

    
        public class AdminLoginViewModel
        {
            [Required(ErrorMessage = "نام کاربری اجباری است")]
            [Display(Name = "نام کاربری")]
            public string Username { get; set; }

            [Required(ErrorMessage = "رمز عبور اجباری است")]
            [DataType(DataType.Password)]
            [Display(Name = "رمز عبور")]
            public string Password { get; set; }

            [Display(Name = "مرا به خاطر بسپار")]
            public bool RememberMe { get; set; }
        }
    public class MonthlyStatViewModel
    {
        public DateTime MonthDate { get; set; }  // تاریخ میلادی ماه
        public string Label { get; set; }         // لیبل نمایشی (مثلاً "فروردین 1402")
        public decimal Revenue { get; set; }      // درآمد ماه
    }
}

