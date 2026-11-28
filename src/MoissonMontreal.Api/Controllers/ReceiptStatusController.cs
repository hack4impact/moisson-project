using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoissonMontreal.Api.Receipts;
using MoissonMontreal.Core.Authorization;
using MoissonMontreal.Core.Dtos;
using MoissonMontreal.Core.Entities;
using MoissonMontreal.Core.Receipts;

namespace MoissonMontreal.Api.Controllers;

[ApiController]
[Route("api/receipts/{receiptId:guid}")]
public class ReceiptStatusController(ReceiptStatusService statuses) : ControllerBase
{
    [HttpPost("submit")]
    [Authorize(Policy = Permissions.ManageReceipts)]
    public Task<ActionResult<ReceiptStatusChangeDto>> Submit(Guid receiptId, CancellationToken cancellationToken) =>
        ApplyAsync(receiptId, ReceiptAction.Submit, null, cancellationToken);

    [HttpPost("approve-level-1")]
    [Authorize(Policy = Permissions.ApproveReceiptsLevel1)]
    public Task<ActionResult<ReceiptStatusChangeDto>> ApproveLevel1(Guid receiptId, CancellationToken cancellationToken) =>
        ApplyAsync(receiptId, ReceiptAction.ApproveLevel1, null, cancellationToken);

    [HttpPost("refuse-level-1")]
    [Authorize(Policy = Permissions.ApproveReceiptsLevel1)]
    public Task<ActionResult<ReceiptStatusChangeDto>> RefuseLevel1(
        Guid receiptId, ReceiptReasonRequest request, CancellationToken cancellationToken) =>
        ApplyAsync(receiptId, ReceiptAction.RefuseLevel1, request.Reason, cancellationToken);

    [HttpPost("approve-level-2")]
    [Authorize(Policy = Permissions.ApproveReceiptsLevel2)]
    public Task<ActionResult<ReceiptStatusChangeDto>> ApproveLevel2(Guid receiptId, CancellationToken cancellationToken) =>
        ApplyAsync(receiptId, ReceiptAction.ApproveLevel2, null, cancellationToken);

    [HttpPost("refuse-level-2")]
    [Authorize(Policy = Permissions.ApproveReceiptsLevel2)]
    public Task<ActionResult<ReceiptStatusChangeDto>> RefuseLevel2(
        Guid receiptId, ReceiptReasonRequest request, CancellationToken cancellationToken) =>
        ApplyAsync(receiptId, ReceiptAction.RefuseLevel2, request.Reason, cancellationToken);

    [HttpPost("issue")]
    [Authorize(Policy = Permissions.ManageReceipts)]
    public Task<ActionResult<ReceiptStatusChangeDto>> Issue(Guid receiptId, CancellationToken cancellationToken) =>
        ApplyAsync(receiptId, ReceiptAction.Issue, null, cancellationToken);

    [HttpPost("cancel")]
    [Authorize(Policy = Permissions.ManageReceipts)]
    public Task<ActionResult<ReceiptStatusChangeDto>> Cancel(
        Guid receiptId, ReceiptReasonRequest request, CancellationToken cancellationToken) =>
        ApplyAsync(receiptId, ReceiptAction.Cancel, request.Reason, cancellationToken);

    private async Task<ActionResult<ReceiptStatusChangeDto>> ApplyAsync(
        Guid receiptId, ReceiptAction action, string? reason, CancellationToken cancellationToken)
    {
        try
        {
            return ToDto(await statuses.ApplyAsync(receiptId, action, reason, User, cancellationToken));
        }
        catch (ReceiptNotFoundException e)
        {
            return NotFound(CreateProblem(StatusCodes.Status404NotFound, "Receipt not found", e.Message));
        }
        catch (InvalidReceiptTransitionException e)
        {
            var problem = CreateProblem(StatusCodes.Status409Conflict, "Status change not allowed", e.Message);
            problem.Extensions["currentStatus"] = e.Status;
            problem.Extensions["action"] = e.Action;
            problem.Extensions["allowedActions"] = e.AllowedActions;
            return Conflict(problem);
        }
        catch (InvalidReceiptReasonException e)
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [nameof(ReceiptReasonRequest.Reason)] = [e.Message],
            }));
        }
        catch (SubmitterCannotApproveException e)
        {
            return StatusCode(StatusCodes.Status403Forbidden, CreateProblem(StatusCodes.Status403Forbidden, "Cannot approve your own receipt", e.Message));
        }
    }

    private ProblemDetails CreateProblem(int status, string title, string detail) =>
        ProblemDetailsFactory.CreateProblemDetails(HttpContext, status, title: title, detail: detail);

    private static ReceiptStatusChangeDto ToDto(AppliedReceiptAction applied) => ToDto(applied.Change, applied.ReceiptNumber);

    private static ReceiptStatusChangeDto ToDto(ReceiptStatusChange change, string? receiptNumber) => new(
        change.ReceiptId,
        receiptNumber,
        change.FromStatus,
        change.ToStatus,
        change.ChangedBy.DisplayName,
        change.ChangedAt,
        change.Reason,
        ReceiptStateMachine.AllowedActions(change.ToStatus));
}
