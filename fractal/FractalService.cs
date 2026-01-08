using System;
using System.Collections.Generic;

namespace cAlgo
{
    public class FractalService
    {
        public List<Bar> bars { get; set; }
        public FractalOptions options;
        public String id { get; set; }
        public Fractal lastFractal { get; set; }

        public Action<FractalEvent> onFractal;

        public FractalService(string timeframe, List<Bar> marketSeries, FractalOptions options)
        {
            this.id = timeframe;
            this.options = options;
            this.bars = marketSeries;
        }

        public void addFractal(Fractal fractal)
        {
            if (lastFractal != null)
                lastFractal.addFractal(fractal);

            lastFractal = fractal;
        }

        public void processIndex(int index)
        {
            if (index < options.period)
                return;

            detectLowFractal(index, id);
            detectHighFractal(index, id);
        }

        public Fractal getLastHighFractal(bool best = true)
        {
            if (lastFractal == null)
                return null;
            if (lastFractal.high)
                return best ? lastFractal.getBest() : lastFractal;
            return lastFractal.getPrevious(best);
        }

        public Fractal getLastLowFractal(bool best = true)
        {
            if (lastFractal == null)
                return null;
            if (!lastFractal.high)
                return best ? lastFractal.getBest() : lastFractal;
            return lastFractal.getPrevious(best);
        }

        public Fractal getLastFractal(bool best = true)
        {
            if (lastFractal == null)
                return null;
            return best ? lastFractal.getBest() : lastFractal;
        }

        private bool isHighFractal(int middleIndex)
        {
            int halfPeriod = options.period / 2;
            double middleValue = bars[middleIndex].High;
            for (int i = (middleIndex - halfPeriod); i <= (middleIndex + halfPeriod); i++)
            {
                if (middleValue < bars[i].High)
                    return false;
            }
            return true;
        }

        private bool isLowFractal(int middleIndex)
        {
            int halfPeriod = getHalfPeriod();
            double middleValue = bars[middleIndex].Low;
            for (int i = (middleIndex - halfPeriod); i <= (middleIndex + halfPeriod); i++)
            {
                if (middleValue > bars[i].Low)
                    return false;
            }
            return true;
        }

        private void detectHighFractal(int index, String id)
        {
            int middleIndex = getMiddleIndex(index);
            bool highFractal = isHighFractal(middleIndex);

            if (highFractal)
            {
                Fractal fractal = new Fractal(middleIndex, bars[middleIndex].OpenTime, bars[middleIndex].High, true, id);
                processFractal(index, fractal);
            }
        }

        private void detectLowFractal(int index, String id)
        {
            int middleIndex = getMiddleIndex(index);
            bool lowFractal = isLowFractal(middleIndex);

            if (lowFractal)
            {
                Fractal fractal = new Fractal(middleIndex, bars[middleIndex].OpenTime, bars[middleIndex].Low, false, id);
                processFractal(index, fractal);
            }
        }

        public int getMiddleIndex(int index)
        {
            return index - getHalfPeriod();
        }

        private int getHalfPeriod()
        {
            return (options.period - (options.period % 2)) / 2;
        }

        private void processFractal(int index, Fractal fractal)
        {
            addFractal(fractal);
            FractalEvent fractalEvent = new FractalEvent(index, fractal);
            triggerOnFractal(fractalEvent);
        }

        private void triggerOnFractal(FractalEvent fractalEvent)
        {
            onFractal?.Invoke(fractalEvent);
        }

    }
}