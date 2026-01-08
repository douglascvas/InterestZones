using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using InterestZones;
using InterestZones.Shared;
using TradingPlatform.BusinessLayer;
using IOrder = InterestZones.Shared.IOrder;

namespace InterestZonesBot
{
    /// <summary>
    /// InterestZonesBot - Automated trading strategy based on Break of Structure and interest zones
    /// Places real limit orders at detected zones with automatic break-even stop loss management
    /// </summary>
    public class InterestZonesBot : Strategy, Bot, OrderCreator
    {
        #region Parameters

        [InputParameter("Symbol", 1)]
        public Symbol symbol;

        [InputParameter("Account", 1)]
        public Account account;
        
        [InputParameter("Order Quantity", 1, 0.01, 1000, 0.01)]
        public double OrderQuantity = 1.0;

        [InputParameter("Max Open Orders", 1, 1, 10, 1)]
        public int MaxOpenOrders = 3;

        [InputParameter("History Period", 1)]
        public Period HistoryPeriod = Period.MIN15;

        [InputParameter("Lookback Bars", 1, 10, 5000, 10)]
        public int LookbackBars = 500;

        [InputParameter("Pivot Period", 1, 1, 100, 1, 0)]
        public int PivotPeriod = 10;

        [InputParameter("Rectangle Height Mode", 2, variants: new object[]
        {
            "Auto (Wick or Average)", RectHeightMode.Auto,
            "Average Only", RectHeightMode.AverageOnly
        })]
        public RectHeightMode RectangleHeightMode = RectHeightMode.Auto;

        [InputParameter("Move to break even on RR", 3, 0, 10, 1, 0)]
        public int MinRR = 1;

        [InputParameter("Max RR Value", 4, 1, 50, 1)]
        public double MaxRRValue = 5;

        [InputParameter("Period for Average Bar Size calculation", 5, 1, 50, 1, 0)]
        public int BarSizeAveragePeriod = 10;

        [InputParameter("Average Bar Size multiplier", 6, 0.1, 50, 0.1)]
        public double BarSizeMultiplier = 0.8;

        [InputParameter("Pivot extra room", 6, 0, 5000, 1)]
        public int PivotExtraRoom = 0;

        // STYLES

        [InputParameter("BOS Line Color", 60)] public Color BOSLineColor = Color.Yellow;

        [InputParameter("BOS Line Width", 7, 1, 5, 1, 0)]
        public int BOSLineWidth = 1;

        [InputParameter("BOS Line Style", 8, variants: new object[]
        {
            "Solid", LineStyleType.Solid,
            "Dash", LineStyleType.Dash,
            "Dot", LineStyleType.Dot
        })]
        public LineStyleType BOSLineStyle = LineStyleType.Solid;

        [InputParameter("Fractal Line Color", 9)]
        public Color FractalLineColor = Color.White;

        [InputParameter("Fractal Line Width", 10, 1, 5, 1, 0)]
        public int FractalLineWidth = 1;

        [InputParameter("Fractal Line Style", 11, variants: new object[]
        {
            "Solid", LineStyleType.Solid,
            "Dash", LineStyleType.Dash,
            "Dot", LineStyleType.Dot
        })]
        public LineStyleType FractalLineStyle = LineStyleType.Dash;

        [InputParameter("InterestArea Border Color", 12)]
        public Color RectBorderColor = Color.Cyan;

        [InputParameter("InterestArea Fill Color", 13)]
        public Color RectFillColor = Color.FromArgb(50, 0, 255, 255);

        [InputParameter("Rectangle Border Width", 14, 1, 5, 1, 0)]
        public int RectBorderWidth = 1;

        [InputParameter("RR Circle Color", 15)]
        public Color RRCircleColor = Color.White;

        [InputParameter("RR Text Color", 16)] public Color RRTextColor = Color.Black;

        #endregion

        #region Fields

        private HistoricalData historicalData;
        InterestZoneRenderingIndicator renderer;
        InterestsZoneManager interestsZoneManager;

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

            renderer = new InterestZoneRenderingIndicator();
            

            // Load initial historical data
            LoadHistoricalData();

            Log($"InterestZonesBot started on {symbol.Name} with period {HistoryPeriod}", StrategyLoggingLevel.Trading);
            
