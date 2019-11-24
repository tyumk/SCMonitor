using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SCCommon;
using Newtonsoft.Json;
using System.Net.Http;
using Microsoft.Win32;
using System.Net;

namespace SCReporter
{
    class Reporter
    {
        public enum ReportResult
        {
            Success = 0,
            UnexpectedError = 1,
            CommunicateServerFailed = 2,
        }

        static int Main(string[] args)
        {
            string method = "Reporter.Main";
            SCTracer.Info(method, "Start.");
            try
            {
                // configファイルから設定を読み取る
                string reporterName = System.Configuration.ConfigurationManager.AppSettings["ReporterName"];
                string destinationURL = System.Configuration.ConfigurationManager.AppSettings["DestinationURL"];

                // レジストリ情報を収集する
                ReportData reportData = new ReportData();
                reportData.ReporterName = reporterName;
                reportData.ReporterHostName = System.Net.Dns.GetHostName();
                foreach (string subkey in Registry.Users.GetSubKeyNames())
                {
                    RegistryKey key = Registry.Users.OpenSubKey(subkey + @"\Volatile Environment");
                    if (key == null || key.SubKeyCount == 0)
                    {
                        continue;
                    }
                    SCTracer.Info(method, "found login session: SID = " + subkey);
                    RegistryKey volatileEnvironment = key.OpenSubKey(key.GetSubKeyNames()[0]);  // 1個しかないはずなので決め打ち
                    string userName = (string)key.GetValue("USERNAME");
                    string clientName = (string)volatileEnvironment.GetValue("CLIENTNAME");
                    string sessionName = (string)volatileEnvironment.GetValue("SESSIONNAME");
                    string ipAddress = HostToIPAddress(clientName);
                    SCTracer.Info(method, string.Format("userName = {0}, clientName = {1}, sessionName = {2}, ipAddress = {3}", userName, clientName, sessionName, ipAddress));

                    // RDPの場合は、本当にコネクションがあるか調べる。(切断してもレジストリに値が残り続けるため。)
                    if (sessionName.ToLower().StartsWith("rdp"))
                    {
                        int rdpPort = 3389;
                        RegistryKey rdpKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Terminal Server\WinStations\RDP-Tcp");
                        if (rdpKey != null)
                        {
                            int portTemp = (int)rdpKey.GetValue("PortNumber");
                            if (portTemp > 0)
                            {
                                SCTracer.Info(method, "Get rdp-port from Registry.");
                                rdpPort = portTemp;
                            }
                        }

                        var connections = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections();
                        var connectInfo = connections.Where(x => x.LocalEndPoint.Port == rdpPort && x.State == System.Net.NetworkInformation.TcpState.Established)
                            .FirstOrDefault(x => x.RemoteEndPoint.Address.ToString().Trim().ToLower() == ipAddress.Trim().ToLower());
                        if (connectInfo == null)
                        {
                            SCTracer.Info(method, string.Format("Not found RDP connection on Port={0}, from {1}. Skip.", rdpPort, clientName));
                            continue;
                        }
                    }

                    // ホスト名(IPアドレス)の形式にして送信。
                    if (!string.IsNullOrWhiteSpace(ipAddress))
                    {
                        clientName = string.Format("{0}({1})", clientName, ipAddress);
                    }

                    reportData.Sessions.Add(new ReportData.LoginSession() { ClientName =  clientName, SessionName = sessionName, UserName = userName});
                }

                // 送信データをJsonにシリアライズし、送信する
                string json = JsonConvert.SerializeObject(reportData);
                try
                {
                    ReportJson(json, destinationURL).Wait();
                }
                catch (Exception e)
                {
                    SCTracer.Error(method, "Reporting Failed. Need Retry Again. return " + ReportResult.CommunicateServerFailed);
                    SCTracer.Exception(method, e);
                    return (int)ReportResult.CommunicateServerFailed;
                }
            }
            catch (Exception e)
            {
                SCTracer.Error(method, "Unexpected Error Occured. return " + ReportResult.UnexpectedError);
                SCTracer.Exception(method, e);
                return (int)ReportResult.UnexpectedError;
            }

            SCTracer.Info(method, "End.");
            return (int)ReportResult.Success;
        }

        static string HostToIPAddress(string hostname)
        {
            string method = "Reporter.HostToIPAddress";
            SCTracer.Info(method, "Start. hostname = " + hostname);
            try
            {
                // Consoleログインで、ホスト名が空文字とかだと、ローカルマシンのIPを返してしまう。
                if (string.IsNullOrWhiteSpace(hostname))
                {
                    SCTracer.Info(method, "End. Skip blank hostname.");
                    return string.Empty;
                }

                IPHostEntry ipInfo = Dns.GetHostEntry(hostname);
                if (ipInfo.AddressList != null && ipInfo.AddressList.Length > 0)
                {
                    IPAddress result = ipInfo.AddressList.FirstOrDefault(x => x.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                    if (result != null)
                    {
                        SCTracer.Warn(method, "End. result: " + result.ToString());
                        return result.ToString();
                    }
                    else
                    {
                        SCTracer.Warn(method, "Not found IPv4 Address..");
                    }
                }
                else
                {
                    SCTracer.Warn(method, "Not found AddressList.");
                }

                return string.Empty;
            }
            catch (Exception e)
            {
                SCTracer.Exception(method, e);
                return string.Empty;
            }
        }

        async static Task ReportJson(string json, string url)
        {
            string method = "Reporter.ReportJson";
            SCTracer.Info(method, "Start. url = " + url + " json = " + json);

            using (HttpClient client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromMilliseconds(10000);
                var result = await client.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));
                if (!result.IsSuccessStatusCode)
                {
                    SCTracer.Error(method, "Report Failed. status = " + (int)result.StatusCode);
                    SCTracer.Error(method, result.Content.ReadAsStringAsync().Result);
                    throw new Exception(string.Format("Failed to report to server.({0})", (int)result.StatusCode));
                }
            }

            SCTracer.Info(method, "End.");
        }
    }
}
