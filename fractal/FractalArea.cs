using System;

namespace cAlgo;

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
    public FakeOrder order { get; set; }

    public bool mitigated => order.open || order.closed;
}