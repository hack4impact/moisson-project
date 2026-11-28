using MoissonMontreal.Core.Receipts;

namespace MoissonMontreal.Api.Data.Configurations;

public static class ReceiptStatusColumn
{
    public const int MaxLength = 30;

    public static string IsValid(string column) =>
        $"{column} IN ({string.Join(", ", Enum.GetNames<ReceiptStatus>().Select(name => $"'{name}'"))})";
}
