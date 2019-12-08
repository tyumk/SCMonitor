using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.Win32;
using System.Net;
using System.Diagnostics;
using System.IO;
using System.Reflection;

using SCCommon;
using Newtonsoft.Json;

namespace SCReporter
{
    class Reporter
    {
        public enum ReportResult
        {
            Success = 0,
            UnexpectedError = 1,
            CommunicateServerFailed = 2,
            FailQueryUserCmd = 3,
        }

        public class UninstallInfo
        {
            public string DisplayName { get; set; }
            public string DisplayVersion { get; set; }
        }

        static int Main(string[] args)
        {
            string method = "Reporter.Main";
            SCTracer.Info(method, "Start.");
            try
            {
                // 送信データ1: Reporter実行マシンの情報: configファイルから設定を読み取る
                string reporterName = System.Configuration.ConfigurationManager.AppSettings["ReporterName"];
                string destinationURL = System.Configuration.ConfigurationManager.AppSettings["DestinationURL"];
                ReportData reportData = new ReportData();
                reportData.ReporterName = reporterName;
                reportData.ReporterHostName = System.Net.Dns.GetHostName();

                // 送信データ2: ログインセッション情報
                List<ReportData.LoginSession> sessions;
                ReportResult result = GetLoginSessions(out sessions);
                if (result != ReportResult.Success)
                {
                    SCTracer.Error(method, "GetLoginSessions failed. result = " + result);
                    return (int)result;
                }
                reportData.Sessions = sessions;

                // 送信データ3：インストール済みソフトウェアを取得
                string checkSoftwareNames = System.Configuration.ConfigurationManager.AppSettings["CheckSoftwareNames"];
                reportData.Softwares = GetInstalledSoftwares(checkSoftwareNames.Split(new char[]{ ',' }, StringSplitOptions.RemoveEmptyEntries));

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

        #region ログインSessionデータの作成
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

        static ReportResult GetLoginSessions(out List<ReportData.LoginSession> sessions)
        {
            string method = "Reporter.GetLoginSessions";
            SCTracer.Info(method, "Start.");
            sessions = new List<ReportData.LoginSession>();

            // 後のbat実行のために、exeのパスに移動しておく
            string currentDirectory = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location));

            // query user 実行
            string queryResult;
            string queryError;
            using (Process proc = new Process())
            {
                proc.StartInfo.FileName = "query_user.bat";
                proc.StartInfo.CreateNoWindow = true;
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.RedirectStandardError = true;

                proc.Start();
                queryResult = proc.StandardOutput.ReadToEnd();
                queryError = proc.StandardError.ReadToEnd();
                proc.WaitForExit();
                SCTracer.Info(method, queryResult);
                if (proc.ExitCode != 1)  // batファイルが見つからないなどの異常時、1以外になる
                {
                    SCTracer.Error(method, "query_result.bat exit with errorcode = " + proc.ExitCode + ". return " + ReportResult.FailQueryUserCmd);
                    return ReportResult.FailQueryUserCmd;
                }
                if (string.IsNullOrWhiteSpace(queryResult))
                {
                    SCTracer.Error(method, "query_result.bat failed. StandardOutput is empty. return " + ReportResult.FailQueryUserCmd);
                    SCTracer.Error(method, "StandardError:\n" + queryError);
                    return ReportResult.FailQueryUserCmd;
                }
            }
            string[] queryResultLines = queryResult.Split('\n');


            for (int i = 1; i < queryResultLines.Length; i++) // 1行目はヘッダのためスキップ
            {
                string[] userData = queryResultLines[i].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (userData.Length < 2)
                {
                    // 不正行はスキップ
                    continue;
                }
                if (!userData[1].StartsWith("console") && !userData[1].StartsWith("rdp"))
                {
                    // RDP切断後など、Session名が空の場合対策
                    continue;
                }
                if (userData[0].StartsWith(">"))
                {
                    // カレントセッションのユーザー名先頭には > が付く
                    userData[0] = userData[0].Substring(1);
                }
                SCTracer.Info(method, "add session: " + userData[0] + ", " + userData[1]);
                sessions.Add(new ReportData.LoginSession() { UserName = userData[0], SessionName = userData[1] });
            }

            // レジストリからCLIENTNAMEを探す
            foreach (string subkey in Registry.Users.GetSubKeyNames())
            {
                RegistryKey key = Registry.Users.OpenSubKey(subkey + @"\Volatile Environment");
                if (key == null || key.SubKeyCount == 0)
                {
                    continue;
                }
                RegistryKey volatileEnvironment = key.OpenSubKey(key.GetSubKeyNames()[0]);  // 1個しかないはずなので決め打ち
                string userName = (string)key.GetValue("USERNAME");
                string clientName = (string)volatileEnvironment.GetValue("CLIENTNAME");
                string sessionName = (string)volatileEnvironment.GetValue("SESSIONNAME");

                ReportData.LoginSession session = sessions.Find(x => x.UserName.ToLower() == userName.ToLower());
                if (session != null)
                {
                    SCTracer.Info(method, "found login session: SID = " + subkey);
                    session.ClientName = clientName;
                    SCTracer.Info(method, string.Format("userName = {0}, clientName = {1}, sessionName = {2}", userName, clientName, sessionName));
                }
            }

            Directory.SetCurrentDirectory(currentDirectory);
            SCTracer.Info(method, "End.");
            return ReportResult.Success;
        }
        #endregion