            interestsZoneManager.PivotPeriod = PivotPeriod;
            interestsZoneManager.RectangleHeightMode = RectangleHeightMode;
            interestsZoneManager.MinRR = MinRR;
            interestsZoneManager.MaxRRValue = MaxRRValue;
            interestsZoneManager.BarSizeAveragePeriod = BarSizeAveragePeriod;
            interestsZoneManager.BarSizeMultiplier = BarSizeMultiplier;
            interestsZoneManager.PivotExtraRoom = PivotExtraRoom;
            interestsZoneManager.BOSLineColor = BOSLineColor;
            interestsZoneManager.BOSLineWidth = BOSLineWidth;
            interestsZoneManager.BOSLineStyle = BOSLineStyle;
            interestsZoneManager.FractalLineColor = FractalLineColor;
            interestsZoneManager.FractalLineWidth = FractalLineWidth;
            interestsZoneManager.FractalLineStyle = FractalLineStyle;
            interestsZoneManager.RectBorderColor = RectBorderColor;
            interestsZoneManager.RectFillColor = RectFillColor;
            interestsZoneManager.RectBorderWidth = RectBorderWidth;
            interestsZoneManager.RRCircleColor = RRCircleColor;
            interestsZoneManager.RRTextColor = RRTextColor;
            interestsZoneManager.OnInit(HistoryPeriod, this);
            
            InterestZoneRenderingIndicator interestZoneRenderingIndicator = new InterestZoneRenderingIndicator();
            interestZoneRenderingIndicator.manager = interestsZoneManager;
            
            // Core.Instance.Indicators.BuiltIn.CreateIndicator()
            // AddIndicator(interestZoneRenderingIndicator);
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
        
        private void SymbolOnNewQuote(Symbol symbol, Quote quote)
        {
            var bid = quote.Bid;
            interestsZoneManager.HandleBid(bid, symbol, interestsZoneManager.bars.Count, interestsZoneManager.bars[interestsZoneManager.bars.Count - 1].OpenTime);
        }

        protected override void OnRemove()
        {
            this.symbol = null;
            this.account = null;
            interestsZoneManager.Cleanup();
        }

        private void LoadHistoricalData()
        {

            int startIndex = Math.Max(0, historicalData.Count - LookbackBars);

            for (int i = startIndex; i < historicalData.Count; i++)
            {
                Bar bar = GetBar(i);
                interestsZoneManager.bars.Add(bar);

                if (interestsZoneManager.bars.Count > PivotPeriod * 2 + 1)
                {
                    interestsZoneManager.fractalService.processIndex(interestsZoneManager.bars.Count - 2);
                }
            }

            // Log($"Loaded {bars.Count} historical bars", StrategyLoggingLevel.Trading);
        }

        private void OnNewHistoryItem(object sender, HistoryEventArgs e)
        {
            Bar bar = GetBar(historicalData.Count - 1);
            interestsZoneManager.bars.Add(bar);
            interestsZoneManager.OnUpdate(UpdateReason.HistoricalBar,interestsZoneManager.bars.Count, symbol, 
                historicalData[historicalData.Count - 1].TimeLeft, GetBar(interestsZoneManager.bars.Count - 1));
        }

        private void OnHistoryItemUpdated(object sender, HistoryEventArgs e)
        {
            if (interestsZoneManager.bars.Count > 0)
            {
                interestsZoneManager.bars[interestsZoneManager.bars.Count - 1] = GetBar(historicalData.Count - 1);
            }
        }

        public IOrder CreateOrder(Fractal fractal, Symbol symbol, double rectangleHigh)
        {
            var order = new RealOrder(
                symbol,
                account,
                isBuy: fractal.low,
                entryPrice: fractal.high ? fractal.value - rectangleHigh : fractal.value + rectangleHigh,
                stopLoss: fractal.value + symbol.TickSize * PivotExtraRoom * (fractal.high ? 1 : -1),
                breakEvenOnProfit: MinRR,
                maxRR: MaxRRValue,
                log: (msg) => Log(msg, StrategyLoggingLevel.Trading)
            );

            // Place the limit order
            order.PlaceLimitOrder(OrderQuantity);
            return order;
        }

        public Bar GetBar(int index)
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
