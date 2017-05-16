using AzureStorage;
using PosLogRelayService;
//using Microsoft.WindowsAzure.Storage;
//using Microsoft.WindowsAzure.Storage.Auth;
//using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Xsl;
using Wiki.Models;
using Wiki.Utilities;
using static Wiki.Models.Constants;

namespace Documenter
{
    public class ApiDocumenter : IDocumenter
    {
        public string DocSourceDir { get; set; }
        public string DocRootDir { get; set; }
        public AzureBlobStorage AzureStorage { get; set; }
        public ApiService Config { get; set; }
        public EnvironmentInfo EnvInfo { get; set; }

        private String _key;
        public String Key
        {
            get
            {
                if (_key == null)
				{
					if(Config.Type == "ApiManagement")
						_key = "Api_Management£" + WikiBlob.SanitizeV2(Name);
					else
						_key = "Api£" + WikiBlob.SanitizeV2(Name);
				}
				return _key;
            }
        }

        public String Link(String className = null)
        {
            return WikiBlob.GetAsWikiLink(Key, false, Name, className);
        }

        public String Name { get; private set; }

        public string Address { get; set; }

        public ApiDocumenter(ApiService api, AzureBlobStorage storage, EnvironmentInfo envInfo, String address)
        {
            this.AzureStorage = storage;
            this.Config = api;
            this.Name = Config.Name;
            this.EnvInfo = envInfo;
            this.Address = address;
        }

        public TimeSpan Start()
        {
            Stopwatch timer = new Stopwatch();
            timer.Start();
            DocumentSwagger();
            timer.Stop();
            return timer.Elapsed;
        }

        private void DocumentSwagger()
        {
            DLog.TraceEvent(TraceEventType.Information, DTEId, $"{Name}\\{EnvInfo.Name}: Indexing and documenting Api Service");

            try
            {
                RestClient client = new RestClient(Config.Authentication, Config.Headers);
                String swaggerJson = client.GetString(Address).Result;
                SaveToAzure(swaggerJson, Key);
            }
            catch (Exception ex)
            {
                DLog.TraceEvent(TraceEventType.Error, DTEId, $"{Name}\\{EnvInfo.Name}: Failed to document Api Service. {ex.Message}");
                DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"{Name}\\{EnvInfo.Name}: Failed to document Api Service. {ex.ToString()}");
            }
        }

        public void SaveToAzure(String content, string key)
        {
            try
            {
                AzureStorage.SetBlobContentAsString(key, content);
            }
            catch (Exception ex)
            {
                DLog.TraceEvent(TraceEventType.Error, DTEId, $"{Name}\\{EnvInfo.Name}: Azure upload failed. {ex.Message}");
                DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"{Name}\\{EnvInfo.Name}: Azure upload failed. {ex.ToString()}");
            }
        }

    }



}