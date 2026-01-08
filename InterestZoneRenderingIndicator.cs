using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using TradingPlatform.BusinessLayer;
using TradingPlatform.BusinessLayer.Chart;

namespace InterestZones;

public class InterestZoneRenderingIndicator : Indicator
{
    private FractalService fractalService;
    private List<Bar> bars;

    public InterestsZoneManager manager { get; set; }

    public InterestZoneRenderingIndicator()
    {
        this.Name = "InterestZoneRenderingIndicator";
        this.SeparateWindow = false;
    }
    
    protected override void OnUpdate(UpdateArgs args)
    {
        
        // something usefull
    }
    
    public override void OnPaintChart(PaintChartEventArgs args)
        {
            if (CurrentChart == null)
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
                manager.fractalPen.DashStyle = GetDashStyle(manager.FractalLineStyle);
                gr.DrawLine(manager.fractalPen, (float)x1, (float)y1, (float)x2, (float)y2);
                fractal = previousFractal;
                previousFractal = previousFractal.getPrevious(false);
            }

            // Draw BOS lines
            foreach (var bos in manager.bosEvents)
            {
                double x1 = converter.GetChartX(bos.fractal.dateTime + halfBar);
                double y1 = converter.GetChartY(bos.fractal.value);
                double x2 = converter.GetChartX(bos.breakTime + halfBar);
                double y2 = converter.GetChartY(bos.fractal.value);

                manager.bosPen.DashStyle = GetDashStyle(manager.BOSLineStyle);
                gr.DrawLine(manager.bosPen, (float)x1, (float)y1, (float)x2, (float)y2);
            }

            // Draw zones
            foreach (var area in manager.openAreas)
                DrawInterestZoneRectangleDrawArea(converter, area, gr);
            foreach (var area in manager.closedAreas)
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
            gr.FillRectangle(manager.brush, rectX, rectY, rectWidth, rectHeight);

            // Draw rectangle border
            // gr.DrawRectangle(rectPen, rectX, rectY, rectWidth, rectHeight);

            // Draw RR if zone is closed
            // if (zone.IsClosed)
            // {
            float centerX = rectX + 20;
            float centerY = rectY + rectHeight / 2;
            float radius = 15;

            // Draw circle
            gr.FillEllipse(manager.circleBrush, centerX - radius, centerY - radius, radius * 2, radius * 2);

            // Draw circle border
            gr.DrawEllipse(manager.circlePen, centerX - radius, centerY - radius, radius * 2, radius * 2);

            // Draw RR text
            var rawRR = (int)(area.order.Closed ? area.order.TotalProfit : area.order.MaxUnrealizedProfit);
            var rr = Math.Min(rawRR, manager.MaxRRValue);
            string rrText = rr + (rawRR > manager.MaxRRValue ? "+" : "");
            SizeF textSize = gr.MeasureString(rrText, manager.font);
            gr.DrawString(rrText, manager.font, manager.textBrush, centerX - textSize.Width / 2,
                centerY - textSize.Height / 2);
        }

        private void DrawStatisticsTable(Graphics gr, Rectangle chartRect)
        {
            int totalZones = manager.closedAreas.Count;

            // Calculate best max value
            int bestMaxValue = 0;
            int bestSum = int.MinValue;

            // Draw table
            string[] lines = new string[]
            {
                $"Total Zones: {totalZones}",
                $"Positive Zones: {manager.positiveAreasCount}",
                $"Sum with Max: {manager.currentRRSum} (RR: {manager.MaxRRValue})",
                $"Best Sum RR: {manager.bestRRSum} (RR: {manager.bestRR})",
                $"Open areas: {manager.openAreas.Count}"
            };

            float x = chartRect.Right - 250;
            float y = 10;
            float padding = 5;
            float lineHeight = 20;

            float maxWidth = 0;
            foreach (var line in lines)
            {
                SizeF size = gr.MeasureString(line, manager.tableFont);
                maxWidth = Math.Max(maxWidth, size.Width);
            }

            float tableWidth = maxWidth + padding * 2;
            float tableHeight = lines.Length * lineHeight + padding * 2;

            gr.FillRectangle(manager.tableBgBrush, x, y, tableWidth, tableHeight);

            float textY = y + padding;
            foreach (var line in lines)
            {
                gr.DrawString(line, manager.tableFont, manager.tableTextBrush, x + padding, textY);
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