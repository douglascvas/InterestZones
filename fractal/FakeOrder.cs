using System;
using InterestZones.Shared;

namespace cAlgo;

public class FakeOrder : IOrderManager
{
    public bool IsBuy { get; set; }
    public bool Closed { get; set; }
    public bool Open { get; set; }
    public int MaxPriceIndex { get; set; }
    public double EntryPrice { get; set; }
    public double StopLoss { get; set; }
    public double MaxUnrealizedProfit { get; set; }
    public double TotalProfit { get; set; }
    public int TotalProfitInt { get; set; }
    public double BreakEvenOnProfit { get; set; }
    public double MaxRR { get; set; }

    // IOrderManager implementation

    public void ProcessPrice(double price, int index)
    {
        if(Closed) return;
        var isSell = !IsBuy;

        Open = !Closed && (Open || (IsBuy && price <= EntryPrice) || (isSell && price >= EntryPrice));
        if(!Open) return;
        
        double risk = Math.Abs(EntryPrice - StopLoss);
        var currentRr = Math.Abs(price - EntryPrice) / risk;
        if ((IsBuy && price < EntryPrice) || (isSell && price > EntryPrice))
            currentRr = -currentRr;
        
        if (currentRr > MaxUnrealizedProfit)
        {
            MaxUnrealizedProfit = currentRr;
            MaxPriceIndex = index;
        }
        
        if ((IsBuy && price <= StopLoss) || (isSell && price >= StopLoss))
        {
            if (MaxUnrealizedProfit > BreakEvenOnProfit)
            {
                TotalProfit = Math.Max(0, MaxUnrealizedProfit);
                TotalProfitInt = (int)Math.Floor(Math.Max(0, MaxUnrealizedProfit));
            }
            else
            {
                TotalProfit = -1;
                TotalProfitInt = -1;
            }

            Closed = true;
            Open = false;
        }

    }
}