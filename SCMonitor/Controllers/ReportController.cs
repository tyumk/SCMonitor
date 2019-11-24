using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Hosting;
using System.IO;
using Newtonsoft.Json;
using SCCommon;
using System.Threading;

namespace SCMonitor.Controllers
{
    public class ReportController : ApiController
    {
        private static Mutex mutex = new Mutex(false, Common.JSON_MUTEX_NAME);

        // GET api/<controller>
        public HttpResponseMessage Get()
        {
            string method = "ReportController.Get";
            SCTracer.Info(method, "Start.");
            bool hasHandle = false;

            try
            {
                string reportPath = HostingEnvironment.MapPath(Common.JSON_FILE_VPATH);
                string data;

                hasHandle = mutex.WaitOne();
                if (File.Exists(reportPath))
                {
                    data = File.ReadAllText(reportPath);
                }
                else
                {
                    data = JsonConvert.SerializeObject(new Common.CollectDataList());
                }
                mutex.ReleaseMutex();
                hasHandle = false;

                HttpResponseMessage response = Request.CreateResponse(HttpStatusCode.OK);
                response.Content = new StringContent(data, System.Text.Encoding.UTF8, @"application/json");

                SCTracer.Info(method, "End.");
                return response;
            }
            catch (Exception e)
            {
                SCTracer.Error(method, "Unexpected Error Occurred.");
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

        // POST api/<controller>
        public HttpResponseMessage Post([FromBody]ReportData reportData)
        {
            string method = "ReportController.Post";
            SCTracer.Info(method, string.Format("Start. {0}, {1}", reportData.ReporterName, reportData.ReporterHostName));
            bool hasHandle = false;

            try
            {
                // 送信されたデータでreport.jsonを更新
                string reportPath = HostingEnvironment.MapPath(Common.JSON_FILE_VPATH);
                Common.CollectDataList data;

                // ファイル操作のためにミューテックス取る (※ファイルの排他取るだけでもいいのでは？)
                hasHandle = mutex.WaitOne();
                if (File.Exists(reportPath))
                {
                    data = JsonConvert.DeserializeObject<Common.CollectDataList>(File.ReadAllText(reportPath));
                }
                else
                {
                    SCTracer.Info(method, "Not found ." + reportPath);
                    data = new Common.CollectDataList();
                }

                // 古いデータ(ホスト名が一致)があれば更新、なければ新規追加
                int oldDataIndex = data.DataList.FindIndex(x => x.Data.ReporterHostName == reportData.ReporterHostName);
                if (oldDataIndex < 0)
                {
                    SCTracer.Info(method, "Add new information.");
                    data.DataList.Add(new Common.CollectData() { Data = reportData });
                }
                else
                {
                    SCTracer.Info(method, "Update old information.");
                    data.DataList[oldDataIndex].UpdateReportData(reportData);
                }
                File.WriteAllText(reportPath, JsonConvert.SerializeObject(data));

                SCTracer.Info(method, "End.");
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            catch (Exception e)
            {
                SCTracer.Error(method, "Unexpected Error Occurred.");
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