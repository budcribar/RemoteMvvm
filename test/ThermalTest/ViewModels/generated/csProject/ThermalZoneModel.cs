using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HPSystemsTools.Models
{
    public class ThermalZoneModel
    {
        public bool IsActive { get; set; }
        public string DeviceName { get; set; } = "";
        public int Temperature { get; set; }
        public int ProcessorLoad { get; set; }
        public int FanSpeed { get; set; }
    }
}
