namespace InterestZones
{
    public class FractalOptions
    {
        public FractalOptions(int period, bool showHorizontalContinuationLine, bool showVerticalContinuationLine, bool linkHighLow, bool requireConfirmation = false)
        {
            this.period = period;
            this.showHorizontalContinuationLine = showHorizontalContinuationLine;
            this.showVerticalContinuationLine = showVerticalContinuationLine;
            this.linkHighLow = linkHighLow;
            this.requireConfirmation = requireConfirmation;
        }

        public FractalOptions()
        {
            this.period = 5;
            this.showHorizontalContinuationLine = true;
            this.showVerticalContinuationLine = true;
            this.linkHighLow = true;
            this.requireConfirmation = false;
        }

        public int period { get; set; }
        public bool showHorizontalContinuationLine { get; set; }
        public bool showVerticalContinuationLine { get; set; }
        public bool linkHighLow { get; set; }
        public bool requireConfirmation { get; set; }
    }
}