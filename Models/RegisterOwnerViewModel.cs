using System.ComponentModel.DataAnnotations;

namespace resturanyar.Models
{
    public class RegisterOwnerViewModel
    {
        [Required(ErrorMessage = "نام را وارد کنید")]
        public string Name { get; set; }

        [Required(ErrorMessage = "شماره موبایل را وارد کنید")]
        [RegularExpression(@"^09\d{9}$", ErrorMessage = "شماره موبایل معتبر نیست")]
        public string Phone { get; set; }

        [Required(ErrorMessage = "رمز عبور را وارد کنید")]
        [DataType(DataType.Password)]
        public string Password { get; set; }
    }
}
