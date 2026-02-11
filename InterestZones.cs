// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using TradingPlatform.BusinessLayer;
using TradingPlatform.BusinessLayer.Chart;
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

        [InputParameter("BOS Line Color Bull", 60)]
        public Color BOSLineColorBull = Color.Lime;

        [InputParameter("BOS Line Color Bear", 60)]
        public Color BOSLineColorBear = Color.Orange;

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

        private Fractal lastFractal;
        private InterestsZoneManager interestsZoneManager = new InterestsZoneManager();

        public Pen bosPenBull { get; set; }
        public Pen bosPenBear { get; set; }
        public SolidBrush brush { get; set; }
        public SolidBrush circleBrush { get; set; }
        public Pen circlePen { get; set; }
        public Pen fractalPen { get; set; }
        public Pen rectPen { get; set; }
        public SolidBrush textBrush { get; set; }
        public Font font { get; set; }

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
            rectPen = new Pen(RectBorderColor, RectBorderWidth);
            fractalPen = new Pen(FractalLineColor, FractalLineWidth);
            bosPenBull = new Pen(BOSLineColorBull, BOSLineWidth);
            bosPenBear = new Pen(BOSLineColorBear, BOSLineWidth);
            brush = new SolidBrush(RectFillColor);
            circleBrush = new SolidBrush(RRCircleColor);
            circlePen = new Pen(RectBorderColor, 1);
            textBrush = new SolidBrush(RRTextColor);
            font = new Font("Arial", 10, FontStyle.Bold);
            bosPenBull.DashStyle = GetDashStyle(BOSLineStyle);
            bosPenBear.DashStyle = GetDashStyle(BOSLineStyle);

            interestsZoneManager.PivotPeriod = PivotPeriod;
            interestsZoneManager.RectangleHeightMode = RectangleHeightMode;
            interestsZoneManager.MinRR = MinRR;
            interestsZoneManager.MaxRRValue = MaxRRValue;
            interestsZoneManager.BarSizeAveragePeriod = BarSizeAveragePeriod;
            interestsZoneManager.BarSizeMultiplier = BarSizeMultiplier;
            interestsZoneManager.PivotExtraRoom = PivotExtraRoom;
            interestsZoneManager.OnInit(HistoricalData.Aggregation.GetPeriod, this);
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

        // public override void OnPaintChart(PaintChartEventArgs args)
        // {
        //     interestZoneRenderingIndicator.OnPaintChart(args);
        // }

        public override void OnPaintChart(PaintChartEventArgs args)
        {
            if (CurrentChart == null)
                return;

            var mainWindow = CurrentChart.Windows[args.WindowIndex];
            var converter = mainWindow.CoordinatesConverter;
            Graphics gr = args.Graphics;

            var fractal = interestsZoneManager.fractalService.lastFractal;
            var previousFractal = fractal == null ? null : fractal.getPrevious(false);
            var halfBar = (interestsZoneManager.bars[^1].OpenTime - interestsZoneManager.bars[^2].OpenTime) / 2;
            while (fractal != null && previousFractal != null)
            {
                double x1 = converter.GetChartX(fractal.dateTime + halfBar);
                double y1 = converter.GetChartY(fractal.value);
                double x2 = converter.GetChartX(interestsZoneManager.bars[previousFractal.index].OpenTime + halfBar);
                double y2 = converter.GetChartY(previousFractal.value);
                fractalPen.DashStyle = GetDashStyle(FractalLineStyle);
                gr.DrawLine(fractalPen, (float)x1, (float)y1, (float)x2, (float)y2);
                fractal = previousFractal;
                previousFractal = previousFractal.getPrevious(false);
            }

            // Draw BOS lines
            foreach (var bos in interestsZoneManager.bosEvents)
            {
                double x1 = converter.GetChartX(bos.fractal.dateTime + halfBar);
                double y1 = converter.GetChartY(bos.fractal.value);
                double x2 = converter.GetChartX(bos.breakTime + halfBar);
                double y2 = converter.GetChartY(bos.fractal.value);

                if (bos.brokeUp)
                    gr.DrawLine(bosPenBull, (float)x1, (float)y1, (float)x2, (float)y2);
                else
                    gr.DrawLine(bosPenBear, (float)x1, (float)y1, (float)x2, (float)y2);
            }

            // Draw zones
            try
            {
                foreach (var area in interestsZoneManager.openAreas)
                    DrawInterestZoneRectangleDrawArea(converter, area, gr);
                foreach (var area in interestsZoneManager.closedAreas)
                    DrawInterestZoneRectangleDrawArea(converter, area, gr);
            }
            catch (Exception e)
            {
                // do nothing
                Core.Loggers.Log("Error drawing interest zones: " + e.Message);
            }

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
            gr.FillEllipse(circleBrush, centerX - radius, centerY - radius, radius * 2,
                radius * 2);

            // Draw circle border
            gr.DrawEllipse(circlePen, centerX - radius, centerY - radius, radius * 2, radius * 2);

            // Draw RR text
            var rawRR = (int)(area.order.Closed ? area.order.TotalProfit : area.order.MaxUnrealizedProfit);
            var rr = Math.Min(rawRR, interestsZoneManager.MaxRRValue);
            string rrText = rr + (rawRR > interestsZoneManager.MaxRRValue ? "+" : "");
            SizeF textSize = gr.MeasureString(rrText, font);
            gr.DrawString(rrText, font, textBrush,
                centerX - textSize.Width / 2,
                centerY - textSize.Height / 2);
        }

        private void DrawStatisticsTable(Graphics gr, Rectangle chartRect)
        {
            int totalZones = interestsZoneManager.closedAreas.Count;

            // Calculate best max value
            int bestMaxValue = 0;
            int bestSum = int.MinValue;

            // Draw table
            string[] lines = new string[]
            {
                $"Total Zones: {totalZones}",
                $"Positive Zones: {interestsZoneManager.positiveAreasCount}",
                $"Sum with Max: {interestsZoneManager.currentRRSum} (RR: {interestsZoneManager.MaxRRValue})",
                $"Best Sum RR: {interestsZoneManager.bestRRSum} (RR: {interestsZoneManager.bestRR})",
                $"Open areas: {interestsZoneManager.openAreas.Count}"
            };

            float x = chartRect.Right - 250;
            float y = 10;
            float padding = 5;
            float lineHeight = 20;

            float maxWidth = 0;
            foreach (var line in lines)
            {
                SizeF size = gr.MeasureString(line, interestsZoneManager.tableFont);
                maxWidth = Math.Max(maxWidth, size.Width);
            }

            float tableWidth = maxWidth + padding * 2;
            float tableHeight = lines.Length * lineHeight + padding * 2;

            gr.FillRectangle(interestsZoneManager.tableBgBrush, x, y, tableWidth, tableHeight);

            float textY = y + padding;
            foreach (var line in lines)
            {
                gr.DrawString(line, interestsZoneManager.tableFont, interestsZoneManager.tableTextBrush, x + padding,
                    textY);
                textY += lineHeight;
            }
        }

        private DashStyle GetDashStyle(LineStyleType style)
        {
            switch (style)
            {
                case LineStyleType.Dash:
                    return DashStyle.Dash;
                case LineStyleType.Dot:
                    return DashStyle.Dot;
                default:
                    return DashStyle.Solid;
            }
        }
    }
}