using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tfs.Integration
{
    public class Example
    {
        public static void Test()
        {
            String[] fileTypes = new String[] { ".dll", ".exe" };
            string projectPath = @"$/REMA1000/ESB/Main/Other";

            TfsDownloader tfsFiles = new TfsDownloader("martin.lokkeberg", "pwd here", "rema.no", "https://rema.visualstudio.com:443/");
            tfsFiles.DownloadSourceFiles(fileTypes, projectPath);
            tfsFiles.CleanUp();
        }
    }
}
