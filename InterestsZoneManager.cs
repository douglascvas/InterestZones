using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using TradingPlatform.BusinessLayer;

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

    #endregion

    #region Fields

    public List<BosEvent> bosEvents { get; set; } = new List<BosEvent>();
    public Action<BosEvent> onBosEvent;
    public Action<FractalArea> onAreaOpen;
    public List<Bar> bars { get; set; } = new List<Bar>();
    public List<FractalArea> closedAreas { get; set; } = new List<FractalArea>();
    public List<FractalArea> openAreas { get; set; } = new List<FractalArea>();
    public List<Fractal> unbrokenFractals { get; set; } = new List<Fractal>();
    public Dictionary<int, Fractal> unbrokenFractalsMap { get; set; } = new Dictionary<int, Fractal>();
    public FractalService fractalService { get; set; }

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

        tableFont = new Font("Arial", 9);
        tableBgBrush = new SolidBrush(Color.FromArgb(200, 0, 0, 0));
        tableTextBrush = new SolidBrush(Color.White);
        this.orderCreator = orderCreator;

        fractalService = new FractalService(period.ToString(),
            bars, new FractalOptions()
            {
                linkHighLow = true,
                period = PivotPeriod,
                requireConfirmation = false
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

        lastArea = null;
        bestRR = 0;
        bestRRSum = 0;
        currentRRSum = 0;
        positiveAreasCount = 0;
    }

    private void HandleFractal(FractalEvent e)
    {
        unbrokenFractalsMap[e.fractal.getId()] = e.fractal;
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
        else
        {
            bars[^1] = bar;
        }

        if (double.IsNaN(lastBid))
            lastBid = symbol.Bid;

        if (count < PivotPeriod * 2 + 1)
            return;

        var bid = symbol.Bid;

        // if (reason == UpdateReason.HistoricalBar)
        // {
        //     var lastBar = bars[bars.Count - 2];
        //     if (lastBar.bullish)
        //     {
        //         if (bar.bullish)
        //         {
        //             HandleBid(bar.Low, symbol, count, now);
        //             lastBid = bar.Low;
        //             HandleBid(bar.High, symbol, count, now);
        //             lastBid = bar.High;
        //         }
        //         else
        //         {
        //             HandleBid(bar.High, symbol, count, now);
        //             lastBid = bar.High;
        //             HandleBid(bar.Low, symbol, count, now);
        //             lastBid = bar.Low;
        //         }
        //     }
        //     else
        //     {
        //         if (bar.bullish)
        //         {
        //             HandleBid(bar.Low, symbol, count, now);
        //             lastBid = bar.Low;
        //             HandleBid(bar.High, symbol, count, now);
        //             lastBid = bar.High;
        //         }
        //         else
        //         {
        //             HandleBid(bar.High, symbol, count, now);
        //             lastBid = bar.High;
        //             HandleBid(bar.Low, symbol, count, now);
        //             lastBid = bar.Low;
        //         }
        //     }
        // }
        // else
        // {
        HandleBid(symbol);
        lastBid = bid;
        // }
    }

    public void HandleBid(Symbol symbol)
    {
        CheckForStructureBreak(symbol);
        HandleUnmitigatedAreas();
    }

    private void HandleUnmitigatedAreas()
    {
        List<FractalArea> toBeRemoved = null;
        var bar = bars[^1];
        var count = bars.Count;
        var now = bar.OpenTime;
        foreach (var area in openAreas)
        {
            if (!area.order.Open && !area.order.Closed)
            {
                area.rectangleEnd = now;
                area.rectangleEndIndex = count - 1;
            }

            area.order.ProcessPrice(bar, count - 1);
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
            // int maxRRFound = closedAreas
            //     .Select(z => z.order.TotalProfitInt)
            //     .Where(z => z > 0)
            //     .DefaultIfEmpty(0)
            //     .Max(z => z);

            // for (int testMax = 1; testMax <= Math.Max(maxRRFound, 1); testMax++)
            // {
            //     int testSum = 0;
            //     testSum += SumRRsForMax(testMax);
            //
            //     if (testSum > bestSum)
            //     {
            //         bestSum = testSum;
            //         bestMaxValue = testMax;
            //     }
            // }
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

    private void CheckForStructureBreak(Symbol symbol)
    {
        List<Fractal> toBeRemoved = null;
        var bar = bars[^1];
        foreach (var fractalN in unbrokenFractalsMap.Values)
        {
            // var lastFractal = fractalService.lastFractal;
            // if (lastFractal == null)
            // return;
            // var lastHighFractal = lastFractal.high ? lastFractal : lastFractal.getPrevious(true);
            // var lastLowFractal = lastFractal.low ? lastFractal : lastFractal.getPrevious(true);
            // if (lastHighFractal == null || lastLowFractal == null) return;

            if ((fractalN.high && bar.High > fractalN.value) || (fractalN.low && bar.Low < fractalN.value))
            {
                if (toBeRemoved == null)
                    toBeRemoved = new List<Fractal>();
                toBeRemoved.Add(fractalN);

                // broke structure
                var fractal = fractalN.getNext(true);
                if (fractal == null) break;
                // toBeRemoved.Add(fractal);
                var averageBarSize = GetAverageBarSize(fractal, bars.Count) * BarSizeMultiplier;
                var fractalBar = bars[fractal.index];
                var wickSize = fractal.high
                    ? fractalBar.upperWickTop - fractalBar.upperWickBottom
                    : fractalBar.lowerWickTop - fractalBar.lowerWickBottom;
                var rectangleHigh = RectangleHeightMode == RectHeightMode.AverageOnly
                    ? averageBarSize
                    : Math.Min(wickSize, averageBarSize);
                if (RectangleHeightMode == RectHeightMode.Auto && rectangleHigh < 0.4 * averageBarSize)
                    rectangleHigh = averageBarSize;

                if (!IsFractalValid(fractal))
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
                    rectangleEnd = bar.OpenTime,
                    rectangleEndIndex = bars.Count - 1
                };
                openAreas.Add(lastArea);
                onAreaOpen?.Invoke(lastArea);

                var bosEvent = new BosEvent()
                {
                    fractal = fractalN,
                    breakTime = bar.OpenTime,
                    breakIndex = bars.Count - 1,
                    brokeUp = fractal.low
                };
                bosEvents.Add(bosEvent);
                onBosEvent?.Invoke(bosEvent);
            }
        }

        if (toBeRemoved != null)
        {
            foreach (var fractal in toBeRemoved)
                unbrokenFractalsMap.Remove(fractal.getId());
        }

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

    private bool IsFractalValid(Fractal fractalN)
    {
        for (int i = fractalN.index + 1; i < bars.Count - 1; i++)
        {
            var bar = bars[i];
            if ((fractalN.high && bar.High > fractalN.value) ||
                (fractalN.low && bar.Low < fractalN.value))
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
}