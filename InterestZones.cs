// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using cAlgo;
using TradingPlatform.BusinessLayer;
using TradingPlatform.BusinessLayer.Chart;

namespace InterestZones
{
    /// <summary>
    /// InterestZones indicator - Detects Break of Structure (BOS) and draws interest zones with RR tracking
    /// </summary>
    public class InterestZones : Indicator
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

        // [InputParameter("Enable Retracement Zones", 30)]
        // public bool EnableRetracementZones = false;
        //
        // [InputParameter("Retracement Percentage", 40, 0, 100, 1, 0)]
        // public int RetracementPercentage = 60;

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

        #region Classes and Enums

        public enum RectHeightMode
        {
            Auto,
            AverageOnly
        }

        public enum LineStyleType
        {
            Solid,
            Dash,
            Dot
        }

        private class BosEvent
        {
            public int breakIndex { get; set; }
            public DateTime breakTime { get; set; }
            public Fractal fractal { get; set; }
            public bool brokeUp { get; set; }
        }

        #endregion

        #region Fields

        private List<BosEvent> bosEvents = new List<BosEvent>();
        private List<Bar> bars = new List<Bar>();
        private List<FractalArea> closedAreas = new List<FractalArea>();
        private List<FractalArea> openAreas = new List<FractalArea>();
        private List<Fractal> unbrokenFractals = new List<Fractal>();
        private Dictionary<int, Fractal> unbrokenFractalsMap = new Dictionary<int, Fractal>();
        private FractalService fractalService;
        private Pen bosPen;
        private SolidBrush brush;
        private SolidBrush circleBrush;
        private Pen circlePen;
        private Pen fractalPen;
        private Pen rectPen;
        private SolidBrush textBrush;
        private Font font;

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
            bosEvents.Clear();
            openAreas.Clear();
            closedAreas.Clear();
            unbrokenFractals.Clear();
            unbrokenFractalsMap.Clear();
            bars.Clear();

            lastFractal = null;
            lastArea = null;
            bestRR = 0;
            bestRRSum = 0;
            currentRRSum = 0;
            positiveAreasCount = 0;

            rectPen = new Pen(RectBorderColor, RectBorderWidth);
            fractalPen = new Pen(FractalLineColor, FractalLineWidth);
            bosPen = new Pen(BOSLineColor, BOSLineWidth);
            brush = new SolidBrush(RectFillColor);
            circleBrush = new SolidBrush(RRCircleColor);
            circlePen = new Pen(RectBorderColor, 1);
            textBrush = new SolidBrush(RRTextColor);
            font = new Font("Arial", 10, FontStyle.Bold);
            tableFont = new Font("Arial", 9);
            tableBgBrush = new SolidBrush(Color.FromArgb(200, 0, 0, 0));
            tableTextBrush = new SolidBrush(Color.White);

            fractalService = new FractalService(this.HistoricalData.Aggregation.GetPeriod.ToString(),
                bars, new FractalOptions()
                {
                    linkHighLow = true,
                    period = PivotPeriod
                });


            fractalService.onFractal += e => HandleFractal(e);

            lastCheckedHigh = double.MinValue;
            lastCheckedLow = double.MaxValue;
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

        protected override void OnUpdate(UpdateArgs args)
        {
            if (args.Reason == UpdateReason.HistoricalBar ||
                args.Reason == UpdateReason.NewBar)
            {
                Bar bar = getBar();
                bars.Add(bar);
                if (Count > 2)
                    fractalService.processIndex(Count - 2);
            }

            if (double.IsNaN(lastBid))
                lastBid = Bid(0);

            if (Count < PivotPeriod * 2 + 1)
                return;

            var bid = this.Symbol.Bid;

            if (args.Reason == UpdateReason.HistoricalBar)
            {
                var lastBar = bars[bars.Count - 2];
                var bar = bars[bars.Count - 1];
                if (lastBar.bullish)
                {
                    if (bar.bullish)
                    {
                        HandleBid(bar.Low);
                        lastBid = bar.Low;
                        HandleBid(bar.High);
                        lastBid = bar.High;
                    }
                    else
                    {
                        HandleBid(bar.High);
                        lastBid = bar.High;
                        HandleBid(bar.Low);
                        lastBid = bar.Low;
                    }
                }
                else
                {
                    if (bar.bullish)
                    {
                        HandleBid(bar.Low);
                        lastBid = bar.Low;
                        HandleBid(bar.High);
                        lastBid = bar.High;
                    }
                    else
                    {
                        HandleBid(bar.High);
                        lastBid = bar.High;
                        HandleBid(bar.Low);
                        lastBid = bar.Low;
                    }
                }
            }
            else if (args.Reason == UpdateReason.NewTick)
            {
                HandleBid(bid);
                lastBid = bid;
            }
        }