        #region インストール済みソフトウェア一覧の作成
        static List<ReportData.InstalledSoftware> GetInstalledSoftwares(string[] softwareNames)
        {
            string method = "Reporter.GetInstalledSoftwares";
            SCTracer.Info(method, "Start. Softwares=[" + string.Join("],[", softwareNames) + "]");

            List<ReportData.InstalledSoftware> ret = new List<ReportData.InstalledSoftware>();

            // レジストリからアンインストール可能なソフトウェア一覧の取得
            List<ReportData.InstalledSoftware> uninstallList = new List<ReportData.InstalledSoftware>();
            uninstallList.AddRange(GetUinstallListFromRegistry(true));
            uninstallList.AddRange(GetUinstallListFromRegistry(false));

            // softwareNames で探索
            ReportData.InstalledSoftware findSoftware;
            foreach (string item in softwareNames)
            {
                SCTracer.Info(method, "find " + item + " ...");
                // configに記載されたソフトウェア名があるか調べる
                findSoftware = uninstallList.Find(x => x.SoftwareName.Trim().ToLower() == item.Trim().ToLower());
                if (findSoftware != null)
                {
                    SCTracer.Info(method, "FOUND: version = " + findSoftware.Version);
                    ret.Add(findSoftware);
                }
            }

            SCTracer.Info(method, "End.");
            return ret;
        }

        static List<ReportData.InstalledSoftware> GetUinstallListFromRegistry(bool is32bit)
        {
            List<ReportData.InstalledSoftware> uninstallList = new List<ReportData.InstalledSoftware>();

            RegistryKey baseKey;
            if (is32bit)
            {
                baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
            }
            else
            {
                // これはプロセスが64bitじゃないと、RegistryView.Registry32と同じ結果を取ってくる
                baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            }
            RegistryKey uninstallKey = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            if (uninstallKey != null)
            {
                foreach (string subKey in uninstallKey.GetSubKeyNames())
                {
                    RegistryKey appkey = uninstallKey.OpenSubKey(subKey, false);
                    string appName = (string)appkey.GetValue("DisplayName") ?? subKey;
                    string appVersion = (string)appkey.GetValue("DisplayVersion") ?? string.Empty;
                    uninstallList.Add(new ReportData.InstalledSoftware() { SoftwareName = appName, Version = appVersion });
                }
            }
            return uninstallList;
        }

        #endregion

        #region Jsonのサーバーに送信
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
        #endregion
    }
}
