using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Wiki.Models;
using Wiki.Utilities;
using static Wiki.Models.Constants;

namespace Tfs.Integration
{
    public class TfsDownloader
    {
        public static String[] ProjectFileFilter = new String[] { ".csproj", ".sln", ".json", ".htm", ".html" };
        public static String[] BizTalkFileFilter = new String[] { ".xsd", ".xsl", ".btm", ".htm", ".html", ".xml" };

        public String UserName { get; set; }
        private String pwd;
        public String Domain { get; set; }

        public Uri TfsUri { get; set; }
        public String WorkingDir { get; set; }

        public TimeSpan CacheDuration { get; set; }

        public object Lock = new object();


        public DirectoryInfo WorkingDirInfo
        {
            get
            {
                return new DirectoryInfo(WorkingDir);
            }
        }

        public TfsDownloader(String username, String pwd, String domain, String uri, long cacheDurationMin = 0)
        {
            this.UserName = username;
            this.pwd = pwd;
            this.Domain = domain;
            this.TfsUri = new Uri(uri);
            this.CacheDuration = TimeSpan.FromMinutes(cacheDurationMin);
        }

        public TfsDownloader(ITfsInfo tfsInfo, long cacheDurationMin = 0)
        {
            this.UserName = tfsInfo.User;
            this.pwd = tfsInfo.Pwd;
            this.Domain = tfsInfo.Domain;
            this.TfsUri = new Uri(tfsInfo.TfsUri);
            this.CacheDuration = TimeSpan.FromMinutes(cacheDurationMin);
        }

        public static String GetTempSavePath(String tfsProjectPath, string name = null)
        {
            String envProjectFolder = tfsProjectPath.Split('/')?.LastOrDefault();
            string path = null;
            if (name == null)
                path = Path.Combine(Path.GetTempPath(), "DTFS", envProjectFolder ?? "");
            else
                path = Path.Combine(Path.GetTempPath(), "DTFS", name, envProjectFolder ?? "");
            return path;
        }

        public void DownloadBizTalkProjects(String tfsProjectPath, String btName)
        {
            String targetPath = GetTempSavePath(tfsProjectPath, btName.Safe());
            IEnumerable<String> fileTypes = Enumerable.Concat(ProjectFileFilter, BizTalkFileFilter);
            DownloadSourceFiles(fileTypes, tfsProjectPath, targetPath);
        }

        public void DownloadCSharp(String tfsProjectPath, String name, bool onlyProjectFiles = true)
        {
            String targetPath = GetTempSavePath(tfsProjectPath, name.Safe());
            DownloadSourceFiles(ProjectFileFilter, tfsProjectPath, targetPath);
        }

        public void CleanUp()
        {
            try
            {
                Directory.Delete(WorkingDir, true);
            }
            catch (Exception ex)
            {
                DLog.TraceEvent(TraceEventType.Error, DTEId, $"Failed to clear temporary source files from \"{WorkingDir}\".{ex.Message}");
                DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"Failed to clear temporary source files from \"{WorkingDir}\".\r\n{ex.ToString()}");
            }
        }

        public String DownloadSourceFiles(IEnumerable<String> fileTypes, String tfsProjectPath, String targetSavePath = null)
        {
            ICredentials networkCredential = new NetworkCredential(UserName, pwd, Domain);
            TfsTeamProjectCollection tfs = new TfsTeamProjectCollection(TfsUri, networkCredential);

            DLog.TraceEvent(TraceEventType.Information, DTEId, $"Fetching source from \"{tfsProjectPath}\"");

            int removeFrom = tfsProjectPath.LastIndexOf('/') + 1;
            string sourcePathParent = tfsProjectPath.Remove(removeFrom);

            WorkingDir = targetSavePath ?? GetTempSavePath(tfsProjectPath, Guid.NewGuid().ToString().Substring(0, 8));

            DLog.TraceEvent(TraceEventType.Information, DTEId, $"Saving source to \"{WorkingDir}\"");

            VersionControlServer sourceControl = (VersionControlServer)tfs.GetService(typeof(VersionControlServer));
            ItemSet items = sourceControl.GetItems(tfsProjectPath, VersionSpec.Latest, RecursionType.Full);

            int citems = 0;
            int cfiles = 0;
            int totalItems = items.Items.Count();

            DateTime maxAge = DateTime.Now - CacheDuration;
            Stopwatch sw = new Stopwatch();
            sw.Start();

            Console.Write($"Fetching {citems}");

            Parallel.ForEach<Item>(items.Items, (item) =>
            {
                int i = 0;
                lock (Lock)
                {
                    citems++;
                    i = citems;
                }
                string savePath = Path.Combine(WorkingDir, item.ServerItem.Replace(tfsProjectPath, "").TrimStart('/'));
                string newPath = Path.Combine(WorkingDir, savePath);

                if (item.ItemType == ItemType.File && fileTypes.Contains(Path.GetExtension(item.ServerItem)))
                {

                    FileInfo f = new FileInfo(newPath);
                    f.Directory.Create();

                    if (!File.Exists(newPath) || (File.GetCreationTime(newPath) < item.CheckinDate && File.GetCreationTime(newPath) < maxAge))
                    {
                        item.DownloadFile(newPath);
                        lock(Lock)
                        {
                            cfiles++;
                        }
                    }
                }
                else if (item.ItemType == ItemType.Folder)
                {
                    if (!Directory.Exists(newPath))
                        Directory.CreateDirectory(newPath);
                }
                if (i % 30 == 0)
                    Console.Write("\rFetching " + i + "/" + totalItems);
            });
            Console.Write("\rFetching " + citems + "/" + totalItems);
            Console.WriteLine();
            sw.Stop();
            DLog.TraceEvent(TraceEventType.Information, DTEId, $"Downloaded {cfiles} files at {cfiles / sw.Elapsed.TotalSeconds } f/sec. The rest were up to date or folders.");
            return WorkingDir;
        }
    }
}

//Task[] downloads = new Task[30];
//foreach (Item item in items.Items)
//{
//    // Replace the server parent path with local parent path
//    string savePath = Path.Combine(WorkingDir, item.ServerItem.Replace(tfsProjectPath, "").TrimStart('/'));
//    string newPath = Path.Combine(WorkingDir, savePath);

//    //Console.WriteLine($"Downloading {item.ServerItem} to {savePath}");
//    if (item.ItemType == ItemType.File && fileTypes.Contains(Path.GetExtension(item.ServerItem)))
//    {
//        if (!File.Exists(newPath) || File.GetCreationTime(newPath) < maxAge)
//        {
//            c++;
//            downloads[i++] = Task.Run(() =>
//            {
//                item.DownloadFile(newPath);
//            });
//        }
//    }
//    else if (item.ItemType == ItemType.Folder)
//        if (!Directory.Exists(newPath))
//            Directory.CreateDirectory(newPath);

//    if (i >= 30)
//    {
//        Task.WhenAll(downloads).GetAwaiter().GetResult();
//        i = 0;
//        DLog.TraceEvent(TraceEventType.Information, DTEId, $"Downloaded {c} files");
//    }
//}
//if (i > 0)
//    Task.WhenAll(downloads).GetAwaiter().GetResult();