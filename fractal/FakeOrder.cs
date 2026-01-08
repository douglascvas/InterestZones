using System;

namespace cAlgo;

public class FakeOrder
{
    public bool isBuy { get; set; }
    public bool closed { get; set; }
    public bool open { get; set; }
    public int maxPriceIndex { get; set; }
    public double entryPrice { get; set; }
    public double stopLoss { get; set; }
    public double maxUnrealizedProfit { get; set; }
    public double totalProfit { get; set; }
    public int totalProfitInt { get; set; }
    public double breakEvenOnProfit { get; set; }
    public double maxRR { get; set; }

    public void ProcessPrice(double price, int index)
    {
        if(closed) return;
        var isSell = !isBuy;

        open = !closed && (open || (isBuy && price <= entryPrice) || (isSell && price >= entryPrice));
        if(!open) return;
        
        double risk = Math.Abs(entryPrice - stopLoss);
        var currentRr = Math.Abs(price - entryPrice) / risk;
        if ((isBuy && price < entryPrice) || (isSell && price > entryPrice))
            currentRr = -currentRr;
        
        if (currentRr > maxUnrealizedProfit)
        {
            maxUnrealizedProfit = currentRr;
            maxPriceIndex = index;
        }
        
        if ((isBuy && price <= stopLoss) || (isSell && price >= stopLoss))
        {
            if (maxUnrealizedProfit > breakEvenOnProfit)
            {
                totalProfit = Math.Max(0, maxUnrealizedProfit);
                totalProfitInt = (int)Math.Floor(Math.Max(0, maxUnrealizedProfit));
            }
            else
            {
                totalProfit = -1;
                totalProfitInt = -1;
            }

            closed = true;
            open = false;
        }

    }
}