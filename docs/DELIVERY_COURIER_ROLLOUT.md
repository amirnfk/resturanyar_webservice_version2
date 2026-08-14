# Delivery courier (پیک) — deploy & test notes

## Deploy

1. **Backup** the production database.
2. Run [`Scripts/AddDeliveryCourierSupport.sql`](../Scripts/AddDeliveryCourierSupport.sql) on the DB (idempotent).
3. Deploy the updated **web/backend** build.
4. Deploy updated **Android** APKs (owner + staff).
5. In ManageStaff (web) or Add Staff (Android), create a user with role **پیک**.
6. Enable Delivery for the restaurant if not already on.
7. Create a Delivery order → assign پیک from manager order list → courier logs in via staff login.

## Manual test checklist

### Regression
- [ ] Dine-In create → kitchen 3→4→5 → waiter/cashier path still works
- [ ] Takeaway create/list unchanged
- [ ] Old staff APK ignoring `role5` / new DTO fields still logs in and lists orders

### Courier happy path
- [ ] پیک staff login → only sees **assigned** Delivery orders
- [ ] Cannot create order / pay / cancel via generic cancel
- [ ] At status 5: **تحویل شد** → status **6**
- [ ] Cashier continues 6→7→8→11

### Unsuccessful delivery
- [ ] At status 5: **تحویل ناموفق** → must enter reason
- [ ] Status stays **5**, assignment cleared, reason visible on web manager card
- [ ] Owner reassigns another پیک or cancels normally

### Owner / web
- [ ] ManageStaff: role پیک + delivery permission toggle
- [ ] Manager order card: assign / unassign پیک for Delivery
- [ ] Owner can still advance Delivery 5→6 without assigning a courier

## Rollback

Use [`Scripts/UndoDeliveryDriverSupport.sql`](../Scripts/UndoDeliveryDriverSupport.sql) only after removing any `role_id=5` users; or keep columns and disable UI by not creating پیک users.
