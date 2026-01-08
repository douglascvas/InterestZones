using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo;
using InterestZones.Shared;
using TradingPlatform.BusinessLayer;

namespace InterestZonesBot
{
    /// <summary>
    /// InterestZonesBot - Automated trading strategy based on Break of Structure and interest zones
    /// Places real limit orders at detected zones with automatic break-even stop loss management
    /// </summary>
    public class InterestZonesBot : Strategy
    {
        #region Parameters

        [InputParameter("Symbol", 10)]
        public Symbol symbol;

        [InputParameter("Account", 20)]
        public Account account;

        [InputParameter("Pivot Period", 30, 1, 100, 1, 0)]
        public int PivotPeriod = 10;

        [InputParameter("Rectangle Height Mode", 40, variants: new object[]
        {
            "Auto (Wick or Average)", RectHeightMode.Auto,
            "Average Only", RectHeightMode.AverageOnly
        })]
        public RectHeightMode RectangleHeightMode = RectHeightMode.Auto;

        [InputParameter("Move to break even on RR", 50, 0, 10, 1, 0)]
        public int MinRR = 1;

        [InputParameter("Max RR Value", 60, 1, 50, 1)]
        public double MaxRRValue = 5;

        [InputParameter("Period for Average Bar Size calculation", 70, 1, 50, 1, 0)]
        public int BarSizeAveragePeriod = 10;

        [InputParameter("Average Bar Size multiplier", 80, 0.1, 50, 0.1)]
        public double BarSizeMultiplier = 0.8;

        [InputParameter("Pivot extra room", 90, 0, 5000, 1)]
        public int PivotExtraRoom = 0;

        [InputParameter("Order Quantity", 100, 0.01, 1000, 0.01)]
        public double OrderQuantity = 1.0;

        [InputParameter("Max Open Orders", 110, 1, 10, 1)]
        public int MaxOpenOrders = 3;

        [InputParameter("History Period", 120)]
        public Period HistoryPeriod = Period.MIN15;

        [InputParameter("Lookback Bars", 130, 10, 5000, 10)]
        public int LookbackBars = 500;

        #endregion

        #region Fields

        private HistoricalData historicalData;
        private List<Bar> bars = new List<Bar>();
        private List<FractalArea> closedAreas = new List<FractalArea>();
        private List<FractalArea> openAreas = new List<FractalArea>();
        private List<Fractal> unbrokenFractals = new List<Fractal>();
        private FractalService fractalService;
        private double lastBid;
        private Fractal lastFractal;
        private int lastProcessedBar = -1;

        #endregion

        public override string[] MonitoringConnectionsIds => new string[] { this.symbol?.ConnectionId };

        public InterestZonesBot()
            : base()
        {
            Name = "InterestZonesBot";
            Description = "Automated trading bot based on Break of Structure and interest zones";
        }

        protected override void OnCreated()
        {
            // Initialization happens in OnRun
        }

        protected override void OnRun()
        {
            if (symbol == null || account == null || symbol.ConnectionId != account.ConnectionId)
            {
                Log("Incorrect input parameters... Symbol or Account are not specified or they have different connectionID.", StrategyLoggingLevel.Error);
                Stop();
                return;
            }

            this.symbol = Core.Instance.GetSymbol(this.symbol?.CreateInfo());

            if (this.symbol == null)
            {
                Log("Failed to get symbol", StrategyLoggingLevel.Error);
                Stop();
                return;
            }

            // Initialize historical data
            historicalData = this.symbol.GetHistory(HistoryPeriod, this.symbol.HistoryType, DateTime.UtcNow.AddDays(-90));

            if (historicalData == null)
            {
                Log("Failed to load historical data", StrategyLoggingLevel.Error);
                Stop();
                return;
            }

            // Subscribe to events
            this.symbol.NewQuote += SymbolOnNewQuote;
            historicalData.NewHistoryItem += OnNewHistoryItem;
            historicalData.HistoryItemUpdated += OnHistoryItemUpdated;

            // Initialize fractal service
            fractalService = new FractalService(HistoryPeriod.ToString(), bars, new FractalOptions()
            {
                linkHighLow = true,
                period = PivotPeriod
            });

            fractalService.onFractal += HandleFractal;

            // Load initial historical data
            LoadHistoricalData();

            Log($"InterestZonesBot started on {symbol.Name} with period {HistoryPeriod}", StrategyLoggingLevel.Trading);
        }

