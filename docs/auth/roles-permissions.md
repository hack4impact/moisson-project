# Roles and permissions

Each employee who uses the app has one role, which matches their department. The role decides what the employee can see and do. This page lists the roles and what each one is allowed to do, as defined in the PRD.

## Who decides what

| Question | Who answers it |
| --- | --- |
| Who is this person? | Microsoft (Entra ID), when the employee signs in with their work account. See [entra-id.md](entra-id.md). |
| Can this person use the app, and with which role? | The app's admin, who adds the employee and gives them a role. See [users-roles.md](../database/users-roles.md). |
| Is this person allowed to do this action? | The API, on every request, by checking the role stored in the app. |

The check always happens on the server. Hiding a button in the desktop app is only a convenience: even if someone found a way around it, the API would still refuse the action.

## Departments and roles

| Department | Role in the app |
| --- | --- |
| Supply | Supply |
| Supply management | Supply Director |
| Finance | Finance |
| Administration of the app | Admin |

The department names above come from the PRD. The admin chooses the role that matches the employee's department when they add them to the app. Roles that IT might set in Microsoft 365 are not used.

## What each role can do

| Role | What it can do |
| --- | --- |
| Supply | Import donations from Business Central, link a donation to a donor, manage donors, prepare a receipt, issue it once approved, generate the PDF |
| Supply Director | Everything Supply can do, plus approve or refuse a receipt (first level) |
| Finance | Approve or refuse a receipt (second level), view the history, export the data |
| Admin | Everything above, plus manage accounts, the price grid and the quality percentages |

## Permissions table

| Permission | Supply | Supply Director | Finance | Admin |
| --- | :---: | :---: | :---: | :---: |
| Import donations and link them to donors | ✓ | ✓ | | ✓ |
| Manage donors | ✓ | ✓ | | ✓ |
| Prepare, issue and generate receipts | ✓ | ✓ | | ✓ |
| Approve or refuse a receipt (first level) | | ✓ | | ✓ |
| Approve or refuse a receipt (second level) | | | ✓ | ✓ |
| View the history | | | ✓ | ✓ |
| Export the data | | | ✓ | ✓ |
| Manage accounts | | | | ✓ |
| Manage the price grid | | | | ✓ |
| Manage the quality percentages | | | | ✓ |

## When access is refused

| Situation | What the API answers |
| --- | --- |
| The employee is not signed in, or their session expired | 401: the app asks them to sign in |
| The employee's role does not allow the action | 403: access refused |
| The employee signed in with Microsoft but was never added to the app | 403 for every protected action |
| The admin removed the employee's access | 403 for every protected action, even for a former Admin |

The first time an employee does an action that requires a permission, their Microsoft account is linked to the account the admin created with their email. After that, the app recognizes them by their Microsoft account, even if their email changes.

## For developers

### Protect an endpoint

Add the permission the action needs:

```csharp
[Authorize(Policy = Permissions.ApproveReceiptsLevel1)]
```

Use the constants in `Permissions`, never a role name. The roles allowed for each permission are decided in one place.

### Change who can do what

1. Update the table in `src/MoissonMontreal.Core/Authorization/Permissions.cs` (`AllowedRoles`).
2. Update the PRD table written in `tests/MoissonMontreal.Tests/Api/PermissionTests.cs` (`PrdPermissionsByRole`). It is kept separate on purpose, so a mistake in one of the two makes the tests fail.
3. Update the tables on this page.

### How it works

| Piece | Location | Role |
| --- | --- | --- |
| `RoleCodes` | `src/MoissonMontreal.Core/Authorization/` | The four role codes, the same as the `code` column of the `roles` table |
| `Permissions` | `src/MoissonMontreal.Core/Authorization/` | The permissions and the roles allowed for each one |
| `PermissionPolicies` | `src/MoissonMontreal.Api/Authorization/` | Creates one authorization policy per permission |
| `PermissionHandler` | `src/MoissonMontreal.Api/Authorization/` | Allows the request when the employee's role is in the list, otherwise the API answers 403 |
| `UserLookupService` | `src/MoissonMontreal.Api/Authorization/` | Finds the employee's active account in the `users` table and links it at the first permission check |

The role is read from the database only when an endpoint requires a permission. Endpoints such as `/health` and `api/me` do not read it.

The Microsoft account is linked by `UserLookupService`, called from `PermissionHandler`, the first time the employee calls an endpoint that requires a permission. Signing in or calling `/api/me` does not link the account, so `entra_object_id` can still be empty after a sign-in.

### Tests

`PermissionTests` checks every role against every permission, the 403 and 401 answers and the account link at the first permission check. These tests need Docker, since they use a PostgreSQL container.

## Open points

- With "Admin can do everything", the same Admin could approve a receipt at both levels. If two different people must approve, that rule belongs to the receipt status workflow, not to the roles.