        private void HandleBid(double bid)
        {
            CheckForStructureBreak(bid);
            HandleUnmitigatedAreas(bid);
        }

        private void HandleUnmitigatedAreas(double bid)
        {
            List<FractalArea> toBeRemoved = null;
            foreach (var area in openAreas)
            {
                if (!area.order.open && !area.order.closed)
                {
                    area.rectangleEnd = Time(0);
                    area.rectangleEndIndex = Count - 1;
                    var bidUp = bid > lastBid;
                    var bidDown = bid < lastBid;
                }

                area.order.ProcessPrice(bid, Count - 1);
                if (area.order.closed)
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

                RecalculateBestRR();
            }
        }

        private void RecalculateBestRR()
        {
            // Calculate best max value
            int bestMaxValue = 0;
            int bestSum = int.MinValue;

            if (closedAreas.Count > 0)
            {
                int maxRRFound = closedAreas
                    .Select(z => z.order.totalProfitInt)
                    .Where(z => z > 0)
                    .Max(z => z);

                for (int testMax = 1; testMax <= Math.Max(maxRRFound, 1); testMax++)
                {
                    int testSum = 0;
                    testSum += SumRRsForMax(testMax);

                    if (testSum > bestSum)
                    {
                        bestSum = testSum;
                        bestMaxValue = testMax;
                    }
                }
            }

            var currentSum = SumRRsForMax(MaxRRValue);
            //  currentSum +=  Math.Min(zone.order.totalProfitInt, (int)zone.order.maxRR);

            bestRR = bestMaxValue;
            bestRRSum = bestSum;
            currentRRSum = currentSum;
            positiveAreasCount = closedAreas.Count(z => z.order.totalProfit > 0);
        }

        private int SumRRsForMax(double maxRrValue)
        {
            int currentSum = 0;
            foreach (var area in closedAreas)
            {
                if (area.order.totalProfitInt > 0 && area.order.totalProfitInt < maxRrValue)
                {
                    currentSum += 0;
                }
                else if (area.order.totalProfitInt >= maxRrValue)
                {
                    currentSum += (int)maxRrValue;
                }
                else
                {
                    currentSum += -1;
                }
            }

            return currentSum;
        }

