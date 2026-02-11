namespace InterestZones
{
    public class FractalEvent
    {
        public FractalEvent(int index, Fractal fractal)
        {
            this.fractal = fractal;
            this.index = index;
        }

        public bool newFractalConfirmed { get; set; }
        public int index { get; set; }
        public Fractal fractal { get; set; }
    }
}