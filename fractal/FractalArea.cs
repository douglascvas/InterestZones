using System;
using InterestZones.Shared;

namespace InterestZones;

public class FractalArea
{
    public Fractal fractal { get; set; }
    public bool structureBroken { get; set; }
    public double rectangleTop { get; set; }
    public double rectangleBottom { get; set; }
    public DateTime rectangleStart { get; set; }
    public int rectangleStartIndex { get; set; }
    public DateTime rectangleEnd { get; set; }
    public int rectangleEndIndex { get; set; }
    public IOrder order { get; set; }

    public bool mitigated => order.Open || order.Closed;
}