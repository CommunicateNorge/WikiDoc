using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wiki.Models
{
    public class Address
    {
        public string environment { get; set; }
        public string address { get; set; }
    }

    public class Endpoint
    {
        public string name { get; set; }
        public string type { get; set; }
        public string actor { get; set; }
        public List<Address> addresses { get; set; }
        public string maps { get; set; }
        public string schemas { get; set; }
    }

    public class Sla
    {
        public string criticality { get; set; }
        public string operatingHours { get; set; }
        public string frequency { get; set; }
        public string compliance { get; set; }
        public string serviceWindow { get; set; }
    }

    public class WebJobInfo
    {
        public string webJobName { get; set; }
        public string startTime { get; set; }
        public string endTime { get; set; }
        public string jobRecurrenceFrequency { get; set; }
        public string interval { get; set; }
        public string runMode { get; set; }
        public string description { get; set; }
        public List<string> relatedBusinessProcesses { get; set; }
        public string version { get; set; }
        public string logging { get; set; }
        public string supportingArchitecture { get; set; }
        public List<Endpoint> receiveEndpoints { get; set; }
        public List<Endpoint> sendEndpoints { get; set; }
        public Sla sla { get; set; }
    }
}
