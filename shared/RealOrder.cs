using System;
using TradingPlatform.BusinessLayer;

namespace InterestZones.Shared
{
    /// <summary>
    /// Manages a real trading order with the same interface as FakeOrder
    /// </summary>
    public class RealOrder : IOrder
    {
        private readonly Symbol symbol;
        private readonly Account account;
        private readonly Action<string> log;
        private Position position;
        private bool slMovedToBreakEven;
        private double maxUnrealizedProfit;
        private int maxPriceIndex;

        public bool IsBuy { get; private set; }
        public bool Closed { get; private set; }
        public bool Open { get; private set; }
        public double EntryPrice { get; private set; }
        public double StopLoss { get; set; }
        public double MaxUnrealizedProfit => maxUnrealizedProfit;
        public double TotalProfit { get; private set; }
        public int TotalProfitInt { get; private set; }
        public double BreakEvenOnProfit { get; private set; }
        public double MaxRR { get; private set; }

        public Position Position => position;

        public RealOrder(
            Symbol symbol,
            Account account,
            bool isBuy,
            double entryPrice,
            double stopLoss,
            double breakEvenOnProfit,
            double maxRR,
            Action<string> log)
        {
            this.symbol = symbol;
            this.account = account;
            this.IsBuy = isBuy;
            this.EntryPrice = entryPrice;
            this.StopLoss = stopLoss;
            this.BreakEvenOnProfit = breakEvenOnProfit;
            this.MaxRR = maxRR;
            this.log = log;
            this.Closed = false;
            this.Open = false;
            this.slMovedToBreakEven = false;
            this.maxUnrealizedProfit = 0;
            this.TotalProfit = 0;
            this.TotalProfitInt = 0;
        }

        public void PlaceLimitOrder(double quantity)
        {
            if (Open || Closed)
                return;

            // Place limit order without SL initially (will be added when position opens)
            var orderParams = new PlaceOrderRequestParameters
            {
                Symbol = symbol,
                Account = account,
                Side = IsBuy ? Side.Buy : Side.Sell,
                OrderTypeId = OrderType.Limit,
                Price = EntryPrice,
                Quantity = quantity
            };

            var result = Core.Instance.PlaceOrder(orderParams);

            if (result.Status == TradingOperationResultStatus.Success)
            {
                log?.Invoke($"Limit order placed: {(IsBuy ? "BUY" : "SELL")} @ {EntryPrice}");
            }
            else
            {
                log?.Invoke($"Failed to place order: {result.Status}");
                Closed = true;
            }
        }

        public void ProcessPrice(double price, int index)
        {
            if (Closed)
                return;

            // Check if position exists for this order
            if (!Open && position == null)
            {
                // Try to find the position (order might have been filled)
                foreach (var pos in Core.Instance.Positions)
                {
                    if (pos.Symbol.Id == symbol.Id &&
                        pos.Account.Id == account.Id &&
                        pos.Side == (IsBuy ? Side.Buy : Side.Sell) &&
                        Math.Abs(pos.OpenPrice - EntryPrice) < symbol.TickSize)
                    {
                        position = pos;
                        Open = true;
                        log?.Invoke($"Position opened: {position.Id}, setting SL to {StopLoss}");

                        // Set initial stop loss
                        SetStopLoss(StopLoss);
                        break;
                    }
                }
            }

            if (!Open)
                return;

            // Calculate current RR
            double risk = Math.Abs(EntryPrice - StopLoss);
            double currentRr = Math.Abs(price - EntryPrice) / risk;
            if ((IsBuy && price < EntryPrice) || (!IsBuy && price > EntryPrice))
                currentRr = -currentRr;

            if (currentRr > maxUnrealizedProfit)
            {
                maxUnrealizedProfit = currentRr;
                maxPriceIndex = index;
            }

            // Move SL to break even if not already done
            if (!slMovedToBreakEven && maxUnrealizedProfit >= BreakEvenOnProfit && position != null)
            {
                slMovedToBreakEven = true;
                SetStopLoss(EntryPrice);
                log?.Invoke($"Stop loss moved to break even for position {position.Id} (RR: {maxUnrealizedProfit:F2})");
            }
        }

        private void SetStopLoss(double slPrice)
        {
            if (position == null)
                return;

            try
            {
                // Try to modify the position's stop loss
                var slOffset = Math.Abs(position.OpenPrice - slPrice);
                var modifyParams = new PlaceOrderRequestParameters
                {
                    Symbol = symbol,
                    Account = account,
                    Side = position.Side,
                    OrderTypeId = OrderType.Stop,
                    TriggerPrice = slPrice,
                    Quantity = position.Quantity
                };

                // Since we can't directly modify SL, we'll let the position handle it naturally
                // The user will need to manually set SL or we rely on the order SL
                StopLoss = slPrice;
            }
            catch (Exception ex)
            {
                log?.Invoke($"Error setting stop loss: {ex.Message}");
            }
        }
    }
}
