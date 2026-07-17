# Mobile Auth Migration Guide

This document describes how to migrate the Resturanyar mobile app from the legacy V1 login + phone-only token flow to the secure V2 authentication endpoints.

## What Changed

| Legacy (insecure) | Secure replacement |
|-------------------|-------------------|
| `POST /api/UserApi/owner_login` + `POST /api/v2/UserApi/generate-token` | `POST /api/v2/UserApi/login/password` |
| `POST /api/UserApi/otpverify` + `POST /api/v2/UserApi/generate-token` | `POST /api/v2/UserApi/login/otp` |
| `POST /api/v2/UserApi/generate-token` (phone only) | **Removed** — returns HTTP 410 Gone |
| `POST /api/v2/UserApi/refresh` with `{ phoneNumber, refreshToken }` | `POST /api/v2/UserApi/refresh` with `{ refreshToken }` only |

V2 endpoints do **not** require the V1 static Bearer token.

## Password Login

**Request**

```
POST /api/v2/UserApi/login/password
Content-Type: application/json

{
  "phoneNumber": "09123456789",
  "password": "your-password"
}
```

**Success response (200)**

```json
{
  "success": true,
  "token": "<jwt>",
  "refreshToken": "<refresh>",
  "expiresAt": "2026-07-18T06:00:00Z",
  "redirectUrl": "/Home/ChooseRestaurant"
}
```

Store `token` and `refreshToken` in the app's secure storage (equivalent to web `localStorage`).

Do **not** send `X-Client: web` from mobile — that header is for web cookie sign-in only.

## OTP Login

**Step 1 — Request OTP** (unchanged)

```
POST /api/UserApi/otprequest
{ "phoneNumber": "09123456789" }
```

**Step 2 — Verify OTP and receive tokens**

```
POST /api/v2/UserApi/login/otp
{
  "phoneNumber": "09123456789",
  "code": "1234"
}
```

If the owner exists, response includes `token` and `refreshToken`.

If registration is required:

```json
{
  "success": false,
  "needsRegistration": true,
  "phoneNumber": "09123456789",
  "registrationToken": "<opaque-token>",
  "message": "..."
}
```

## Registration After OTP

```
POST /api/v2/UserApi/register
{
  "phoneNumber": "09123456789",
  "name": "Owner Name",
  "password": "secret123",
  "registrationToken": "<from login/otp response>"
}
```

`registrationToken` expires after 10 minutes and is single-use.

## Token Refresh

```
POST /api/v2/UserApi/refresh
{
  "refreshToken": "<stored refresh token>"
}
```

## Logout

```
POST /api/v2/UserApi/logout
Authorization: Bearer <access token>
Content-Type: application/json

"<refresh token string>"
```

## Using JWT on V2 API Calls

```
Authorization: Bearer <access token>
```

All `/api/v2/UserApi/*` business endpoints require a valid JWT.

## Migration Checklist

1. Replace `owner_login` + `generate-token` with `login/password`.
2. Replace `otpverify` + `generate-token` with `login/otp`.
3. Update refresh calls to send only `refreshToken`.
4. Remove any client code calling `generate-token`.
5. Store JWT + refresh token after login; attach JWT to V2 requests.

## Web vs Mobile

| Client | Cookie session | JWT storage |
|--------|----------------|-------------|
| Web manager login | Yes (`X-Client: web`) | `localStorage` |
| Mobile app | No | App secure storage |

Both clients use the same V2 login endpoints; only web sends `X-Client: web` and `credentials: include`.
