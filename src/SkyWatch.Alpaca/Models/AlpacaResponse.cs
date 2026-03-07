namespace SkyWatch.Alpaca.Models;

public class AlpacaResponse
{
    public int ErrorNumber { get; set; }

    public string ErrorMessage { get; set; } = string.Empty;

    public uint ClientTransactionID { get; set; }

    public uint ServerTransactionID { get; set; }
}
