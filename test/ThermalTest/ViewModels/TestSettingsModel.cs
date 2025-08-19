using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HPSystemsTools.Models
{
    public class TestSettingsModel
    {
        private const int DEFAULT_DTS = 100;
        /// <summary>
        /// Percent of TJunction / DTS
        /// </summary>
        public int CpuTemperatureThreshold { get; set; } = 90;
        public int CpuLoadThreshold { get; set; } = 15;
        public int CpuLoadTimeSpan { get; set; } = 60;
        public int GetTemperatureThreshold(string? name)
        {
            int dts = DEFAULT_DTS;
            if (name != null)
            {
                if (DTS.ContainsKey(name))
                {
                    dts = DTS[name];
                }
            }
            return CpuTemperatureThreshold * dts / 100;
        }

        public static Dictionary<string, int> DTS { get; set; } = new Dictionary<string, int>()
        {
            {"Intel(R) Core(TM) i7-10850H CPU @ 2.70GHz", 100 },
            {"Intel(R) Xeon(R) w5-3435X", 98 },
            {"Intel(R) Xeon(R) w9-3495X", 99 },
            {"Intel(R) Xeon(R) w9-3475X", 95 },
            {"Intel(R) Xeon(R) w7-3465X", 97 },
            {"Intel(R) Xeon(R) w7-3455", 92 },
            {"Intel(R) Xeon(R) w7-3445", 94 },
            {"Intel(R) Xeon(R) w5-3425", 103 },
            {"Intel(R) Xeon(R) w7-2475X", 94 },
            {"Intel(R) Xeon(R) w7-2495X", 94 },
            {"Intel(R) Xeon(R) Gold 6448Y", 92 },
            {"Intel(R) Xeon(R) Gold 6444Y",100 },
            {"Intel(R) Xeon(R) Gold 6442Y", 95 },
            {"Intel(R) Xeon(R) Gold 6430Y", 90 },
            {"Intel(R) Xeon(R) Gold 6542Y", 101 },
            {"Intel(R) Xeon(R) Silver 4114 CPU @ 2.20GHz", 78 }
        };
    }
}
