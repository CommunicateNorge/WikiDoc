using AzureStorage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wiki.Models;
using Wiki.Utilities;

namespace ScheduledUploader
{
    class BTDocumenterUploader
    {
        public void StartDocumenterAndUploadToBlob(StringBuilder log, AzureBlobStorage azureStorage, BizTalk currentBTInst, EnvironmentInfo info)
        {
                string documenterPath = Path.Combine(@"C:\Program Files (x86)\Microsoft Services\BizTalk Documenter 2013R2", "Microsoft.Services.Tools.BiztalkDocumenter.exe");

                var proc = System.Diagnostics.Process.Start(documenterPath, "/def");

                Process[] documenterProcess = Process.GetProcessesByName("Microsoft.Services.Tools.BiztalkDocumenter");

                Stopwatch s = new Stopwatch();
                s.Start();
                documenterProcess.FirstOrDefault().WaitForExit();
                while (!documenterProcess.FirstOrDefault().HasExited)
                {
                    Thread.Sleep(new TimeSpan(0, 0, 1));
                }
                s.Stop();

                log.AppendLine("Documenter Done! Time used: " + s.Elapsed.ToString());

                string tempPath = System.IO.Path.GetTempPath();
                log.AppendLine("Path to temp folder = " + tempPath);

                string[] files = Directory.GetFiles(tempPath, "*.chm", SearchOption.AllDirectories);

                FileInfo newest = null;

                foreach (var item in files)
                {
                    FileInfo currentFile = new FileInfo(item);
                    if (newest == null || newest.CreationTime < currentFile.CreationTime)
                    {
                        newest = currentFile;
                    }
                }

                log.AppendLine("Newest file found: " + newest.FullName);

                Thread.Sleep(new TimeSpan(0, 0, 2));

                Process[] hhProcess = Process.GetProcessesByName("hh");

                if (hhProcess.FirstOrDefault() != null)
                    hhProcess.FirstOrDefault().Kill();

                log.AppendLine("Uploading to Blob");

                string res = azureStorage.SetBlobContentToFile(WikiBlob.Combine("File", "BTDocumenter", currentBTInst.Name, info.Name), newest);

                log.AppendLine("Uploaded! " + res);
            }
    }
}
