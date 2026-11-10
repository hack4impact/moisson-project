using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoissonMontreal.Core.Authorization;

namespace MoissonMontreal.Tests.Api;

// Test-only endpoint protected by a permission: the real API has no endpoint that requires one yet.
[ApiController]
[Route("test/permissions")]
public class PermissionTestController : ControllerBase
{
    [HttpGet("approve-receipts-level-1")]
    [Authorize(Policy = Permissions.ApproveReceiptsLevel1)]
    public IActionResult ApproveReceiptsLevel1() => Ok();
}
