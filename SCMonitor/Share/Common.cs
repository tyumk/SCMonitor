using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using SCCommon;

namespace SCMonitor
{
    public class Common
    {
        public const string JSON_MUTEX_NAME = "SCMonitor.ReportJsonMutex";
        public const string JSON_FILE_VPATH = "~/report.json";

        public class CollectData
        {
            public ReportData Data { get; set; } = new ReportData();
            public DateTime CollectTime { get; set; } = DateTime.Now;

            public void UpdateReportData(ReportData data)
            {
                Data = data;
                CollectTime = DateTime.Now;
            }
        }
        public class CollectDataList
        {
            public List<CollectData> DataList { get; set; } = new List<CollectData>();
        }
    }
}