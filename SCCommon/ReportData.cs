using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace SCCommon
{
    public class ReportData
    {
        public string ReporterName { get; set; } = string.Empty;
        public string ReporterHostName { get; set; } = string.Empty;
        public List<LoginSession> Sessions { get; set; } = new List<LoginSession>();
        public List<InstalledSoftware> Softwares { get; set; } = new List<InstalledSoftware>();

        public class LoginSession
        {
            public string UserName { get; set; } = string.Empty;
            public string SessionName { get; set; } = string.Empty;
            public string ClientName { get; set; } = string.Empty;
        }

        public class InstalledSoftware
        {
            public string SoftwareName { get; set; } = string.Empty;
            public string Version { get; set; } = string.Empty;
        }
    }

    
}
