using System.Text.Json.Serialization;

namespace MoissonMontreal.Core.Receipts;

[JsonConverter(typeof(JsonStringEnumConverter<ReceiptAction>))]
public enum ReceiptAction
{
    Submit,
    ApproveLevel1,
    RefuseLevel1,
    ApproveLevel2,
    RefuseLevel2,
    Issue,
    Cancel,
}
