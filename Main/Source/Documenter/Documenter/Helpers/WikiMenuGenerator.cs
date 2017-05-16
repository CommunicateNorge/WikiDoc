using AzureStorage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wiki.Models;
using Wiki.Utilities;
using static Wiki.Models.Constants;

namespace Documenter
{
    public class WikiMenuGenerator
    {
        string linkTreeKey = "Link_Tree";

        public List<BizTalkMenuItem> BizTalkHeaders = new List<BizTalkMenuItem>();

        public List<ApiServiceMenuItem> ApiServiceHeaders = new List<ApiServiceMenuItem>();
        public List<ApiManagementMenuItem> ApiManagementHeaders = new List<ApiManagementMenuItem>();

		public List<DbMenuItem> DbHeaders = new List<DbMenuItem>();

        public List<WebJobMenuItem> WebJobHeaders = new List<WebJobMenuItem>();

        /// <summary>
        /// BizTalk will get one header per BizTalk installation.
        /// All other types will get one header period.
        /// </summary>
        public void CreateMenu(AzureBlobStorage storage)
        {
            try
            {
                StringBuilder menu = new StringBuilder();

                foreach (BizTalkMenuItem item in BizTalkHeaders)
                {
                    Dictionary<String, String> link = item.CreateLinks();
                    AppendSection(ref menu, link.Values, item.Config.Name);
                }
                CreateAndAppendSection(ref menu, ApiServiceHeaders, "Api Services");
                CreateAndAppendSection(ref menu, ApiManagementHeaders, "Api Management");
				CreateAndAppendSection(ref menu, DbHeaders, "Databases");
                CreateAndAppendSection(ref menu, WebJobHeaders, "WebJobs");

                storage.SetBlobContentAsString(linkTreeKey, menu.ToString());
            }
            catch (Exception ex)
            {
                DLog.TraceEvent(TraceEventType.Critical, DTEId, $"Failed to create menu tree.{ex.Message}");
                DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"Failed to create menu tree.{ex.ToString()}");
            }
        }

        private void CreateAndAppendSection(ref StringBuilder menu, IEnumerable<IMenuItem> headers, String title)
        {
            List<String> links = new List<string>();
            foreach (IMenuItem item in headers)
            {
                Dictionary<String, String> link = item.CreateLinks();
                links.AddRange(link.Values);
            }
            AppendSection(ref menu, links, title);
        }

        private void AppendSection(ref StringBuilder menu, IEnumerable<String> links, string title)
        {
            String titleClean = title.Split(' ').FirstOrDefault()?.ToLowerInvariant();

			if (title == "Api Management")
				titleClean = "ApiManagement";

			menu.AppendLine(HtmlGenerator.CreateTag("li", "", className: "list-group-item", closeTag: false, id: "autogen"));
            menu.AppendLine(HtmlGenerator.CreateLink("#", title, className: "linkHeader mainHeader", id:$"{titleClean}Header"));
            menu.AppendUl(links, "projectInternalLinks autoLinkTree");
            menu.AppendLine(HtmlGenerator.CloseTag("li"));
        }


        public BizTalkMenuItem AddBizTalkHeader(BizTalk bt)
        {
            BizTalkMenuItem btItem = new BizTalkMenuItem(bt);
            BizTalkHeaders.Add(btItem);
            return btItem;
        }

        public ApiServiceMenuItem AddApiServiceHeader(ApiService api)
        {
            ApiServiceMenuItem apiItem = new ApiServiceMenuItem(api);
            ApiServiceHeaders.Add(apiItem);
            return apiItem;
        }

		public ApiManagementMenuItem AddApiManagementHeader(ApiService api)
		{
			ApiManagementMenuItem apiItem = new ApiManagementMenuItem(api);
			ApiManagementHeaders.Add(apiItem);
			return apiItem;
		}

		public WebJobMenuItem AddWebJobHeader(WebJob wj)
        {
            WebJobMenuItem item = new WebJobMenuItem(wj);
            WebJobHeaders.Add(item);
            return item;
        }

