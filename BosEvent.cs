using System;
using InterestZones;

namespace InterestZones;

public class BosEvent
{
    public int breakIndex { get; set; }
    public DateTime breakTime { get; set; }
    public Fractal fractal { get; set; }
    public bool brokeUp { get; set; }
}