        protected override void OnStop()
        {
            if (this.symbol != null)
            {
                this.symbol.NewQuote -= SymbolOnNewQuote;
            }

            if (historicalData != null)
            {
                historicalData.NewHistoryItem -= OnNewHistoryItem;
                historicalData.HistoryItemUpdated -= OnHistoryItemUpdated;
                historicalData.Dispose();
            }

            Log("InterestZonesBot stopped", StrategyLoggingLevel.Trading);
        }

        protected override void OnRemove()
        {
            this.symbol = null;
            this.account = null;
            bars.Clear();
            openAreas.Clear();
            closedAreas.Clear();
            unbrokenFractals.Clear();
        }

        protected override List<StrategyMetric> OnGetMetrics()
        {
            List<StrategyMetric> result = base.OnGetMetrics();

            result.Add("Open areas", openAreas.Count.ToString());
            result.Add("Closed areas", closedAreas.Count.ToString());
            result.Add("Active positions", openAreas.Count(a => a.order.Open).ToString());

            return result;
        }

        private void LoadHistoricalData()
        {
            bars.Clear();
            unbrokenFractals.Clear();

            int startIndex = Math.Max(0, historicalData.Count - LookbackBars);

            for (int i = startIndex; i < historicalData.Count; i++)
            {
                Bar bar = GetBarFromHistory(i);
                bars.Add(bar);

                if (bars.Count > PivotPeriod * 2 + 1)
                {
                    fractalService.processIndex(bars.Count - 2);
                }
            }

            lastProcessedBar = historicalData.Count - 1;
            lastBid = symbol.Bid;

            Log($"Loaded {bars.Count} historical bars", StrategyLoggingLevel.Trading);
        }

        private void OnNewHistoryItem(object sender, HistoryEventArgs e)
        {
            Bar bar = GetBarFromHistory(historicalData.Count - 1);
            bars.Add(bar);

            if (bars.Count > PivotPeriod * 2 + 1)
            {
                fractalService.processIndex(bars.Count - 2);

                // Process bar price movements
                var previousBar = bars[bars.Count - 2];
                if (previousBar.bullish)
                {
                    if (bar.bullish)
                    {
                        HandleBid(bar.Low);
                        HandleBid(bar.High);
                    }
                    else
                    {
                        HandleBid(bar.High);
                        HandleBid(bar.Low);
                    }
                }
                else
                {
                    if (bar.bullish)
                    {
                        HandleBid(bar.Low);
                        HandleBid(bar.High);
                    }
                    else
                    {
                        HandleBid(bar.High);
                        HandleBid(bar.Low);
                    }
                }
            }

            lastProcessedBar = historicalData.Count - 1;
        }

        private void OnHistoryItemUpdated(object sender, HistoryEventArgs e)
        {
            if (bars.Count > 0)
            {
                bars[bars.Count - 1] = GetBarFromHistory(historicalData.Count - 1);
            }
        }

        private void SymbolOnNewQuote(Symbol symbol, Quote quote)
        {
            var bid = quote.Bid;
            HandleBid(bid);
            lastBid = bid;

            // Update all open orders
            foreach (var area in openAreas.ToList())
            {
                area.order.ProcessPrice(bid, bars.Count - 1);
            }
        }

        private void HandleFractal(FractalEvent e)
        {
            lastFractal = e.fractal;
            var last = unbrokenFractals.LastOrDefault();
            if (last == null || last.getId() != e.fractal.getId())
                unbrokenFractals.Add(e.fractal);
            else if (last.getId() == e.fractal.getId())
                unbrokenFractals[^1] = e.fractal;
        }

        private void HandleBid(double bid)
        {
            CheckForStructureBreak(bid);
            HandleOpenAreas(bid);
        }

        private void HandleOpenAreas(double bid)
        {
            List<FractalArea> toBeRemoved = null;
            foreach (var area in openAreas)
            {
                area.order.ProcessPrice(bid, bars.Count - 1);
                if (area.order.Closed)
                {
                    if (toBeRemoved == null) toBeRemoved = new List<FractalArea>();
                    toBeRemoved.Add(area);
                }
            }

            if (toBeRemoved != null)
            {
                foreach (var area in toBeRemoved)
                {
                    openAreas.Remove(area);
                    closedAreas.Add(area);
                }
            }
        }

