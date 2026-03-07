namespace SkyWatch.Alpaca.Models;

public class AlpacaValueResponse<T> : AlpacaResponse
{
    public T? Value { get; set; }
}
