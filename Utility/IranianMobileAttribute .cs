using System.ComponentModel.DataAnnotations;

namespace resturanyar.Utility
{
    public class IranianMobileAttribute : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
                return new ValidationResult("شماره تلفن الزامی است.");

            string phone = value.ToString();
            if (!System.Text.RegularExpressions.Regex.IsMatch(phone, @"^09\d{9}$"))
                return new ValidationResult("شماره تلفن باید با 09 شروع و شامل ۱۱ رقم باشد.");

            return ValidationResult.Success;
        }
    }
}
