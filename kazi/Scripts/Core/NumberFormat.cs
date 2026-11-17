using System;
using System.Globalization;

namespace Kazi.Core
{
    /// <summary>Konuşmaya uygun kısa sayı biçimi: 1,2 bin / 3,4 milyon.</summary>
    public static class NumberFormat
    {
        private static readonly string[] Names = { "", "bin", "milyon", "milyar", "trilyon", "katrilyon", "kentilyon" };

        public static string Short(long value)
        {
            if (value < 0) return "eksi " + Short(value == long.MinValue ? long.MaxValue : -value);
            if (value < 1000) return value.ToString(CultureInfo.InvariantCulture);
            double v = value;
            int idx = 0;
            while (v >= 1000 && idx < Names.Length - 1) { v /= 1000.0; idx++; }
            double r = Math.Round(v, v < 10 ? 1 : 0, MidpointRounding.AwayFromZero);
            if (r >= 1000 && idx < Names.Length - 1) { r /= 1000.0; idx++; r = Math.Round(r, 1, MidpointRounding.AwayFromZero); }
            string num = r.ToString(r == Math.Floor(r) ? "0" : "0.0", CultureInfo.InvariantCulture).Replace('.', ',');
            return num + " " + Names[idx];
        }

        public static string Lira(long value) { return Short(value) + " lira"; }

        public static string Dec(double v)
        {
            return v.ToString(v == Math.Floor(v) ? "0" : "0.##", CultureInfo.InvariantCulture).Replace('.', ',');
        }

        /// <summary>Süre: "2 saat 5 dakika", "45 saniye".</summary>
        public static string Duration(double seconds)
        {
            if (seconds < 0) seconds = 0;
            long s = (long)Math.Round(seconds);
            if (s < 60) return s + " saniye";
            long m = s / 60;
            if (m < 60) return m + " dakika";
            long h = m / 60; m = m % 60;
            return m == 0 ? h + " saat" : h + " saat " + m + " dakika";
        }
    }
}
