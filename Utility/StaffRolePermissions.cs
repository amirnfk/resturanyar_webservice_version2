using Resturanyar.Data;

namespace resturanyar.Utility
{
    public static class StaffRolePermissions
    {
        public const int CourierRoleId = 5;
        public const int BaristaRoleId = 6;

        public static bool IsSettableStaffRole(int roleId) => roleId >= 2 && roleId <= 6;

        /// <summary>
        /// Courier is delivery-only. Barista and other settable roles (2–4, 6) may mix
        /// order/kitchen/payment but never delivery.
        /// </summary>
        public static void ApplyExclusiveLocks(User user)
        {
            if (user.role_id == CourierRoleId)
            {
                user.order_management_permission = false;
                user.kitchen_management_permission = false;
                user.payment_management_permission = false;
                user.delivery_management_permission = true;
                return;
            }

            user.delivery_management_permission = false;
        }
    }
}
