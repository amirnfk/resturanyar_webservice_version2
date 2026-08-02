# Dual-auth soak checklist (Phase 5)

V1 static-token APIs remain live for old APKs. New Android builds use:

- Owner: `/api/v2/UserApi` + `V2TokenStore`
- Staff: `/api/v2/StaffApi` + `StaffV2TokenStore`

## Before raising min version / retiring static token

1. Confirm `StaffRefreshTokens` table exists (Phase 0 SQL).
2. Deploy API with StaffApi + OwnerGaps endpoints.
3. Ship Android build that uses JWT for owner + staff.
4. Monitor for ≥ 2 weeks:
   - Staff/owner login failures
   - HTTP 401 rates and refresh failures
   - Order create/update/status errors
5. When majority of devices are on the new build, raise `UpdateConfig.ForceVersion` via existing checkversion, then disable `StaticTokenMiddleware` in a separate deploy.

## Do not

- Drop `RefreshTokens` or force-logout owners
- Require JWT on SignalR `OrderHub` until separately planned
- Remove V1 Subscription staff login until min-version gate is proven
