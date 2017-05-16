using AzureStorage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tfs.Integration;
using Wiki.Models;
using static Wiki.Models.Constants;

namespace Documenter
{
    public class AutoDocumenter
    {
        public static WikiConfiguration Config
        {
            get
            {
                return WikiConfiguration.GetConfiguration();
            }
        }
        private WikiMenuGenerator _menu;
        public WikiMenuGenerator Menu
        {
            get
            {
                if (_menu == null)
                    _menu = new WikiMenuGenerator();
                return _menu;
            }
        }

        public static AzureBlobStorage GetPrimaryStorage(EnvironmentInfo envInfo = null)
        {
            return StorageHelper.GetPrimaryStorage(envInfo);
        }

        public static AzureBlobStorage GetSecondaryStorage(EnvironmentInfo envInfo = null)
        {
            return StorageHelper.GetSecondaryStorage(envInfo);
        }

        public void Start()
        {
            DLog.TraceEvent(TraceEventType.Information, DTEId, $"Starting documenter");
            DocumentBizTalk();
            DocumentApiServices();
            DocumentDatabases();
            DocumentWebJobs();
            DLog.TraceEvent(TraceEventType.Information, DTEIdM, $"Creating Menu Index");
            Menu.CreateMenu(GetPrimaryStorage());
            DLog.TraceEvent(TraceEventType.Information, DTEIdM, $"Scaffolding Templates");
            ScaffoldTemplates();
            DLog.TraceEvent(TraceEventType.Information, DTEIdM, $"Documenter finished");
        }

        #region WebJobs
        private void DocumentWebJobs()
        {
            foreach (WebJob wj in Config.WebJobs)
            {
                WebJobMenuItem header = Menu.AddWebJobHeader(wj);

                foreach (Wiki.Models.Environment env in wj.Environments)
                {
                    header.AddDocumentedEnvironment(DocumentWebJobEnvironment(wj, env));
                }
            }
        }

        private WebJobDocumenter DocumentWebJobEnvironment(WebJob wj, Wiki.Models.Environment env)
        {
            EnvironmentInfo envInfo = Config.GetEnvironment(env);
            DLog.TraceEvent(TraceEventType.Information, DTEIdM, $"Documenting WebJob: {wj.Name} - {envInfo.Name}");

            WebJobDocumenter d = null;

            if (wj.UseTFS())
            {
                TfsDownloader downloader = new TfsDownloader(wj, Config.TfsCacheDurationMin);
                downloader.DownloadCSharp(env.Address, $"WebJob_{wj.Name}_{envInfo.Name}");
                d = new WebJobDocumenter(downloader.WorkingDirInfo, wj.Name, GetPrimaryStorage(envInfo), envInfo);
                d.Start();
            }
            else
            {
                d = new WebJobDocumenter(new DirectoryInfo(env.Address), envInfo.Name, GetPrimaryStorage(envInfo), envInfo);
                d.Start();
            }
            return d;
        }
        #endregion

        //TODO: fix BTDocumenter retrieval
        #region BizTalk
        private BizDocumenter DocumentBizTalkEnvironment(BizTalk bt, Wiki.Models.Environment env)
        {
            EnvironmentInfo envInfo = Config.GetEnvironment(env);

            DLog.TraceEvent(TraceEventType.Information, DTEIdM, $"Documenting BizTalk: {bt.Name} - {envInfo.Name}");

            BizDocumenter d = null;
            String pth = null;

            if (bt.UseTFS())
            {
                TfsDownloader downloader = new TfsDownloader(bt, Config.TfsCacheDurationMin);
                downloader.DownloadBizTalkProjects(env.Address, $"BT_{bt.Name}_{envInfo.Name}");
                pth = downloader.WorkingDirInfo?.FullName;
                d = new BizDocumenter(downloader.WorkingDirInfo, Config.DocDir, envInfo, false, GetPrimaryStorage(envInfo), bt);
                d.Start();
            }
            else
            {
                pth = env.Address;
                d = new BizDocumenter(new DirectoryInfo(env.Address), Config.DocDir, envInfo, false, GetPrimaryStorage(envInfo), bt);
                d.Start();
            }

            if(bt.UploadLogTexts)
            {
                IDocumenter logTextDocumenter = new SplunkLogTextDocumenter(GetPrimaryStorage(envInfo), envInfo, pth, bt);
                logTextDocumenter.Start();
            }
            return d;
        }

        private void DocumentBizTalk()
        {
            foreach (BizTalk bt in Config.BizTalk)
            {
                BizTalkMenuItem btHeader = Menu.AddBizTalkHeader(bt);

                foreach (Wiki.Models.Environment env in bt.Environments)
                {
                    btHeader.AddDocumentedEnvironment(DocumentBizTalkEnvironment(bt, env));
                }
            }
        }
        #endregion

        #region ApiServices
        private ApiDocumenter DocumentApiServiceEnvironment(ApiService api, Wiki.Models.Environment env)
        {
            EnvironmentInfo envInfo = Config.GetEnvironment(env);
            DLog.TraceEvent(TraceEventType.Information, DTEIdM, $"Documenting Api Service: {api.Name} - {envInfo.Name}");

            ApiDocumenter apid = new ApiDocumenter(api, GetPrimaryStorage(envInfo), envInfo, env.Address);
            apid.Start();
            return apid;
        }

        private void DocumentApiServices()
        {
            foreach (ApiService api in Config.ApiServices)
            {
				if (api.Type == "ApiManagement")
				{
					ApiManagementMenuItem apiHeader = Menu.AddApiManagementHeader(api);

					foreach (Wiki.Models.Environment env in api.Environments)
					{
						apiHeader.AddDocumentedEnvironment(DocumentApiServiceEnvironment(api, env));
					}
				}
				else if (api.Type == "Swagger")
				{
					ApiServiceMenuItem apiHeader = Menu.AddApiServiceHeader(api);

					foreach (Wiki.Models.Environment env in api.Environments)
					{
						apiHeader.AddDocumentedEnvironment(DocumentApiServiceEnvironment(api, env));
					}
				}
            }
        }
        #endregion

        #region Databases
        private DbDocumenter DocumentDbEnvironment(Database db, Wiki.Models.Environment env)
        {
            EnvironmentInfo envInfo = Config.GetEnvironment(env);
            DLog.TraceEvent(TraceEventType.Information, DTEIdM, $"Documenting Database: {db.Name} - {envInfo.Name}");

            DbDocumenter dbd = new DbDocumenter(env.Address, db.Name, Config.DocDir, GetPrimaryStorage(envInfo), envInfo);
            dbd.Start();
            return dbd;
        }

        private void DocumentDatabases()
        {
            foreach (Database db in Config.Databases)
            {
                DbMenuItem header = Menu.AddDbHeader(db);

                foreach (Wiki.Models.Environment env in db.Environments)
                {
                    header.AddDocumentedEnvironment(DocumentDbEnvironment(db, env));
                }
            }
        }
        #endregion

        #region Templates
        private void ScaffoldTemplates()
        {
            AzureBlobStorage storage = GetPrimaryStorage();
            int count = 0;
            foreach (var item in Directory.EnumerateFiles(Path.Combine(Config.DocDir.FullName, "Templates")))
            {
                FileInfo f = new FileInfo(item);
                if (storage.SetBlobContentIfNotExists(f.Name, f))
                    count++;
            }
            Console.WriteLine(count + " templates were uploaded");
        }
        #endregion
    }
}
