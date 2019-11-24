using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.IO;
using System.Threading;
using SCCommon;
using Newtonsoft.Json;

namespace SCMonitor.Models
{
    public class MonitorModel
    {
        public Common.CollectDataList data = new Common.CollectDataList();
        private static Mutex mutex = new Mutex(false, Common.JSON_MUTEX_NAME);
        public int unknownStatusThresholdMinutes = 60;

        public MonitorModel(string filePath)
        {
            string method = "MonitorModel.MonitorModel";
            bool hasHandle = false;

            string value = System.Configuration.ConfigurationManager.AppSettings["UnknownStatusThresholdMinutes"];
            if (!int.TryParse(value, out unknownStatusThresholdMinutes))
            {
                SCTracer.Error(method, "UnknownStatusThresholdMinutes: failed to parse " + value + " to int.");
            }

            try
            {
                hasHandle = mutex.WaitOne();
                if (File.Exists(filePath))
                {
                    data = JsonConvert.DeserializeObject<Common.CollectDataList>(File.ReadAllText(filePath));
                }
            }
            catch (Exception e)
            {
                SCTracer.Exception(method, e);
                throw;
            }
            finally
            {
                if (hasHandle)
                {
                    mutex.ReleaseMutex();
                }
            }
        }
    }
}