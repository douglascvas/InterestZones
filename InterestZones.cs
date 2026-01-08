// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using System.Collections.Generic;
using System.Drawing;
using TradingPlatform.BusinessLayer;
using IOrder = InterestZones.Shared.IOrder;

namespace InterestZones
{
    /// <summary>
    /// InterestZones indicator - Detects Break of Structure (BOS) and draws interest zones with RR tracking
    /// </summary>
    public class InterestZones : Indicator, Bot, OrderCreator
    {
        #region Parameters

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

        private List<BosEvent> bosEvents = new List<BosEvent>();
        private List<Bar> bars = new List<Bar>();
        private List<FractalArea> closedAreas = new List<FractalArea>();
        private List<FractalArea> openAreas = new List<FractalArea>();
        private List<Fractal> unbrokenFractals = new List<Fractal>();
        private Dictionary<int, Fractal> unbrokenFractalsMap = new Dictionary<int, Fractal>();
        private FractalService fractalService;

        private FractalArea lastArea;
        private double lastBid;
        private double lastCheckedHigh = double.MinValue;
        private double lastCheckedLow = double.MaxValue;
        private int bestRR;
        private int bestRRSum;
        private int currentRRSum;
        private int positiveAreasCount;
        Font tableFont;
        SolidBrush tableBgBrush;
        SolidBrush tableTextBrush;
        private Fractal lastFractal;
        
        private InterestsZoneManager interestsZoneManager = new InterestsZoneManager();

        #endregion

        public InterestZones()
            : base()
        {
            Name = "InterestZones";
            Description = "Detects Break of Structure and draws interest zones with RR tracking";
            SeparateWindow = false;
        }

        protected override void OnInit()
        {
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
            interestsZoneManager.OnInit(HistoricalData.Aggregation.GetPeriod, this);

            InterestZoneRenderingIndicator interestZoneRenderingIndicator = new InterestZoneRenderingIndicator();
            interestZoneRenderingIndicator.manager = interestsZoneManager;
            this.AddIndicator(interestZoneRenderingIndicator);
        }

        protected override void OnUpdate(UpdateArgs args)
        {
            interestsZoneManager.OnUpdate(args.Reason, Count, Symbol, Time(0), GetBar(0));
        }
        
        public IOrder CreateOrder(Fractal fractal, Symbol symbol, double rectangleHigh)
        {
            return new FakeOrder()
            {
                IsBuy = fractal.low,
                MaxUnrealizedProfit = 0,
                TotalProfit = 0,
                EntryPrice = fractal.high ? fractal.value - rectangleHigh : fractal.value + rectangleHigh,
                StopLoss = fractal.value + symbol.TickSize * PivotExtraRoom * (fractal.high ? 1 : -1),
                Open = false,
                Closed = false,
                MaxRR = MaxRRValue,
                BreakEvenOnProfit = MinRR
            };
        }

        public Bar GetBar(int index)
        {
            return new Bar()
            {
                Open = Open(0),
                Close = Close(0),
                High = High(0),
                Low = Low(0),
                Volume = Volume(0),
                OpenTime = Time(0),
            };
        }
    }
}