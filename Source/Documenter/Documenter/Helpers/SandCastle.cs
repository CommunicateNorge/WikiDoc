using AzureStorage;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Wiki.Utilities;
using static Wiki.Models.Constants;

namespace Documenter
{
    public class SandCastleUtility
    {

    }

    class SandCastleUploader
    {
        public AzureBlobStorage Storage { get; set; }
        public string BlobBaseKey { get; set; }
        public String EntryPointBlobKey { get; set; }
        public string HelpFolderPath { set; get; }
        public string AppName { set; get; }


        public string AppFTPAddress { set; get; }
        private NetworkCredential Creds;
        private int count = 0;

        public SandCastleUploader(string name, string solutionName, string sandCastleHelpFolder, AzureBlobStorage storage)
        {
            this.AppName = solutionName;
            this.BlobBaseKey = WikiBlob.Combine("SandCastle", name, solutionName);
            this.Storage = storage;
            HelpFolderPath = sandCastleHelpFolder;

        }

        public String ParseAndUploadHelpFiles()
        {
            String htmlPath = Path.Combine(HelpFolderPath, "html");
            String title = WikiBlob.GetFriendlyName(AppName) + " C# Code Documentation";

            String indexFilePath = Path.Combine(HelpFolderPath, "index.html");
            String indexFileContent = File.ReadAllText(indexFilePath);
            Match entryPointMatch = Regex.Match(indexFileContent, @"href=""html/([a-z0-9]{8}\-[a-z0-9]{4}\-[a-z0-9]{4}\-[a-z0-9]{4}\-[a-z0-9]{12})\.htm""");
            EntryPointBlobKey = WikiBlob.Combine(BlobBaseKey, entryPointMatch.Groups[1].Value.ToString());

            #region Ignore
            //foreach (var item in Directory.EnumerateFiles(HelpPath, "index.html"))
            //{
            //    String content = File.ReadAllText(item);
            //    content = Regex.Replace(content, @"href=""html/([a-z0-9]{8}\-[a-z0-9]{4}\-[a-z0-9]{4}\-[a-z0-9]{4}\-[a-z0-9]{12})\.htm""", delegate(Match match)
            //    {
            //        return "href=\"" + BlobBaseKey.BlobKeyCombine(match.Groups[1].Value.ToString()) + "\"";
            //    });

            //MatchCollection mc = Regex.Matches(content, @"href=""([a-zA-Z0-9\-_\\\.£€]+)""");
            //foreach (var c in mc)
            //{
            //    Console.WriteLine(c.ToString());
            //}
            //    Match m = Regex.Match(content, @"href=""([a-zA-Z0-9\-_\\\.£€]+)""");

            //    content = content.Replace("src=\"../icons/", "src=\"/Content/Icons/SandCastle/");
            //    content = content.Replace("A Sandcastle Documented Class Library", title);
            //    content = content.Replace(@"<form id=""SearchForm"" method=""get"" action=""#"" onsubmit=""javascript:TransferToSearchPage(); return false;""><input id=""SearchTextBox"" type=""text"" maxlength=""200"" /><button id=""SearchButton"" type=""submit""></button></form>", "");
            //    if (content.Contains("<body>"))
            //        content = content.ExtractPart("<body>", "</body>");
            //    else if (content.Contains("<body onload=\"OnLoad('cs')\">"))
            //        content = content.ExtractPart("<body onload=\"OnLoad('cs')\">", "</body>");
            //    else if (content.Contains("<body onload=\"OnSearchPageLoad();\">"))
            //        content = content.ExtractPart("<body onload=\"OnSearchPageLoad();\">", "</body>");

            //    content = content.Surround("<div class=\"SandCastle\">", "</div>");

            //    String key = BlobBaseKey.BlobKeyCombine(Path.GetFileNameWithoutExtension(item));
            //    Storage.SetBlobContentAsString(key, content);
            //}
            #endregion

            foreach (var item in Directory.EnumerateFiles(htmlPath, "*.htm"))
            {
                String currentFileName = Path.GetFileNameWithoutExtension(item);
                String content = File.ReadAllText(item);
                content = Regex.Replace(content, @"href=""([a-z0-9]{8}\-[a-z0-9]{4}\-[a-z0-9]{4}\-[a-z0-9]{4}\-[a-z0-9]{12})\.htm""", delegate(Match match)
                {
                    return "href=\"" + WikiBlob.Combine(BlobBaseKey, match.Groups[1].Value.ToString()) + "\"";
                });

                content = content.Replace("A Sandcastle Documented Class Library", title);
                content = content.Replace("src=\"../icons/", "src=\"/Content/Icons/SandCastle/");
                content = content.Replace(@"<form id=""SearchForm"" method=""get"" action=""#"" onsubmit=""javascript:TransferToSearchPage(); return false;""><input id=""SearchTextBox"" type=""text"" maxlength=""200"" /><button id=""SearchButton"" type=""submit""></button></form>", "");
                if (content.Contains("<body>"))
                    content = content.ExtractPart("<body>", "</body>");
                else if (content.Contains("<body onload=\"OnLoad('cs')\">"))
                    content = content.ExtractPart("<body onload=\"OnLoad('cs')\">", "</body>");
                else if (content.Contains("<body onload=\"OnSearchPageLoad();\">"))
                    content = content.ExtractPart("<body onload=\"OnSearchPageLoad();\">", "</body>");

                content = content.Surround("<div class=\"SandCastle\">", "</div>");

                String key = WikiBlob.Combine(BlobBaseKey, currentFileName);
                Storage.SetBlobContentAsString(key, content);
            }

            return EntryPointBlobKey;
        }

        [Obsolete]
        public void StartUpload()
        {
            Console.Write("  > " + count);
            FtpMkdir(AppFTPAddress);
            UploadContent(HelpFolderPath, AppFTPAddress);
        }

        [Obsolete]
        private void FtpUpload(string ftpPath, string localPath)
        {
            using (WebClient client = new WebClient())
            {
                client.Credentials = Creds;
                client.UploadFile(ftpPath, "STOR", localPath);
            }
            Console.Write("\r  > " + count++);
        }

        [Obsolete]
        private void FtpMkdir(string ftpPath)
        {
            try
            {
                WebRequest req = FtpWebRequest.Create(ftpPath);
                req.Method = WebRequestMethods.Ftp.MakeDirectory;
                req.Credentials = Creds;
                req.GetResponse();
                Console.Write("\r  > " + count++);
            }
            catch (Exception ex)
            {
                //Ignore, exception means dir exists
            }
        }

        [Obsolete]
        private void UploadContent(string dirPath, string uploadPath)
        {
            string[] files = Directory.GetFiles(dirPath, "*.*");
            string[] subDirs = Directory.GetDirectories(dirPath);

            foreach (string file in files)
            {
                FtpUpload(uploadPath + "/" + Path.GetFileName(file), file);
            }

            foreach (string subDir in subDirs)
            {
                FtpMkdir(uploadPath + "/" + Path.GetFileName(subDir));
                UploadContent(subDir, uploadPath + "/" + Path.GetFileName(subDir));
            }
        }

    }
}
