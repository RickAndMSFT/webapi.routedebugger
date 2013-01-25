using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DemoRouteDebugger.Models
{
    public class Machine
    {
        public DateTime LastPlayed { get; set; }

        public int MachineAlertCount { get; set; }

        public string MachineId { get; set; }

        public string MachineName { get; set; }

        public string MachinePosition { get; set; }

        public string MachineStatus { get; set; }
    }
}