        private void CheckForStructureBreak(double bid)
        {
            List<Fractal> toBeRemoved = null;
            for (int f = 0; f < unbrokenFractals.Count - 1; f++)
            {
                Fractal fractalN = unbrokenFractals[f];
                // var lastFractal = fractalService.lastFractal;
                // if (lastFractal == null)
                // return;
                // var lastHighFractal = lastFractal.high ? lastFractal : lastFractal.getPrevious(true);
                // var lastLowFractal = lastFractal.low ? lastFractal : lastFractal.getPrevious(true);
                // if (lastHighFractal == null || lastLowFractal == null) return;

                var bidUp = bid > lastBid;
                var bidDown = bid < lastBid;

                if ((fractalN.high && bid > fractalN.value) || (fractalN.low && bid < fractalN.value))
                {
                    if (toBeRemoved == null)
                        toBeRemoved = new List<Fractal>();
                    toBeRemoved.Add(fractalN);

                    // broke structure
                    var fractal = fractalN.getNext(true);
                    if (fractal == null) return;
                    // toBeRemoved.Add(fractal);
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

                    lastArea = new FractalArea
                    {
                        fractal = fractal,
                        order = new FakeOrder()
                        {
                            isBuy = fractal.low,
                            maxUnrealizedProfit = 0,
                            totalProfit = 0,
                            entryPrice = fractal.high ? fractal.value - rectangleHigh : fractal.value + rectangleHigh,
                            stopLoss = fractal.value + Symbol.TickSize * PivotExtraRoom * (fractal.high ? 1 : -1),
                            open = false,
                            closed = false,
                            maxRR = MaxRRValue,
                            breakEvenOnProfit = MinRR
                        },
                        structureBroken = true,
                        rectangleTop = fractal.high
                            ? fractal.value + Symbol.TickSize * PivotExtraRoom
                            : fractal.value + rectangleHigh,
                        rectangleBottom = fractal.low
                            ? fractal.value - Symbol.TickSize * PivotExtraRoom
                            : fractal.value - rectangleHigh,
                        rectangleStart = fractal.dateTime,
                        rectangleStartIndex = fractal.index,
                        rectangleEnd = Time(0),
                        rectangleEndIndex = Count - 1
                    };
                    openAreas.Add(lastArea);

                    bosEvents.Add(new BosEvent()
                    {
                        fractal = fractalN,
                        breakTime = Time(0),
                        breakIndex = Count - 1,
                        brokeUp = bidUp
                    });
                }
            }

            if (toBeRemoved != null)
                foreach (var fractal in toBeRemoved)
                    unbrokenFractals.Remove(fractal);

            // foreach (var fractal in unbrokenFractals)
            // {
            //     if (fractal.getNext(false) != null && ((fractal.high && lastBid <= fractal.value && bid > fractal.value)
            //         || (fractal.low && lastBid >= fractal.value && bid < fractal.value)))
            //     {
            //         // broke structure
            //         unbrokenFractals.Remove(fractal);
            //         var averageBarSize = GetAverageBarSize(fractal);
            //     }
            // }
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
            var max = Math.Min(Count - 1, start + n);
            var barSizeSum = 0.0;
            for (int i = start; i < max; i++)
            {
                barSizeSum += bars[i].High - bars[i].Low;
            }

            return barSizeSum / n;
        }

        public override void OnPaintChart(PaintChartEventArgs args)
        {
            if (CurrentChart == null || CurrentChart.Windows.Count() == 0)
                return;

            var mainWindow = CurrentChart.Windows[args.WindowIndex];
            var converter = mainWindow.CoordinatesConverter;
            Graphics gr = args.Graphics;

            var fractal = fractalService.lastFractal;
            var previousFractal = fractal == null ? null : fractal.getPrevious(false);
            var halfBar = (bars[^1].OpenTime - bars[^2].OpenTime) / 2;
            while (fractal != null && previousFractal != null)
            {
                double x1 = converter.GetChartX(fractal.dateTime + halfBar);
                double y1 = converter.GetChartY(fractal.value);
                double x2 = converter.GetChartX(bars[previousFractal.index].OpenTime + halfBar);
                double y2 = converter.GetChartY(previousFractal.value);
                fractalPen.DashStyle = GetDashStyle(FractalLineStyle);
                gr.DrawLine(fractalPen, (float)x1, (float)y1, (float)x2, (float)y2);
                fractal = previousFractal;
                previousFractal = previousFractal.getPrevious(false);
            }

            // Draw BOS lines
            foreach (var bos in bosEvents)
            {
                double x1 = converter.GetChartX(bos.fractal.dateTime + halfBar);
                double y1 = converter.GetChartY(bos.fractal.value);
                double x2 = converter.GetChartX(bos.breakTime + halfBar);
                double y2 = converter.GetChartY(bos.fractal.value);

                bosPen.DashStyle = GetDashStyle(BOSLineStyle);
                gr.DrawLine(bosPen, (float)x1, (float)y1, (float)x2, (float)y2);
            }

            // Draw zones
            foreach (var area in openAreas)
                DrawInterestZoneRectangleDrawArea(converter, area, gr);
            foreach (var area in closedAreas)
                DrawInterestZoneRectangleDrawArea(converter, area, gr);

            // Draw statistics table
            DrawStatisticsTable(gr, args.Rectangle);
        }

