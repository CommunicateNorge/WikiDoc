using AzureStorage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Wiki.Models;
using Wiki.Utilities;
using static Wiki.Models.Constants;

namespace Documenter
{
    public class DbDocumenter : IDocumenter
    {
        public String DocRootDir { get; set; }
        public String DbConnStr { get; set; }
        public String Name { get; set; }
        public AzureBlobStorage Storage { get; set; }
        public EnvironmentInfo EnvInfo { get; set; }

        private string _key;
        public String Key
        {
            get
            {
                if (_key == null)
                    _key = "SQL£Db£Schema£" + WikiBlob.SanitizeV2(Name);
                return _key;
            }
        }
        public String Link(String className = null)
        {
            return WikiBlob.GetAsWikiLink(Key, false, Name, className);
        }

        public DbDocumenter(String dbConnStr, String dbName, DirectoryInfo docdir, AzureBlobStorage storage, EnvironmentInfo envInfo)
        {
            this.DocRootDir = docdir.FullName;
            this.DbConnStr = dbConnStr;
            this.Storage = storage;
            this.Name = dbName;
            this.EnvInfo = envInfo;
        }

        public TimeSpan Start()
        {
            DLog.TraceEvent(TraceEventType.Information, DTEId, $"{Name}\\{EnvInfo.Name}: Indexing and documenting SQL Database");

            try
            {
               // throw new NotImplementedException("asd");
                FileInfo sqldocumenter = new FileInfo(Path.Combine(DocRootDir, "sqldbdoc", "sqldbdoc.exe"));

                FileInfo dbDocName = new FileInfo(Path.Combine(sqldocumenter.Directory.FullName, Name + ".htm"));

                Process process = new Process();
                process.StartInfo.FileName = sqldocumenter.FullName;
                process.StartInfo.Arguments = "\"" + DbConnStr + "\" \"" + dbDocName.FullName + "\" /y";
                process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                process.Start();
                process.WaitForExit();

                String content = TrimFileStartAndEnd(dbDocName, 33, 2).Insert(0, "<div id=\"sqldb\">").Append("</div>").ToString();
                SaveToAzure(content, Key);
            }
            catch (Exception ex)
            {
                DLog.TraceEvent(TraceEventType.Error, DTEId, $"{Name}\\{EnvInfo.Name}: Failed to document SQL Database {Name}. {ex.Message}");
                DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"{Name}\\{EnvInfo.Name}: Failed to document SQL Database {Name}. {ex.ToString()}");
                SaveToAzure($"<p>Failed to extract database metadata for {Name}</p>", Key);
            }
            return TimeSpan.FromMinutes(1.0);
        }

        [Obsolete("Name should be configured in the json app config instead.")]
        private void SetName()
        {
            Regex rex = new Regex(".*Database=(.*);integrated security=.*");
            Match m = rex.Match(DbConnStr);
            Name = m.Groups[1].Value;
        }

        public void SaveToAzure(String content, string key)
        {
            try
            {
                Storage.SetBlobContentAsString(key, content);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        public static StringBuilder TrimFileStartAndEnd(FileInfo file, int lineStart, int lineEnd = 0)
        {
            int count = lineStart;
            string[] lines = File.ReadAllLines(file.FullName);

            if (lineStart + lineEnd > lines.Length)
                throw new ArgumentException("Cannot remove more lines than exists in the file!");

            StringBuilder sb = new StringBuilder();

            while (count < (lines.Length - lineEnd))
            {
                sb.AppendLine(lines[count++]);
            }
            return sb;
        }

    }
}
