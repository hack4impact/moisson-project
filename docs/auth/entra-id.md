# Microsoft Entra ID setup

Employees sign in to the app with their Moisson Montréal Microsoft work account. Authentication is handled by Microsoft Entra ID in the Moisson Montréal tenant. What each person is allowed to do is decided by the app itself, through the role stored in the `users` table (see [users-roles.md](../database/users-roles.md)), not by Entra ID.

Two app registrations exist in the tenant:

| Registration | Purpose |
| --- | --- |
| MoissonMontreal.Api | Represents the central API. It defines the permission that the desktop app requests when it calls the API. |
| MoissonMontreal.Desktop | Represents the desktop app. Employees sign in through it, and it obtains the tokens sent to the API. |

None of these values are secrets. The desktop app is a public client and the API validates tokens with the public keys published by Entra ID, so no client secret or certificate is needed.

## Identifiers

| Item | Value |
| --- | --- |
| Tenant | Moisson Montreal (moissonmontreal.org) |
| Tenant ID | `e044b870-92ef-4827-b053-ed04a8fb5304` |
| API client ID | `c5f09751-5359-4c03-84ed-2547220dbd32` |
| API Application ID URI | `api://c5f09751-5359-4c03-84ed-2547220dbd32` |
| API scope | `api://c5f09751-5359-4c03-84ed-2547220dbd32/access_as_user` |
| Desktop client ID | `442b9269-21a8-4b2f-bd29-fa6b74698d10` |
| Desktop redirect URI | `http://localhost` |

## Values used in the code

### API (token validation)

| Setting | Value |
| --- | --- |
| Authority | `https://login.microsoftonline.com/e044b870-92ef-4827-b053-ed04a8fb5304/v2.0` |
| Expected issuer | `https://login.microsoftonline.com/e044b870-92ef-4827-b053-ed04a8fb5304/v2.0` |
| Expected audience | `c5f09751-5359-4c03-84ed-2547220dbd32` |
| Required scope | `access_as_user` (in the `scp` claim) |

The API issues version 2 access tokens, so the `aud` claim contains the API client ID, not the `api://` URI.

### Desktop app (sign-in with MSAL)

| Setting | Value |
| --- | --- |
| Authority | `https://login.microsoftonline.com/e044b870-92ef-4827-b053-ed04a8fb5304` |
| Client ID | `442b9269-21a8-4b2f-bd29-fa6b74698d10` |
| Redirect URI | `http://localhost` |
| Scope to request | `api://c5f09751-5359-4c03-84ed-2547220dbd32/access_as_user` |

## Claims available to the API

Each access token contains the following claims about the signed-in employee:

| Claim | Content | Use in the app |
| --- | --- | --- |
| `oid` | Identifier of the Microsoft account | Stored in `users.entra_object_id` when the account is linked |
| `email` | Work email (optional claim added to the access token) | Used to find the employee in the `users` table when the account is not linked yet |
| `preferred_username` | Sign-in name, usually the same as the email | Fallback when `email` is missing |
| `name` | Display name | Can prefill `users.display_name` |
| `tid` | Tenant ID | Confirms the token comes from the Moisson Montréal tenant |
| `scp` | Granted scopes | Must contain `access_as_user` |

## Registration settings

### MoissonMontreal.Api

| Setting | Value |
| --- | --- |
| Supported account types | Single tenant (Moisson Montreal only) |
| Redirect URI | None |
| Application ID URI | `api://c5f09751-5359-4c03-84ed-2547220dbd32` |
| Scope | `access_as_user`, consent by admins and users, enabled |
| Optional claims | `email` on the access token |
| Access token version | 2 (`requestedAccessTokenVersion` in the manifest) |
| Owner | h4i@moissonmontreal.org |

### MoissonMontreal.Desktop

| Setting | Value |
| --- | --- |
| Supported account types | Single tenant (Moisson Montreal only) |
| Platform | Public client/native (mobile and desktop) |
| Redirect URI | `http://localhost` |
| API permissions | Microsoft Graph `User.Read` (delegated), MoissonMontreal.Api `access_as_user` (delegated) |
| Owner | h4i@moissonmontreal.org |

## Remaining steps with the IT team

1. Review and approve the configuration described above.
2. Grant admin consent for the permissions of MoissonMontreal.Desktop, so that employees are not asked to consent on their first sign-in. This requires an administrator role.
3. Add an IT account as an owner of both registrations, so that they do not depend on a single account.
