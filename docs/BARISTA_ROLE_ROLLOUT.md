# Barista (باریستا) — deploy & test notes

## Access matrix

- **Default:** `kitchen_management_permission = true`; order/payment off.
- **Optional (like chef):** owner may also grant order and/or payment.
- **Never:** `delivery_management_permission` (forced off server-side for role 6).
- **API:** kitchen-only barista (`IsBaristaOnlyStaff`) is limited to status 3→4→5 and blocked from create/pay/delivery. With order or payment JWT claims, those paths follow the same rules as other staff.

## Deploy

1. Backup the production database.
2. Run [`Scripts/AddBaristaRoleSupport.sql`](../Scripts/AddBaristaRoleSupport.sql) (idempotent `Roles` insert only).
3. Deploy the updated web/backend build.
4. Deploy updated Android APKs (owner + staff).
5. In ManageStaff (web) or Add Staff (Android), create a user with role **باریستا** (opt-in). Do not convert existing chefs.

Existing staff JWTs are unchanged. No new permission column. Kitchen notifications stay on `TargetRoleId = 3`.

## Manual test checklist

### Regression (roles 2–5)

- [ ] Waiter: create/edit orders unchanged
- [ ] Chef: kitchen 3→4→5 unchanged; mixed order/payment still editable
- [ ] Cashier: payment path unchanged
- [ ] Courier: assigned-only list and 5→6 / delivery-failed unchanged
- [ ] Owner ManageStaff: mixed perms for waiter/chef/cashier still work; پیک still locked

### Barista — kitchen only (default)

- [ ] Staff login → kitchen UI (statuses 3 and 4)
- [ ] 3→4 and 4→5 via status API succeed
- [ ] `createorder` returns 403
- [ ] `UpdateOrder` (full edit) returns 403
- [ ] Payment / assign-driver / delivery-failed return 403
- [ ] Web StaffLogin shows Android-only message (not cashier dashboard)

### Barista — hybrid

- [ ] ManageStaff / Android: order and payment toggles editable; delivery locked off
- [ ] Barista + order: `createorder` allowed
- [ ] Barista + payment: receipt issue / payment path allowed
- [ ] Delivery still forced false if payload tampers with `delivery_management_permission`

### Owner / web

- [ ] ManageStaff role باریستا defaults kitchen on; order/payment optional
- [ ] `role_id` 0 or 1 rejected on add/edit

## Rollback

Stop creating barista users. [`Scripts/UndoBaristaRoleSupport.sql`](../Scripts/UndoBaristaRoleSupport.sql) only after reassigning or deleting `Users.role_id = 6`.
