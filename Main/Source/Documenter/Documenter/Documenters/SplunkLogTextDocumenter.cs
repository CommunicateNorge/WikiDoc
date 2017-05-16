using AzureStorage;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Wiki.Models;
using Wiki.Utilities;
using static Wiki.Models.Constants;

namespace Documenter
{
    public class LogText
    {
        public LogText(String text, String application)
        {
            this.Application = application;
            if(text.StartsWith("${") && text.EndsWith("}"))
            {
                ParameterizedText = text.TrimStart('$').TrimStart('{').TrimEnd('}');
                IsParameterized = true;
            }
            else
            {
                Text = text;
            }
        }

        public bool IsParameterized { get; set; }
        public String Text { get; set; }
        public String ParameterizedText { get; set; }
        public String Application { get; set; }
        public String PortBindingsFile { get; set; }
        public String SettingsGeneratorFile { get; set; }

        private string _from;
        public String From
        {
            get
            {
                if (_from == null)
                {
                    if (Regex.IsMatch(Text, ".* from .*", RegexOptions.IgnoreCase) || (String.IsNullOrEmpty(To) && Application.Contains("Common")))
                        _from = Application.Split('.').FirstOrDefault() ?? "";
                    else
                        _from = String.Empty;
                }
                return _from;
            }
        }

        private string _to;
        public String To
        {
            get
            {
                if (_to == null)
                {
                    if (Regex.IsMatch(Text, ".* to .*", RegexOptions.IgnoreCase))
                    {
                        String[] fromto = Application.Split('.');
                        _to = (fromto.Length > 1) ? fromto[1] : "";
                    }
                    else
                        _to = String.Empty;
                }
                return _to;
            }
        }
    }

    public class SplunkLogTextDocumenter : IDocumenter
    {
        private BizTalk BtConfig;

        public String btPath { get; set; }
        public Dictionary<String, List<LogText>> Applications { get; set; } = new Dictionary<string, List<LogText>>();
        public AzureBlobStorage AzureStorage { get; set; }

        public String HtmlContent { get; set; }
        public String ExceptionContent { get; set; }

        public SplunkLogTextDocumenter(AzureBlobStorage storage, EnvironmentInfo envInfo, String path, BizTalk bt)
        {
            this.EnvInfo = envInfo;
            this.btPath = path;
            this.AzureStorage = storage;
            this.BtConfig = bt;
        }

        public TimeSpan Start()
        {
            Stopwatch timer = new Stopwatch();
            timer.Start();
            DocumentLogTexts();
            timer.Stop();
            return timer.Elapsed;
        }

        public void SaveToAzure(String content, string key)
        {
            try
            {
                AzureStorage.SetBlobContentAsString(key, content);
            }
            catch (Exception ex)
            {
                DLog.TraceEvent(TraceEventType.Error, DTEId, $"{Name}\\{EnvInfo?.Name}: Azure upload failed. {ex.Message}");
                DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"{Name}\\{EnvInfo?.Name}: Azure upload failed. {ex.ToString()}");
            }
        }

        private void DocumentLogTexts()
        {
            try
            {
                DLog.TraceEvent(TraceEventType.Information, DTEId, $"{Name}\\{EnvInfo?.Name}: Gathering log texts from SettingsFileGenerators");
                ParseLogTexts();
                BuildHtml();
                SaveToAzure(JsonConvert.SerializeObject(Applications), WikiBlob.Combine("File", Key) + "€json");
            }
            catch (Exception ex)
            {
                ExceptionContent = ExceptionContent + HtmlGenerator.CreateTag("p", $"Could not parse all log texts. {ex.Message}");
            }
            SaveToAzure(HtmlContent + ExceptionContent, Key);
        }

        private void BuildHtml()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(HtmlGenerator.StartTag("table", "simpleTable"));

            sb.AppendLine(HtmlGenerator.StartTag("tr"));
            sb.AppendLine(HtmlGenerator.CreateTag("th", "Application") + HtmlGenerator.CreateTag("th", "From") + HtmlGenerator.CreateTag("th", "To") + HtmlGenerator.CreateTag("th", "Log Text") + HtmlGenerator.CreateTag("th", "Log Text Parameterized"));
            sb.AppendLine(HtmlGenerator.CloseTag("tr"));

