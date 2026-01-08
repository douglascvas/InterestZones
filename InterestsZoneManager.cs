using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using TradingPlatform.BusinessLayer;
using IOrder = InterestZones.Shared.IOrder;

namespace InterestZones;

public partial class InterestsZoneManager
{
    #region Parameters

    public int PivotPeriod = 10;

    public RectHeightMode RectangleHeightMode = RectHeightMode.Auto;

    public int MinRR = 1;

    public double MaxRRValue = 5;

    public int BarSizeAveragePeriod = 10;

    public double BarSizeMultiplier = 0.8;

    public int PivotExtraRoom = 0;

    // STYLES

    public Color BOSLineColor = Color.Yellow;

    public int BOSLineWidth = 1;

    public LineStyleType BOSLineStyle = LineStyleType.Solid;

    public Color FractalLineColor = Color.White;

    public int FractalLineWidth = 1;

    public LineStyleType FractalLineStyle = LineStyleType.Dash;

    public Color RectBorderColor = Color.Cyan;

    public Color RectFillColor = Color.FromArgb(50, 0, 255, 255);

    public int RectBorderWidth = 1;

    public Color RRCircleColor = Color.White;

    public Color RRTextColor = Color.Black;

    #endregion

    #region Fields

    public List<BosEvent> bosEvents { get; set; } = new List<BosEvent>();
    public List<Bar> bars { get; set; } = new List<Bar>();
    public List<FractalArea> closedAreas { get; set; } = new List<FractalArea>();
    public List<FractalArea> openAreas { get; set; } = new List<FractalArea>();
    public List<Fractal> unbrokenFractals { get; set; } = new List<Fractal>();
    public Dictionary<int, Fractal> unbrokenFractalsMap { get; set; } = new Dictionary<int, Fractal>();
    public FractalService fractalService { get; set; }

    public Pen bosPen { get; set; }
    public SolidBrush brush { get; set; }
    public SolidBrush circleBrush { get; set; }
    public Pen circlePen { get; set; }
    public Pen fractalPen { get; set; }
    public Pen rectPen { get; set; }
    public SolidBrush textBrush { get; set; }
    public Font font { get; set; }

    public FractalArea lastArea { get; set; }
    public double lastBid { get; set; }
    public int bestRR { get; set; }
    public int bestRRSum { get; set; }
    public int currentRRSum { get; set; }
    public int positiveAreasCount { get; set; }
    public Font tableFont { get; set; }
    public SolidBrush tableBgBrush { get; set; }
    public SolidBrush tableTextBrush { get; set; }
    private Fractal lastFractal;

    private Indicator simpleIndicator;
    private OrderCreator orderCreator;

    #endregion

    public void OnInit(Period period, OrderCreator orderCreator)
    {
        Cleanup();

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
        this.orderCreator = orderCreator;

        fractalService = new FractalService(period.ToString(),
            bars, new FractalOptions()
            {
                linkHighLow = true,
                period = PivotPeriod
            });


        fractalService.onFractal += e => HandleFractal(e);
    }

    public void Cleanup()
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

    public void OnUpdate(UpdateReason reason, int count, Symbol symbol, DateTime now, Bar bar)
    {
        if (reason == UpdateReason.HistoricalBar ||
            reason == UpdateReason.NewBar)
        {
            bars.Add(bar);
            if (count > 2)
                fractalService.processIndex(count - 2);
        }

        if (double.IsNaN(lastBid))
            lastBid = symbol.Bid;

        if (count < PivotPeriod * 2 + 1)
            return;

        var bid = symbol.Bid;

        if (reason == UpdateReason.HistoricalBar)
        {
            var lastBar = bars[bars.Count - 2];
            if (lastBar.bullish)
            {
                if (bar.bullish)
                {
                    HandleBid(bar.Low, symbol, count, now);
                    lastBid = bar.Low;
                    HandleBid(bar.High, symbol, count, now);
                    lastBid = bar.High;
                }
                else
                {
                    HandleBid(bar.High, symbol, count, now);
                    lastBid = bar.High;
                    HandleBid(bar.Low, symbol, count, now);
                    lastBid = bar.Low;
                }
            }
            else
            {
                if (bar.bullish)
                {
                    HandleBid(bar.Low, symbol, count, now);
                    lastBid = bar.Low;
                    HandleBid(bar.High, symbol, count, now);
                    lastBid = bar.High;
                }
                else
                {
                    HandleBid(bar.High, symbol, count, now);
                    lastBid = bar.High;
                    HandleBid(bar.Low, symbol, count, now);
                    lastBid = bar.Low;
                }
            }
        }
        else if (reason == UpdateReason.NewTick)
        {
            HandleBid(bid, symbol, count, now);
            lastBid = bid;
        }
    }

    public void HandleBid(double bid, Symbol symbol, int count, DateTime now)
    {
        CheckForStructureBreak(bid, symbol, count, now);
        HandleUnmitigatedAreas(bid, count, now);
    }

    private void HandleUnmitigatedAreas(double bid, int count, DateTime now)
    {
        List<FractalArea> toBeRemoved = null;
        foreach (var area in openAreas)
        {
            if (!area.order.Open && !area.order.Closed)
            {
                area.rectangleEnd = now;
                area.rectangleEndIndex = count - 1;
                var bidUp = bid > lastBid;
                var bidDown = bid < lastBid;
            }

            area.order.ProcessPrice(bid, count - 1);
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
                .Select(z => z.order.TotalProfitInt)
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
        //  currentSum +=  Math.Min(zone.order.TotalProfitInt, (int)zone.order.MaxRR);

        bestRR = bestMaxValue;
        bestRRSum = bestSum;
        currentRRSum = currentSum;
        positiveAreasCount = closedAreas.Count(z => z.order.TotalProfit > 0);
    }

    private int SumRRsForMax(double maxRrValue)
    {
        int currentSum = 0;
        foreach (var area in closedAreas)
        {
            if (area.order.TotalProfitInt > 0 && area.order.TotalProfitInt < maxRrValue)
            {
                currentSum += 0;
            }
            else if (area.order.TotalProfitInt >= maxRrValue)
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

    private void CheckForStructureBreak(double bid, Symbol symbol, int count, DateTime now)
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
                var averageBarSize = GetAverageBarSize(fractal, count) * BarSizeMultiplier;
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
                    order = orderCreator.CreateOrder(fractal, symbol, rectangleHigh),
                    structureBroken = true,
                    rectangleTop = fractal.high
                        ? fractal.value + symbol.TickSize * PivotExtraRoom
                        : fractal.value + rectangleHigh,
                    rectangleBottom = fractal.low
                        ? fractal.value - symbol.TickSize * PivotExtraRoom
                        : fractal.value - rectangleHigh,
                    rectangleStart = fractal.dateTime,
                    rectangleStartIndex = fractal.index,
                    rectangleEnd = now,
                    rectangleEndIndex = count - 1
                };
                openAreas.Add(lastArea);

                bosEvents.Add(new BosEvent()
                {
                    fractal = fractalN,
                    breakTime = now,
                    breakIndex = count - 1,
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

    private double GetAverageBarSize(Fractal fractal, int count)
    {
        var n = Math.Max(BarSizeAveragePeriod, 3);
        var half = n / 2;
        var start = Math.Max(0, fractal.index + half - n);
        var max = Math.Min(count - 1, start + n);
        var barSizeSum = 0.0;
        for (int i = start; i < max; i++)
        {
            barSizeSum += bars[i].High - bars[i].Low;
        }

        return barSizeSum / n;
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