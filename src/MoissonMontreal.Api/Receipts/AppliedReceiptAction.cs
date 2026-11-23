using MoissonMontreal.Core.Entities;

namespace MoissonMontreal.Api.Receipts;

public sealed record AppliedReceiptAction(ReceiptStatusChange Change, string? ReceiptNumber);
