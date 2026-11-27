using System.Text.Json.Serialization;

namespace MoissonMontreal.Core.Receipts;

[JsonConverter(typeof(JsonStringEnumConverter<ReceiptStatus>))]
public enum ReceiptStatus
{
    Draft,
    PendingSupplyApproval,
    PendingFinanceApproval,
    Approved,
    Issued,
    Cancelled,
}
