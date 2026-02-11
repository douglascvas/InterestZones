namespace InterestZones.Shared
{
    /// <summary>
    /// Interface for order management - can be implemented by FakeOrder or real order wrapper
    /// </summary>
    public interface IOrder
    {
        bool IsBuy { get; }
        bool Closed { get; }
        bool Open { get; }
        double EntryPrice { get; }
        double StopLoss { get; set; }
        double MaxUnrealizedProfit { get; }
        double TotalProfit { get; }
        int TotalProfitInt { get; }
        double BreakEvenOnProfit { get; }
        double MaxRR { get; }

        void ProcessPrice(Bar bar, int index);
    }
}
