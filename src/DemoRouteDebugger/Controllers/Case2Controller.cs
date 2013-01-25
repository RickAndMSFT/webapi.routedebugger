using System;
using System.Collections.Generic;
using System.Web.Http;
using DemoRouteDebugger.Models;

namespace DemoRouteDebugger.Controllers
{
    public class Case2Controller : ApiController
    {
        public IEnumerable<Machine> Get()
        {
            return new List<Machine>
                   {
                       new Machine
                           {
                               LastPlayed = DateTime.UtcNow,
                               MachineAlertCount = 1,
                               MachineId = "122",
                               MachineName = "test",
                               MachinePosition = "12",
                               MachineStatus = "test"
                           }
                   };
        }

        public IEnumerable<Machine> All(string code)
        {
            return new List<Machine>
                   {
                       new Machine
                           {
                               LastPlayed = DateTime.UtcNow,
                               MachineAlertCount = 1,
                               MachineId = "122",
                               MachineName = "test",
                               MachinePosition = "12",
                               MachineStatus = "test"
                           }
                   };
        }

    }
}