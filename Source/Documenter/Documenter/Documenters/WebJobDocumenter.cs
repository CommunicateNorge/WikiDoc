using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AzureStorage;
using static Wiki.Models.Constants;
using System.Diagnostics;
using Wiki.Models;
using Newtonsoft.Json;
using Wiki.Utilities;

namespace Documenter
{
    public class WebJobDocumenter : IDocumenter
    {
        private AzureBlobStorage Storage;
        private DirectoryInfo WorkingDirInfo;
        public EnvironmentInfo EnvInfo { get; set; }

        public string Name { get; private set; }

        private String _key;
        public String Key
        {
            get
            {
                if(_key == null)
                    _key = WikiBlob.Combine("WebJob", WikiBlob.SanitizeV2(Name));
                return _key;
            }
        }

        public String Link(String className = null)
        {
            return WikiBlob.GetAsWikiLink(Key, false, Name, className);
        }

        public WebJobDocumenter(DirectoryInfo workingDirInfo, string name, AzureBlobStorage azureBlobStorage, EnvironmentInfo envInfo)
        {
            this.WorkingDirInfo = workingDirInfo;
            this.Name = name;
            this.Storage = azureBlobStorage;
            this.EnvInfo = envInfo;
        }

        public TimeSpan Start()
        {
            DLog.TraceEvent(TraceEventType.Information, DTEId, $"{Name}\\{EnvInfo.Name}: Indexing and documenting WebJob");

            try
            {
                String metaInfo = Directory.GetFiles(WorkingDirInfo.FullName, "*.meta.json").FirstOrDefault();
                String content = null;

                if (metaInfo != null)
                    content = DocumentWebJobFromMetaFile(metaInfo);
                else
                {
                    content = HtmlGenerator.CreateTag("h3", Name);
                    content += HtmlGenerator.CreateTag("p", "The WebJob lacks metadata so no documentation could be generated. Take a look at this article on how to add metadata to your WebJob.");
                }
                SaveToAzure(content, Key);     
            }
            catch (Exception ex)
            {
                DLog.TraceEvent(TraceEventType.Error, DTEId, $"{Name}\\{EnvInfo.Name}: Failed to document WebJob. {ex.Message}");
                DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"{Name}\\{EnvInfo.Name}: Failed to document WebJob. {ex.ToString()}");
                SaveToAzure(HtmlGenerator.CreateTag("p", $"{Name}\\{EnvInfo.Name}: Failed to document WebJob"), Key);
            }
            return TimeSpan.FromMinutes(1.0);
        }

        private String DocumentWebJobFromMetaFile(String path)
        {
            WebJobInfo wji = JsonConvert.DeserializeObject<WebJobInfo>(File.ReadAllText(path));

            StringBuilder sb = new StringBuilder();

            sb.AppendLine(HtmlGenerator.CreateTag("div", "", "webjobautodiv", false));

            sb.AppendHtmlTag("h3", wji.webJobName);
            sb.AppendHtmlTag("p", wji.description);
            AppendRunTimeSection(wji, ref sb);
            AppendMiscSection(wji, ref sb);

            sb.AppendHtmlTag("h3", "Receive Endpoints");
            foreach (var item in wji.receiveEndpoints)
            {
                AppendEndpointSection(item, ref sb, "From");
            }

            sb.AppendHtmlTag("h3", "Send Endpoints");
            foreach (var item in wji.sendEndpoints)
            {
                AppendEndpointSection(item, ref sb, "To");
            }

            AppendSlaSection(wji.sla, ref sb);
            sb.AppendLine(HtmlGenerator.CloseTag("div"));

            return sb.ToString();
        }

        private void AppendSlaSection(Sla sla, ref StringBuilder sb)
        {
            sb.AppendHtmlTag("h3", "SLA");
            sb.Append(HtmlGenerator.CreateTag("p", "", null, false));
            sb.Append((sla.criticality != null) ? $"Criticality: {sla.criticality}{HtmlGenerator.Br}" : "");
            sb.Append((sla.operatingHours != null) ? $"Operating hours: {sla.operatingHours}{HtmlGenerator.Br}" : "");
            sb.Append((sla.frequency != null) ? $"Frequency: {sla.frequency}{HtmlGenerator.Br}" : "");
            sb.Append((sla.compliance != null) ? $"Compliance: {sla.compliance}{HtmlGenerator.Br}" : "");
            sb.Append((sla.serviceWindow != null) ? $"Service window: {sla.serviceWindow}{HtmlGenerator.Br}" : "");
            sb.Append(HtmlGenerator.CloseTag("p"));
        }

        private void AppendEndpointSection(Endpoint edp, ref StringBuilder sb, string direction = "From")
        {
            sb.AppendHtmlTag("h4", edp.name);
            sb.Append(HtmlGenerator.CreateTag("p", "", null, false));
            sb.Append((edp.type != null) ? $"Endpoint type: {edp.type}{HtmlGenerator.Br}" : "");
            sb.Append((edp.actor != null) ? $"{direction}: {edp.actor}{HtmlGenerator.Br}" : "");

            if (edp.addresses != null)
            {
                sb.AppendUl(Addresses(edp.addresses));
            }
            sb.Append(HtmlGenerator.CloseTag("p"));
        }

        public static IEnumerable<String> Addresses(List<Address> addresses)
        {
            foreach (var item in addresses)
            {
                yield return $"{item.environment} : {item.address}";
            }           
        }

        private void AppendMiscSection(WebJobInfo wji, ref StringBuilder sb)
        {
            sb.Append(HtmlGenerator.CreateTag("p", "", null, false));
            sb.Append($"Version: {wji.version}{HtmlGenerator.Br}");
            sb.Append((wji.relatedBusinessProcesses?.Count > 0) ? $"Related business process(es): {String.Join(",", wji.relatedBusinessProcesses)}{HtmlGenerator.Br}" : "");
            sb.Append((wji.logging != null) ? $"Logging: {wji.logging}{HtmlGenerator.Br}" : "");
            sb.Append((wji.supportingArchitecture != null) ? $"Architecture: {HtmlGenerator.CreateLink(wji.supportingArchitecture)}{HtmlGenerator.Br}" : "");
            sb.Append(HtmlGenerator.CloseTag("p"));
        }

        private void AppendRunTimeSection(WebJobInfo wji, ref StringBuilder sb)
        {
            sb.Append(HtmlGenerator.CreateTag("p", "", null, false));
            sb.Append($"Run Mode: {wji.runMode}");
            sb.Append((wji.startTime != null || wji.endTime != null) ? $"Start: {wji.startTime}, End: {wji.endTime}{HtmlGenerator.Br}" : "");
            sb.Append((wji.jobRecurrenceFrequency != null) ? $"Recurrence: {wji.jobRecurrenceFrequency}{HtmlGenerator.Br}" : "");
            sb.Append((wji.interval != null) ? $"Interval: {wji.interval}{HtmlGenerator.Br}" : "");
            sb.Append(HtmlGenerator.CloseTag("p"));
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
    }
}