            foreach (var item in Applications)
            {
                String rows = HtmlGenerator.CreateRows<LogText>(item.Value, (ltkv) =>
                {
                    String row = HtmlGenerator.CreateTag("td", ltkv.Application)
                                + HtmlGenerator.CreateTag("td", ltkv.From)
                                + HtmlGenerator.CreateTag("td", ltkv.To)
                                + HtmlGenerator.CreateTag("td", ltkv.Text)
                                + HtmlGenerator.CreateTag("td", ltkv.ParameterizedText);
                    return row;
                });
                sb.Append(rows);
            }
            sb.AppendLine(HtmlGenerator.CloseTag("table"));
            HtmlContent = sb.ToString();
        }

        private void ParseLogTexts()
        {
            DirectoryInfo di = new DirectoryInfo(btPath);
            foreach (FileInfo file in di.EnumerateFiles("PortBindingsMaster.xml", SearchOption.AllDirectories))
            {
                DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"Processing {file.FullName}");
                String application = file.Directory.Parent.Name;
                String pbMaster = File.ReadAllText(file.FullName);
                MatchCollection matches = Regex.Matches(pbMaster, Regex.Escape("&lt;LogText vt=\"8\"&gt;") + "(.*?)" + Regex.Escape("&lt;/LogText&gt;"));

                if (matches.Count > 0)
                {
                    bool atLeastOneParameterized = false;

                    Applications.Add(application, new List<LogText>());

                    foreach (Match match in matches)
                    {
                        LogText lt = new LogText(match.Groups[1].Value, application);
                        Applications[application].Add(lt);
                        atLeastOneParameterized = atLeastOneParameterized || lt.IsParameterized;
                    }

                    if (atLeastOneParameterized && GetSettingsFileGeneratorPath(file.Directory.FullName) != null)
                    {
                        try
                        {
                            MapParameterizedLogTexts(application, file);
                        }
                        catch (Exception ex)
                        {
                            ExceptionContent = ExceptionContent + HtmlGenerator.CreateTag("p", $"Could not map all log texts for {application}. {ex.Message}");
                            DLog.TraceEvent(TraceEventType.Error, DTEId, $"Could not map all log texts for {application}. {ex.ToString()}");
                        }
                    }
                }
            }
        }

        private void MapParameterizedLogTexts(String application, FileInfo portBindingsMaster)
        {
            IEnumerable<LogText> ltxts = Applications[application].Where(z => z.IsParameterized);
            String settingsFileGenPath = GetSettingsFileGeneratorPath(portBindingsMaster.Directory.FullName);
            String ltxtReg = GetLogTextRegex(ltxts);
            XmlNodeList nlist = LoadSettingsFileGeneratorXml(settingsFileGenPath);

            foreach (XmlNode row in nlist)
            {
                IEnumerable<LogText> l = ltxts.Where(x => row.InnerText.Contains(x.ParameterizedText));
                if (l != null && l.Count() > 0 && row.ChildNodes.Count > 1)
                {
                    for (int i = 1; i < row.ChildNodes.Count; i++)
                    {
                        if (!String.IsNullOrEmpty(row.ChildNodes[i].InnerText))
                        {
                            l.Each(z => z.Text = row.ChildNodes[i].InnerText);
                            break;
                        }
                    }
                }
            }
        }

        private XmlNodeList LoadSettingsFileGeneratorXml(string path)
        {
            var doc = new XmlDocument();
            doc.Load(path);
            var XNMngr = new XmlNamespaceManager(doc.NameTable);
            XNMngr.AddNamespace("ss", "urn:schemas-microsoft-com:office:spreadsheet");
            var tableNode = (XmlElement)doc.DocumentElement.SelectSingleNode("//ss:Table", XNMngr);
            XmlNodeList rowNodes = doc.DocumentElement.SelectNodes("//ss:Row", XNMngr);
            return rowNodes;
        }

        private String GetLogTextRegex(IEnumerable<LogText> ltxs)
        {
            String regex = "(";
            foreach (LogText lt in ltxs)
            {
                if (lt.IsParameterized)
                    regex += lt.ParameterizedText + "|";
            }
            regex.TrimEnd('|');
            return regex + ")";
        }

        private String GetSettingsFileGeneratorPath(String parent)
        {
            String p = Path.Combine(parent, "SettingsFileGenerator.xml");
            if (File.Exists(p))
                return p;
            return null;
        }

        public EnvironmentInfo EnvInfo { get; set; }

        public string Key
        {
            get
            {
                return WikiBlob.Combine("Integration", WikiBlob.SanitizeV2(BtConfig.Name), WikiBlob.SanitizeV2(Name));
            }
        }

        public string Name
        {
            get
            {
                return "Splunk Log Texts";
            }
        }

        public String Link(String className = null)
        {
            return WikiBlob.GetAsWikiLink(Key, false, Name, className);
        }


    }


}