        public DbMenuItem AddDbHeader(Database db)
        {
            DbMenuItem item = new DbMenuItem(db);
            DbHeaders.Add(item);
            return item;
        }

    }

    public interface IMenuItem
    {
        Dictionary<string, string> CreateLinks();
    }

    public class WebJobMenuItem : IMenuItem
    {
        public List<WebJobDocumenter> Environments = new List<WebJobDocumenter>();
        public WebJob Config { get; set; }

        public WebJobMenuItem(WebJob wj)
        {
            this.Config = wj;
        }

        public void AddDocumentedEnvironment(WebJobDocumenter db)
        {
            Environments.Add(db);
        }

        public Dictionary<string, string> CreateLinks()
        {
            Dictionary<String, String> webjobs = new Dictionary<string, string>();
            foreach (WebJobDocumenter item in Environments)
            {
                if (!webjobs.ContainsKey(item.Key))
                    webjobs.Add(item.Key, item.Link(className: "linkHeader"));

                webjobs[item.Key] += HtmlGenerator.CreateTag("span", item.EnvInfo.Name.FirstLetterUpperCase(), "env" + item.EnvInfo.Index);
            }
            return webjobs;
        }
    }

    public class DbMenuItem : IMenuItem
    {
        public List<DbDocumenter> Environments = new List<DbDocumenter>();
        public Database Config { get; set; }

        public DbMenuItem(Database db)
        {
            this.Config = db;
        }
        public void AddDocumentedEnvironment(DbDocumenter db)
        {
            Environments.Add(db);
        }

        public Dictionary<string, string> CreateLinks()
        {
            Dictionary<String, String> databases = new Dictionary<string, string>();
            foreach (DbDocumenter item in Environments)
            {
                if (!databases.ContainsKey(item.Key))
                    databases.Add(item.Key, item.Link(className: "linkHeader"));

                databases[item.Key] += HtmlGenerator.CreateTag("span", item.EnvInfo.Name.FirstLetterUpperCase(), "env" + item.EnvInfo.Index);
            }
            return databases;
        }
    }

    public class ApiServiceMenuItem : IMenuItem
    {
        public List<ApiDocumenter> Environments = new List<ApiDocumenter>();
        public ApiService Config { get; set; }

        public ApiServiceMenuItem(ApiService api)
        {
            this.Config = api;
        }
        public void AddDocumentedEnvironment(ApiDocumenter api)
        {
            Environments.Add(api);
        }

        public Dictionary<string, string> CreateLinks()
        {
            Dictionary<String, String> apis = new Dictionary<string, string>();
            foreach (ApiDocumenter item in Environments)
            {
                if (!apis.ContainsKey(item.Key))
                    apis.Add(item.Key, item.Link(className: "linkHeader"));

                apis[item.Key] += HtmlGenerator.CreateTag("span", item.EnvInfo.Name.FirstLetterUpperCase(), "env" + item.EnvInfo.Index);
            }
            return apis;
        }
    }

	public class ApiManagementMenuItem : IMenuItem
	{
		public List<ApiDocumenter> Environments = new List<ApiDocumenter>();
		public ApiService Config { get; set; }

		public ApiManagementMenuItem(ApiService api)
		{
			this.Config = api;
		}
		public void AddDocumentedEnvironment(ApiDocumenter api)
		{
			Environments.Add(api);
		}

		public Dictionary<string, string> CreateLinks()
		{
			Dictionary<String, String> apis = new Dictionary<string, string>();
			foreach (ApiDocumenter item in Environments)
			{
				if (!apis.ContainsKey(item.Key))
					apis.Add(item.Key, item.Link(className: "linkHeader"));

				apis[item.Key] += HtmlGenerator.CreateTag("span", item.EnvInfo.Name.FirstLetterUpperCase(), "env" + item.EnvInfo.Index);
			}
			return apis;
		}
	}

	public class BizTalkMenuItem : IMenuItem
    {
        public List<BizDocumenter> BtEnvironments = new List<BizDocumenter>();
        public BizTalk Config { get; set; }

        public BizTalkMenuItem(BizTalk bt)
        {
            this.Config = bt;
        }

        public void AddDocumentedEnvironment(BizDocumenter d)
        {
            BtEnvironments.Add(d);
        }

        public Dictionary<String, String> CreateLinks()
        {
            Dictionary<String, String> applications = new Dictionary<string, string>();
            foreach (BizDocumenter d in BtEnvironments)
            {
                foreach (BizTalkApplication item in d.AllApplications.Values.OrderBy(x => x.Name)) //iterate over Integrations
                {
                    if (d.ShouldAppBeIgnored(item.Name))
                        continue;

                    if (!applications.ContainsKey(item.Key))
                        applications.Add(item.Key, item.Link(className: "linkHeader"));

                    applications[item.Key] += HtmlGenerator.CreateTag("span", d.EnvInfo.Name.FirstLetterUpperCase(), "env" + d.EnvInfo.Index);
                }
            }
            return applications;
        }

    }
}
