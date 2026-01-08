namespace InterestZones.Shared
{
    /// <summary>
    /// Configuration for zone detection - shared between indicator and bot
    /// </summary>
    public class ZoneDetectorConfig
    {
        public int PivotPeriod { get; set; } = 10;
        public RectHeightMode RectangleHeightMode { get; set; } = RectHeightMode.Auto;
        public int MinRR { get; set; } = 1;
        public double MaxRRValue { get; set; } = 5;
        public int BarSizeAveragePeriod { get; set; } = 10;
        public double BarSizeMultiplier { get; set; } = 0.8;
        public int PivotExtraRoom { get; set; } = 0;
    }

    public enum RectHeightMode
    {
        Auto,
        AverageOnly
    }
}
