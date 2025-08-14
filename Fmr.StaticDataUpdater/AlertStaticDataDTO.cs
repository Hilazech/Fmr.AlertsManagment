using System;
using System.Collections.Generic;
using System.Text;

namespace Fmr.StaticDataUpdater
{
    public class AlertStaticDataDTO
    {
        public string MainProperty { get; set; }
        public string Key { get; set; }
        public double? YearlyHigh { get; set; }
        public double? YearlyLow { get; set; }
        public double YearlyChangePcnt { get; set; }
        public double ThreeMonthsChangePcnt { get; set; }
        public double ThreeMonthsVolume { get; set; }
        public double OpenWeekPrice { get; set; }
        public double OpenMonthPrice { get; set; }
        public double OpenYearPrice { get; set; }
        public double OpenThreeMonthPrice { get; set; }
        public double OpenSixMonthPrice { get; set; }
        public double PctFromYearRangePrice { get; set; }
        public double StartYearChangePcnt { get; set; }
        public double StartMonthChangePcnt { get; set; }
        public double? LastClosePrice { get; set; }
        public double MonthVolume { get; set; }
        public double? DailyHight { get; set; }
        public double? DailyLow { get; set; }
        public double? LastPrice { get; set; }
        public DateTime? LastUpdate { get; set; }

    }

    public enum CompareSign
    {
        GreaterThan,
        LessThan,
        EqualTo,
        NotEqualTo
    }
}
