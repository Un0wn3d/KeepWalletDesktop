namespace KeepWalletDesktop.Models;

public class Transaction
{
    public int ID { get; set; }
    public string Type { get; set; } = string.Empty;
    public double Cost { get; set; }
}
