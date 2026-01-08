using System;
using TradingPlatform.BusinessLayer;

namespace InterestZones;

public class Bar
{
    public double Open { get; set; }
    public double Close { get; set; }
    public double High { get; set; }
    public double Low { get; set; }
    public DateTime OpenTime { get; set; }
    public double Volume { get; set; }
    public double bodyTop => Math.Max(Open, Close);
    public double bodyBottom => Math.Min(Open, Close);
    public double upperWickTop => High;
    public double upperWickBottom => bodyTop;
    public double lowerWickTop => bodyBottom;
    public double lowerWickBottom => Low;
    public bool bullish => Close > Open;
}