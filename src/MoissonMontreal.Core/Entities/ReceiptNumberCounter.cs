namespace MoissonMontreal.Core.Entities;

public class ReceiptNumberCounter
{
    public string Series { get; set; } = string.Empty;

    public int LastNumber { get; set; }
}