        private void CheckForStructureBreak(double bid)
        {
            List<Fractal> toBeRemoved = null;
            for (int f = 0; f < unbrokenFractals.Count - 1; f++)
            {
                Fractal fractalN = unbrokenFractals[f];

                if ((fractalN.high && bid > fractalN.value) || (fractalN.low && bid < fractalN.value))
                {
                    if (toBeRemoved == null)
                        toBeRemoved = new List<Fractal>();
                    toBeRemoved.Add(fractalN);

                    // Structure broken - create interest zone
                    var fractal = fractalN.getNext(true);
                    if (fractal == null) return;

                    var averageBarSize = GetAverageBarSize(fractal) * BarSizeMultiplier;
                    var fractalBar = bars[fractal.index];
                    var wickSize = fractal.high
                        ? fractalBar.upperWickTop - fractalBar.upperWickBottom
                        : fractalBar.lowerWickTop - fractalBar.lowerWickBottom;
                    var rectangleHigh = RectangleHeightMode == RectHeightMode.AverageOnly
                        ? averageBarSize
                        : Math.Min(wickSize, averageBarSize);
                    if (RectangleHeightMode == RectHeightMode.Auto && rectangleHigh < 0.4 * averageBarSize)
                    {
                        rectangleHigh = averageBarSize;
                    }

                    if (!IsFractalValid(fractal, 0))
                        continue;

                    // Check if we can open more orders
                    int activeOrders = openAreas.Count(a => a.order.Open || !a.order.Closed);
                    if (activeOrders >= MaxOpenOrders)
                    {
                        Log($"Max open orders ({MaxOpenOrders}) reached, skipping zone", StrategyLoggingLevel.Trading);
                        continue;
                    }

                    double entryPrice = fractal.high ? fractal.value - rectangleHigh : fractal.value + rectangleHigh;
                    double stopLoss = fractal.value + symbol.TickSize * PivotExtraRoom * (fractal.high ? 1 : -1);

                    // Create real order manager
                    var orderManager = new RealOrderManager(
                        symbol,
                        account,
                        isBuy: fractal.low,
                        entryPrice: entryPrice,
                        stopLoss: stopLoss,
                        breakEvenOnProfit: MinRR,
                        maxRR: MaxRRValue,
                        log: (msg) => Log(msg, StrategyLoggingLevel.Trading)
                    );

                    // Place the limit order
                    orderManager.PlaceLimitOrder(OrderQuantity);

                    var area = new FractalArea
                    {
                        fractal = fractal,
                        order = orderManager,
                        structureBroken = true,
                        rectangleTop = fractal.high
                            ? fractal.value + symbol.TickSize * PivotExtraRoom
                            : fractal.value + rectangleHigh,
                        rectangleBottom = fractal.low
                            ? fractal.value - symbol.TickSize * PivotExtraRoom
                            : fractal.value - rectangleHigh,
                        rectangleStart = fractal.dateTime,
                        rectangleStartIndex = fractal.index,
                        rectangleEnd = DateTime.UtcNow,
                        rectangleEndIndex = bars.Count - 1
                    };

                    openAreas.Add(area);

                    Log($"Interest zone created: {(fractal.low ? "BUY" : "SELL")} @ {entryPrice:F5}, SL: {stopLoss:F5}",
                        StrategyLoggingLevel.Trading);
                }
            }

            if (toBeRemoved != null)
                foreach (var fractal in toBeRemoved)
                    unbrokenFractals.Remove(fractal);
        }

        private bool IsFractalValid(Fractal fractalN, double height)
        {
            for (int i = fractalN.index + 1; i < bars.Count - 1; i++)
            {
                var bar = bars[i];
                if ((fractalN.high && bar.High > fractalN.value - height) ||
                    (fractalN.low && bar.Low < fractalN.value + height))
                    return false;
            }

            return true;
        }

        private double GetAverageBarSize(Fractal fractal)
        {
            var n = Math.Max(BarSizeAveragePeriod, 3);
            var half = n / 2;
            var start = Math.Max(0, fractal.index + half - n);
            var max = Math.Min(bars.Count, start + n);
            var barSizeSum = 0.0;
            for (int i = start; i < max; i++)
            {
                barSizeSum += bars[i].High - bars[i].Low;
            }

            return barSizeSum / n;
        }

        private Bar GetBarFromHistory(int index)
        {
            return new Bar()
            {
                Open = historicalData.Open(index),
                Close = historicalData.Close(index),
                High = historicalData.High(index),
                Low = historicalData.Low(index),
                Volume = historicalData.Volume(index),
                OpenTime = historicalData[index, SeekOriginHistory.Begin].TimeLeft,
            };
        }
    }
}