        private void DrawInterestZoneRectangleDrawArea(IChartWindowCoordinatesConverter converter, FractalArea area,
            Graphics gr)
        {
            double x1 = converter.GetChartX(area.rectangleStart);
            double y1 = converter.GetChartY(area.rectangleTop);

            // Extend only unmitigated zones - mitigated zones stop at mitigation point
            double x2 = converter.GetChartX(area.rectangleEnd);
            double y2 = converter.GetChartY(area.rectangleBottom);

            float rectX = (float)Math.Min(x1, x2);
            float rectY = (float)Math.Min(y1, y2);
            float rectWidth = (float)Math.Abs(x2 - x1);
            float rectHeight = (float)Math.Abs(y2 - y1);

            // Fill rectangle
            gr.FillRectangle(brush, rectX, rectY, rectWidth, rectHeight);

            // Draw rectangle border
            // gr.DrawRectangle(rectPen, rectX, rectY, rectWidth, rectHeight);

            // Draw RR if zone is closed
            // if (zone.IsClosed)
            // {
            float centerX = rectX + 20;
            float centerY = rectY + rectHeight / 2;
            float radius = 15;

            // Draw circle
            gr.FillEllipse(circleBrush, centerX - radius, centerY - radius, radius * 2, radius * 2);

            // Draw circle border
            gr.DrawEllipse(circlePen, centerX - radius, centerY - radius, radius * 2, radius * 2);

            // Draw RR text
            var rawRR = (int)(area.order.closed ? area.order.totalProfit : area.order.maxUnrealizedProfit);
            var rr = Math.Min(rawRR, MaxRRValue);
            string rrText = rr + (rawRR > MaxRRValue ? "+" : "");
            SizeF textSize = gr.MeasureString(rrText, font);
            gr.DrawString(rrText, font, textBrush, centerX - textSize.Width / 2,
                centerY - textSize.Height / 2);
        }

        private void DrawStatisticsTable(Graphics gr, Rectangle chartRect)
        {
            int totalZones = closedAreas.Count;

            // Calculate best max value
            int bestMaxValue = 0;
            int bestSum = int.MinValue;

            // Draw table
            string[] lines = new string[]
            {
                $"Total Zones: {totalZones}",
                $"Positive Zones: {positiveAreasCount}",
                $"Sum with Max: {currentRRSum} (RR: {MaxRRValue})",
                $"Best Sum RR: {bestRRSum} (RR: {bestRR})",
                $"Open areas: {openAreas.Count}"
            };

            float x = chartRect.Right - 250;
            float y = 10;
            float padding = 5;
            float lineHeight = 20;

            float maxWidth = 0;
            foreach (var line in lines)
            {
                SizeF size = gr.MeasureString(line, tableFont);
                maxWidth = Math.Max(maxWidth, size.Width);
            }

            float tableWidth = maxWidth + padding * 2;
            float tableHeight = lines.Length * lineHeight + padding * 2;

            gr.FillRectangle(tableBgBrush, x, y, tableWidth, tableHeight);

            float textY = y + padding;
            foreach (var line in lines)
            {
                gr.DrawString(line, tableFont, tableTextBrush, x + padding, textY);
                textY += lineHeight;
            }
        }

        private System.Drawing.Drawing2D.DashStyle GetDashStyle(LineStyleType style)
        {
            switch (style)
            {
                case LineStyleType.Dash:
                    return System.Drawing.Drawing2D.DashStyle.Dash;
                case LineStyleType.Dot:
                    return System.Drawing.Drawing2D.DashStyle.Dot;
                default:
                    return System.Drawing.Drawing2D.DashStyle.Solid;
            }
        }

        public Bar getBar()
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