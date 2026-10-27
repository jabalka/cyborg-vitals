using System;
using System.Globalization;

namespace CyborgVitals
{
    public static class AlertValues
    {
        public static bool TryParse(string text, CultureInfo culture, out double value)
        {
            bool parsed = double.TryParse(text, NumberStyles.Float, culture, out value)
                || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
            return parsed && !double.IsNaN(value) && !double.IsInfinity(value);
        }
        public static double Step(double value, double minimum, double maximum, double step, int direction)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) value = minimum;
            return Math.Round(Math.Max(minimum, Math.Min(maximum, value + step * Math.Sign(direction))), 2);
        }
    }
}
