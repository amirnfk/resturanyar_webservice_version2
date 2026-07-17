using System.ComponentModel.DataAnnotations;
using resturanyar.Models.AdminMessage;

namespace resturanyar.Models.ViewModels.Admin
{
    public class SendRestaurantMessageViewModel
    {
        [Required(ErrorMessage = "عنوان پیام الزامی است.")]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "متن پیام الزامی است.")]
        public string Body { get; set; } = string.Empty;

        public AdminMessageType MessageType { get; set; } = AdminMessageType.Public;

        public int[] SelectedRestaurantIds { get; set; } = Array.Empty<int>();

        public List<RestaurantPickerItem> Restaurants { get; set; } = new();
    }

    public class RestaurantPickerItem
    {
        public int RestaurantId { get; set; }
        public string RestaurantName { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string OwnerPhone { get; set; } = string.Empty;
    }
}
