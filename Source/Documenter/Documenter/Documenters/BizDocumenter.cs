using AzureStorage;
using CustomXsltDocumenter;
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
using System.Xml;
using System.Xml.Xsl;
using Wiki.Models;
using Wiki.Utilities;
using static Wiki.Models.Constants;

namespace Documenter
{
    public class BizDocumenter : IDocumenter
    {
        private static string TransformMapXslt = @"BizTalkMapDocumenterHTML.xslt";
        private static string TransformSchemaXsl = @"BizTalkSchemaDocumenterHTML.xs3p.xsl";
        private static string Msxsl = "msxsl.exe";
        public string BizTalkDocumenterFolderName;

        public bool SandCastleOnly { get; set; }
        public string Name { get; set; }
        public string DocSourceDir { get; set; }
        public string DocRootDir { get; set; }
        public int MapsUpToDate { get; set; }
        public int SchemasUpToDate { get; set; }
        public int MapsNewOrUpdated { get; set; }
        public int SchemasNewOrUpdated { get; set; }
        public DirectoryInfo AppRootDir { get; set; }
        public Dictionary<String, BizTalkApplication> AllApplications = new Dictionary<String, BizTalkApplication>();
        public string SQLDbIndexTree { get; set; }

        public AzureBlobStorage AzureStorage { get; set; }
        private XslCompiledTransform Xslt = new XslCompiledTransform();
        private bool SkipSchemaUpload = false;
        private bool SkipMapUpload = false;

        public BizTalk Config { get; set; }
        public EnvironmentInfo EnvInfo { get; set; }

        //Unused
        private string _key;
        public String Key
        {
            get
            {
                if (_key == null)
                    _key = WikiBlob.Combine("BizTalk", WikiBlob.SanitizeV2(Name));
                return _key;
            }
        }

        //Unused
        public String Link(String className = null)
        {
            return WikiBlob.GetAsWikiLink(Key, false, Name, className);
        }

        public BizDocumenter(DirectoryInfo appSourceDirectory, DirectoryInfo docRootDirectory, EnvironmentInfo envInfo, bool sandCastleOnly, AzureBlobStorage storage, BizTalk btConfig)
        {
            this.AppRootDir = appSourceDirectory;
            this.DocRootDir = docRootDirectory.FullName;
            this.SandCastleOnly = sandCastleOnly;
            this.Name = btConfig.Name;
            this.AzureStorage = storage;
            this.Config = btConfig;
            this.EnvInfo = envInfo;
            this.BizTalkDocumenterFolderName = "BTDocumenter_" + Name.Safe() + "_" + envInfo.Name.Safe();
            this.DocSourceDir = DocRootDir + @"\Source_" + Name.Safe() + envInfo.Name.Safe();
        }

        public TimeSpan Start()
        {
            Stopwatch timer = new Stopwatch();
            timer.Start();
            IndexIntegrationApps();

            if (SandCastleOnly)
                DocumentSandCastle();
            else
            {
                RetrieveBizTalkDocumenterDoc();
                DocumentMapsAndSchemas();
                DocumentOrchs();
                DocumentPipelines();
                DocumentRecievePorts();
                DocumentSendPorts();
                DocumentTransformations();
                DocumentApplications();
                DocumentSandCastle();
                CreateDocumentIndeces();
                CleanUp();
            }
            timer.Stop();
            return timer.Elapsed;
        }

        private void CleanUp()
        {
            String path = GetBizTalkDocumenterPath();
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch (Exception ex)
            {
                DLog.TraceEvent(TraceEventType.Error, DTEId, $"{Name}\\{EnvInfo.Name}: Failed to clean up old BTDocumenter files. {ex.Message}");
            }
        }

        private void RetrieveBizTalkDocumenterDoc()
        {
            try
            {
                String path = GetBizTalkDocumenterPath();
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

                String btDocumenterKey = WikiBlob.Combine("File", "BTDocumenter", Name, EnvInfo.Name);

                String newPath = Path.Combine(path, DateTime.Now.ToString("yyyy-MM-dd_hh-mm") + ".chm");

                if (AzureStorage.GetBlobContentToFile(btDocumenterKey, newPath))
                {
                    DateTime modified = (DateTime)AzureStorage.GetBlobLastModified(btDocumenterKey);
                    TimeSpan age = DateTime.Now - modified;
                    if (age.Days > 2)
                        DLog.TraceEvent(TraceEventType.Warning, DTEId, $"{Name}\\{EnvInfo.Name}: The BTDocumenter file found is {age} old, so it may be outdated.");

                    Process cmd = new Process();
                    ProcessStartInfo startInfo = new ProcessStartInfo();
                    startInfo.FileName = "Hh.exe";
                    startInfo.Arguments = "-decompile " + $"{path} {newPath}";
                    cmd.StartInfo = startInfo;
                    cmd.Start();
                    cmd.WaitForExit();
                }
                else
                    DLog.TraceEvent(TraceEventType.Warning, DTEId, $"{Name}\\{EnvInfo.Name}: The BTDocumenter file was not found with key {btDocumenterKey}.");
            }
            catch (Exception ex)
            {
                DLog.TraceEvent(TraceEventType.Error, DTEId, $"{Name}\\{EnvInfo.Name}: Exception when retrieving BTDocumenter content. {ex.Message}");
            }
        }

        private void IndexIntegrationApps()
        {
            DLog.TraceEvent(TraceEventType.Information, DTEId, $"{Name}\\{EnvInfo.Name}: Indexing and documenting all apps");

            List<string> appsToIgnore = GetAppIgnoreList();

            foreach (DirectoryInfo intApp in AppRootDir.GetDirectories())
            {
                if(!appsToIgnore.Contains(intApp.Name))
                { 
                    String rootPath = Path.Combine(DocSourceDir, intApp.Name);
                    BizTalkApplication i = new BizTalkApplication(rootPath, intApp.Name, intApp);
                    AllApplications.Add(intApp.Name, i);
                    DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"{Name}\\{EnvInfo.Name}: Indexed app {intApp.FullName}, hash path {rootPath}");
                }
                else
                    DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"{Name}\\{EnvInfo.Name}: Ignored app {intApp.FullName}");
            }
        }

        private List<String> GetAppIgnoreList()
        {
            return Config.IgnoreApps ?? new List<string>();
        }

        private void DocumentSandCastle()
        {
            DLog.TraceEvent(TraceEventType.Information, DTEId, $"{Name}\\{EnvInfo.Name}: Indexing and uploading SandCastle Help-files");
            int count = 1;
            Console.Write(count);
            foreach (var intAppEntry in AllApplications.OrderBy(x => x.Value.Name))
            {
                FileInfo sln = intAppEntry.Value.OrgPath.GetFiles("*.sln").FirstOrDefault();
                if (sln == null) continue;

                String path = Path.Combine(sln.FullName.Replace(".sln", ".Doc"), "Help");
                DirectoryInfo helpFiles = new DirectoryInfo(path);
                if (helpFiles.Exists)
                {
                    try
                    {
                        DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"{Name}\\{EnvInfo.Name}: Help file directory \"{helpFiles.FullName}\" found.");
                        SandCastleUploader scu = new SandCastleUploader(WikiBlob.SanitizeV2(Name), WikiBlob.SanitizeV2(intAppEntry.Value.Name), path, AzureStorage);
                        String entryPointKey = scu.ParseAndUploadHelpFiles();
                        DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"{Name}\\{EnvInfo.Name}: Help file entry point {entryPointKey} added to integration {intAppEntry.Value.Name}");
                        intAppEntry.Value.CSharpDocs.Add(WikiBlob.GetAsWikiLink(entryPointKey));
                        Console.Write("\r" + count++);
                    }
                    catch (Exception ex)
                    {
                        DLog.TraceEvent(TraceEventType.Error, DTEId, $"{Name}\\{EnvInfo.Name}: Exception when parsing SandCastle documentation. {ex.Message}");
                    }
                }
            }
        }

        private void DocumentOrchs()
        {
            DLog.TraceEvent(TraceEventType.Information, DTEId, $"{Name}\\{EnvInfo.Name}: Indexing and uploading Orchestrations");
       
            int count = 0;
            Console.Write(count);
            String iNamePrefix = "Application:</td><td class=\"TableData\">";
            String iNameSuffix = "</td>";
            String orchNamePrefix = "Orchestration : ";
            String orchNameSuffix = "</nobr>";

            DirectoryInfo di = new DirectoryInfo(GetBizTalkDocumenterPath("Orchestration"));
            if(di.Exists)
            {
                foreach (var item in di.GetFiles("*.htm"))
                {
                    if (item.Name.IndexOf("Code") < 0 && item.Name.IndexOf("Overview") < 0 && item.Name.IndexOf("CorrelationTypes") < 0)
                    {
                        DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"{Name}\\{EnvInfo.Name}: BTDocumenter Orchestration: {item.FullName}");

                        String content = File.ReadAllText(item.FullName);
                        String integrationName = ExtractPart(content, iNamePrefix, iNameSuffix);

                        if (ShouldAppBeIgnored(integrationName))
                            continue;

                        String orchistrationName = ExtractPart(content, orchNamePrefix, orchNameSuffix);

                        String key = "Orchestration£" + DotEncode(integrationName) + "£" + DotEncode(orchistrationName);                        

                        if (AllApplications.ContainsKey(integrationName))
                        {
                            AllApplications[integrationName].Orchestrations.Add(CreateWikiLink(key, orchistrationName));
                        }

                        SaveToAzure(AlterImageLinks(content), key);

                        FileInfo code = new FileInfo(Path.Combine(item.Directory.FullName, "Code" + item.Name));
                        if (code.Exists) SaveToAzure(AlterImageLinks(code), key + "£Code");

                        FileInfo overview = new FileInfo(Path.Combine(item.Directory.FullName, "Overview" + item.Name));
                        if (overview.Exists)
                        {
                            Regex r = new Regex(@"(<img src=.{30,50}\.jpg"">)", RegexOptions.IgnoreCase);
                            string ovrviw = File.ReadAllText(overview.FullName);
                            Match m = r.Match(ovrviw);
                            ovrviw = ovrviw.Replace(m.Groups[1].Value, "<img src=\"/Images/OrchImage_" + EnvInfo.Name.Safe() + "/" + key + "£Image\"></img>");
                            SaveToAzure(AlterImageLinks(ovrviw), key + "£Overview");
                        }

                        FileInfo image = new FileInfo(Path.Combine(item.Directory.FullName, item.Name.Replace(".htm",".jpg")));
                        if (image.Exists) SaveToAzure(image, key + "£Image");

                        Console.Write("\r" + count++);
                    }
                }
            }
            else
            {
                Console.WriteLine();
                DLog.TraceEvent(TraceEventType.Warning, DTEId, $"{Name}\\{EnvInfo.Name}: No BizTalk Documenter Orchestration folder found at {di.FullName}");
            }
            Console.WriteLine();
        }

        private void DocumentPipelines()
        {
            DLog.TraceEvent(TraceEventType.Information, DTEId, $"{Name}\\{EnvInfo.Name}: Indexing and uploading Pipelines");

            int count = 0;
            Console.Write(count);
            String iNamePrefix = "Application:</nobr></td><td class=\"TableData\">";
            String iNameSuffix = "</td>";
            String pipeNamePrefix = "Pipeline : ";
            String pipeNameSuffix = "</nobr>";

            DirectoryInfo di = new DirectoryInfo(GetBizTalkDocumenterPath("Pipeline"));
            if (di.Exists)
            {
                foreach (var item in di.GetFiles("*.htm"))
                {
                    DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"{Name}\\{EnvInfo.Name}: BTDocumenter Pipeline: {item.FullName}");
                    String content = File.ReadAllText(item.FullName);
                    String integrationName = ExtractPart(content, iNamePrefix, iNameSuffix);

                    if (ShouldAppBeIgnored(integrationName))
                        continue;

                    String pipelineName = ExtractPart(content, pipeNamePrefix, pipeNameSuffix);

                    String key = "Pipeline£" + DotEncode(integrationName) + "£" + DotEncode(pipelineName);

                    if (AllApplications.ContainsKey(integrationName))
                    {
                        AllApplications[integrationName].Pipelines.Add(CreateWikiLink(key, pipelineName));
                    }

                    SaveToAzure(AlterImageLinks(content), key);
                    Console.Write("\r" + count++);
                }
            }
            else
            {
                Console.WriteLine();
                DLog.TraceEvent(TraceEventType.Warning, DTEId, $"{Name}\\{EnvInfo.Name}: No BizTalk Documenter Pipeline folder found at {di.FullName}");
            }
            Console.WriteLine();
        }

        private void DocumentRecievePorts()
        {
            DLog.TraceEvent(TraceEventType.Information, DTEId, $"{Name}\\{EnvInfo.Name}: Indexing and uploading Recieve Ports");
            int count = 0;
            Console.Write(count);
            String iNamePrefix = "Application:</nobr></td><td class=\"TableData\">";
            String iNameSuffix = "</td>";
            String rpNamePrefix = "Receive Port : ";
            String rpNameSuffix = "</nobr>";

            DirectoryInfo di = new DirectoryInfo(GetBizTalkDocumenterPath("ReceivePort"));
            if (di.Exists)
            {
                foreach (var item in di.GetFiles("*.htm"))
                {
                    DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"{Name}\\{EnvInfo.Name}: BTDocumenter Recieve Port: {item.FullName}");
                    String content = File.ReadAllText(item.FullName);
                    String integrationName = ExtractPart(content, iNamePrefix, iNameSuffix);

                    if (ShouldAppBeIgnored(integrationName))
                        continue;

                    String rpName = ExtractPart(content, rpNamePrefix, rpNameSuffix);

                    String key = "RecievePorts£" + DotEncode(integrationName) + "£" + DotEncode(rpName);

                    if (AllApplications.ContainsKey(integrationName))
                    {
                        AllApplications[integrationName].RecievePorts.Add(CreateWikiLink(key, rpName));
                    }

                    SaveToAzure(AlterImageLinks(content), key);
                    Console.Write("\r" + count++);
                }
            }
            else
            {
                Console.WriteLine();
                DLog.TraceEvent(TraceEventType.Warning, DTEId, $"{Name}\\{EnvInfo.Name}: No BizTalk Documenter ReceivePort folder found at {di.FullName}");
            }
            Console.WriteLine();
        }

        private void DocumentSendPorts()
        {
            DLog.TraceEvent(TraceEventType.Information, DTEId, $"{Name}\\{EnvInfo.Name}: Indexing and uploading Send Ports");
            int count = 0;
            Console.Write(count);
            String iNamePrefix = "Application:</td><td class=\"TableData\">";
            String iNameSuffix = "</td>";
            String spNamePrefix = "Send Port : ";
            String spNameSuffix = "</nobr>";

            DirectoryInfo di = new DirectoryInfo(GetBizTalkDocumenterPath("SendPort"));
            if (di.Exists)
            {
                foreach (var item in di.GetFiles("*.htm"))
                {
                    DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"{Name}\\{EnvInfo.Name}: BTDocumenter Send Port: {item.FullName}");

                    String content = File.ReadAllText(item.FullName);
                    String integrationName = ExtractPart(content, iNamePrefix, iNameSuffix);

                    if (ShouldAppBeIgnored(integrationName))
                        continue;

                    String spName = ExtractPart(content, spNamePrefix, spNameSuffix);

                    String key = DotEncode("SendPorts£" + integrationName + "£" + spName);

                    if (AllApplications.ContainsKey(integrationName))
                    {
                        AllApplications[integrationName].SendPorts.Add(CreateWikiLink(key, spName));
                    }

                    SaveToAzure(AlterImageLinks(content), key);
                    Console.Write("\r" + count++);
                }
            }
            else
            {
                Console.WriteLine();
                DLog.TraceEvent(TraceEventType.Warning, DTEId, $"{Name}\\{EnvInfo.Name}: No BizTalk Documenter Send Port folder found at {di.FullName}");
            }
            Console.WriteLine();
        }

        private void DocumentApplications()
        {
            DLog.TraceEvent(TraceEventType.Information, DTEId, $"{Name}\\{EnvInfo.Name}: Indexing and uploading Applications");
            int count = 0;
            Console.Write(count);
            String cPrefix = "<body>";
            String cSuffix = "<HR CLASS=\"Rule\">";
            String namePrefix = "Application : ";
            String nameSuffix = "</nobr>";

            DirectoryInfo di = new DirectoryInfo(GetBizTalkDocumenterPath("Application"));
            if (di.Exists)
            {
                foreach (var item in di.GetFiles("*.htm"))
                {
                    DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"{Name}\\{EnvInfo.Name}: BTDocumenter Application: {item.FullName}");

                    String content = File.ReadAllText(item.FullName);
                    String integrationName = ExtractPart(content, namePrefix, nameSuffix);

                    if (ShouldAppBeIgnored(integrationName))
                        continue;

                    content = AlterImageLinks(ExtractPart(content, cPrefix, cSuffix));
                    content = AlterReferencedAppLinks(content);

                    if (AllApplications.ContainsKey(integrationName))
                    {
                        AllApplications[integrationName].Extra = content;
                    }
                    Console.Write("\r" + count++);
                }
            }
            Console.WriteLine();
        }

        private string AlterReferencedAppLinks(string content)
        {
            content = Regex.Replace(content, @"<A CLASS=""TableData"" HREF=""&#xD;&#xA;\s+/Application/[\w\d\-]{36}\.html&#xD;&#xA;\s+"">([\w\.\-_ ]+)</A>", delegate (Match match)
            {
                String nm = match.Groups[1].Value.ToString();
                return WikiBlob.GetAsWikiLink(WikiBlob.Combine("Integration", WikiBlob.SanitizeV2(nm)), false, nm, "TableData");
            });
            return content;
        }

        private void CreateDocumentIndeces()
        {
            DLog.TraceEvent(TraceEventType.Information, DTEId, $"{Name}\\{EnvInfo.Name}: Creating application index pages and uploading");

            foreach (var item in AllApplications.Values.OrderBy(x => x.Name)) //iterate over Integrations
            {
                if (ShouldAppBeIgnored(item.Name))
                    continue;

                String integrationKey = "Integration£" + DotEncode(item.Name);
                String integrationHtmlPath = Path.Combine(item.Path, integrationKey + ".html");

                StringBuilder sb = new StringBuilder();
                sb.Append(item.Extra).Append("</br>");
                AppendLinks("C# Code Doc", sb, item.CSharpDocs, true);
                AppendLinks("Orchestrations", sb, item.Orchestrations);
                AppendLinks("Pipelines", sb, item.Pipelines);
                AppendLinks("Recieve Ports", sb, item.RecievePorts);
                AppendLinks("Send Ports", sb, item.SendPorts);
                AppendLinks("Maps", sb, item.Maps);
                AppendLinks("Schemas", sb, item.Schemas);
                AppendLinks("Transformations", sb, item.Transformations, true);

                SaveToAzureAndLocal(integrationHtmlPath, integrationKey, sb.ToString());
            }
            CreateMapOverviewPage();
            CreateSchemaOverviewPage();
        }

        private void CreateMapOverviewPage()
        {
            DLog.TraceEvent(TraceEventType.Information, DTEId, $"{Name}\\{EnvInfo.Name}: Creating map index page and uploading");

            StringBuilder allMaps = new StringBuilder();

            foreach (var item in AllApplications.Values.OrderBy(x => x.Name))
            {
                if (item.Maps?.Count != 0 && !ShouldAppBeIgnored(item.Name))
                {
                    allMaps.AppendLine(HtmlGenerator.CreateTag("h3", item.Name));
                    HtmlGenerator.CreateUl(item.Maps.OrderBy(c => c), ref allMaps);
                }
            }
            SaveToAzure(allMaps.ToString(), "Overview£Maps£" + WikiBlob.SanitizeV2(Config.Name));
        }

        private void CreateSchemaOverviewPage()
        {
            DLog.TraceEvent(TraceEventType.Information, DTEId, $"{Name}\\{EnvInfo.Name}: Creating schema index pages and uploading");

            StringBuilder allSchemas = new StringBuilder();

            foreach (var item in AllApplications.Values.OrderBy(x => x.Name))
            {
                if (item.Schemas?.Count != 0 && !ShouldAppBeIgnored(item.Name))
                {
                    allSchemas.AppendLine(HtmlGenerator.CreateTag("h3", item.Name));
                    HtmlGenerator.CreateUl(item.Schemas.OrderBy(c => c), ref allSchemas);
                }
            }
            SaveToAzure(allSchemas.ToString(), "Overview£Schemas£" + WikiBlob.SanitizeV2(Config.Name));
        }

        private void DocumentTransformations()
        {
            DLog.TraceEvent(TraceEventType.Information, DTEId, $"{Name}\\{EnvInfo.Name}: Documenting and uploading transformations");
            Int32[] count = new Int32[1];
            Console.Write(count[0]);
            
            DocumentTransformationsRecursive(AppRootDir, count);
            Console.WriteLine();
        }

        private void DocumentTransformationsRecursive(DirectoryInfo dir, Int32[] count)
        {
            foreach (var orgFile in dir.EnumerateFiles())
            {
                if (orgFile.Extension.Equals(".xsl", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        String appName = GetChildFolderName(AppRootDir.FullName, orgFile.FullName);

                        if (ShouldAppBeIgnored(appName))
                            continue;

                        String key = "Transformation£" + DotEncode(appName) + "£" + DotEncode(orgFile.Name.Replace(".xsl", ""));
                        String lnk = CreateWikiLink(key, orgFile.Name.Replace(".xsl", ""));

                        if (AllApplications.ContainsKey(appName))
                        {
                            AllApplications[appName].Transformations.Add(lnk);
                        }

                        String content = File.ReadAllText(orgFile.FullName);
                        content = "<pre><code>" + content.Replace("<", "&lt;").Replace(">","&gt;") + "</code></pre>";
                        SaveToAzure(content, key);

                        count[0] = count[0] + 1;
                        Console.Write("\r" + count[0]);
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine();
                        DLog.TraceEvent(TraceEventType.Warning, DTEId, $"{Name}\\{EnvInfo.Name}: Exception when documenting transformation {orgFile.Name}. {ex.Message}");
                        DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"{Name}\\{EnvInfo.Name}: Exception when documenting transformation {orgFile.FullName}. {ex.ToString()}");
                    }
                }
            }
            foreach (var folder in dir.EnumerateDirectories())
            {
                DocumentTransformationsRecursive(folder, count);
            }
        }

        private void DocumentMapsAndSchemas()
        {
            DLog.TraceEvent(TraceEventType.Information, DTEId, $"{Name}\\{EnvInfo.Name}: Documenting and uploading new/updated schemas/maps");
            DocumentMapsAndSchemasRecursive(AppRootDir);
            Console.WriteLine();
        }

        private bool IsLegacy(FileInfo artifact)
        {
            try
            {
                DirectoryInfo solutionDir = GetChildFolderPath(AppRootDir, artifact);
                DirectoryInfo projDir = GetChildFolderPath(solutionDir, artifact);
                FileInfo solutionFile = solutionDir.GetFiles("*.sln").FirstOrDefault();

                foreach (String line in File.ReadLines(solutionFile.FullName))
                {
                    if (line.IndexOf("\"" + projDir.Name + "\\") >= 0)
                    {
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                DLog.TraceEvent(TraceEventType.Warning, DTEId, $"{Name}\\{EnvInfo.Name}: Failed to determine legacy for artifact {artifact.Name}. {ex.Message}");
                DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"{Name}\\{EnvInfo.Name}: Failed to determine legacy for artifact {artifact.FullName}. {ex.ToString()}");
            }
            return true;
        }

        private void DocumentMapsAndSchemasRecursive(DirectoryInfo dir)
        {
            Console.Write("\r  > Maps: " + MapsUpToDate + "|" + MapsNewOrUpdated + "  Schemas: " + SchemasUpToDate + "|" + SchemasNewOrUpdated);
            foreach (var orgFile in dir.EnumerateFiles())
            {
                if (orgFile.Extension.Equals(".btm", StringComparison.OrdinalIgnoreCase))
                {
                    if (IsLegacy(orgFile))
                        continue;

                    Console.Write("\r  > Maps: " + MapsUpToDate + "|" + MapsNewOrUpdated + "  Schemas: " + SchemasUpToDate + "|" + SchemasNewOrUpdated);
                    try
                    {
                        FileInfo docFile = GetNewPath(orgFile, ".btm", ".html");
                        String mapKey = AddMapToIndex(docFile);

                        String hash = null;
                        if (!DocIsUpToDate(docFile, orgFile, ref hash))
                        {
                            MapsNewOrUpdated++;
                            DocumentMap(docFile, orgFile, ref hash);
                            SaveToAzure(docFile, mapKey);
                            UploadRawMap(orgFile, mapKey + "€btm");
                        }
                        else
                            MapsUpToDate++;
                    }
                    catch (Exception ex)
                    {
                        DLog.TraceEvent(TraceEventType.Warning, DTEId, $"{Name}\\{EnvInfo.Name}: Failed to document map {orgFile.Name}. {ex.Message}");
                        DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"{Name}\\{EnvInfo.Name}: Failed to document map {orgFile.FullName}. {ex.ToString()}");
                    }
                }
                else if (orgFile.Extension.Equals(".xsd", StringComparison.OrdinalIgnoreCase))
                {
                    if (IsLegacy(orgFile))
                        continue;

                    Console.Write("\r  > Maps: " + MapsUpToDate + "|" + MapsNewOrUpdated + "  Schemas: " + SchemasUpToDate + "|" + SchemasNewOrUpdated);
                    try
                    {
                        FileInfo docFile = GetNewPath(orgFile, ".xsd", ".html");
                        String schemaKey = AddSchemaToIndex(docFile);

                        String hash = null;
                        if (!DocIsUpToDate(docFile, orgFile, ref hash))
                        {
                            SchemasNewOrUpdated++;
                            DocumentSchema(docFile, orgFile, ref hash);
                            SaveToAzure(docFile, schemaKey);
                            UploadRawSchema(orgFile, schemaKey + "€xsd");
                        }
                        else
                            SchemasUpToDate++;
                    }
                    catch (Exception ex)
                    {
                        DLog.TraceEvent(TraceEventType.Warning, DTEId, $"{Name}\\{EnvInfo.Name}: Failed to document schema {orgFile.Name}. {ex.Message}");
                        DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"{Name}\\{EnvInfo.Name}: Failed to document schema {orgFile.FullName}. {ex.ToString()}");
                    }
                }
            }

            foreach (var folder in dir.EnumerateDirectories())
            {
                DocumentMapsAndSchemasRecursive(folder);
            }
        }

        private void UploadRawSchema(FileInfo orgFile, string schemaKey)
        {
            try
            {
                if(Config.UploadRawSchemas.HasValue && Config.UploadRawSchemas.Value)
                    SaveToAzure(orgFile, WikiBlob.Combine("File", schemaKey));
            }
            catch (Exception ex)
            {
                DLog.TraceEvent(TraceEventType.Warning, DTEId, $"{Name}\\{EnvInfo.Name}: Failed to upload raw schema {orgFile.Name}. {ex.Message}");
                DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"{Name}\\{EnvInfo.Name}: Failed to upload raw schema {orgFile.FullName}. {ex.ToString()}");
            }
        }

        private void UploadRawMap(FileInfo orgFile, string mapKey)
        {
            try
            {
                if (Config.UploadRawMaps.HasValue && Config.UploadRawMaps.Value)
                    SaveToAzure(orgFile, WikiBlob.Combine("File", mapKey));
            }
            catch (Exception ex)
            {
                DLog.TraceEvent(TraceEventType.Warning, DTEId, $"{Name}\\{EnvInfo.Name}: Failed to upload raw map {orgFile.Name}. {ex.Message}");
                DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"{Name}\\{EnvInfo.Name}: Failed to upload raw map {orgFile.FullName}. {ex.ToString()}");
            }
        }

        private bool DocIsUpToDate(FileInfo docFile, FileInfo orgFile, ref string hash, bool createDir = true)
        {
            if (createDir)
                docFile.Directory.Create();

            if (docFile.Exists)
            {
                return IsHashEqual(docFile, orgFile, ref hash);
            }
            return false;
        }

        private void UpdateHash(FileInfo orgFile, FileInfo docFile, ref string hash)
        {
            if (hash == null)
                hash = BitConverter.ToString(MD5.Create().ComputeHash(orgFile.OpenRead())).Replace("-", "").ToLower();

            File.WriteAllText(docFile.FullName + ".hash", hash);
        }

        private void DocumentMap(FileInfo docFile, FileInfo orgFile, ref string hash)
        {
            if (docFile.Exists)
                File.Delete(docFile.FullName);

            UpdateHash(orgFile, docFile, ref hash);

            Process process = new Process();
            process.StartInfo.FileName = Path.Combine(DocRootDir, Msxsl);
            process.StartInfo.Arguments = "\"" + orgFile.FullName + "\" \"" + Path.Combine(DocRootDir, TransformMapXslt) + "\" -o \"" + docFile.FullName + "\"";
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            process.Start();
            process.WaitForExit();
            HandleCusotomXslDocumentation(docFile, orgFile);
        }

        private void HandleCusotomXslDocumentation(FileInfo docFile, FileInfo orgFile)
        {
            // get xsl file
            string fileNameWithoutExtension = docFile.Name.Substring(0, docFile.Name.Length - 5); // .html = 5 characters
            string xslFilePath = Directory.GetFiles(orgFile.Directory.FullName, fileNameWithoutExtension + "*.xsl").FirstOrDefault();
            if (!string.IsNullOrEmpty(xslFilePath))
            {
                XmlDocument htmlDoc = new XmlDocument();
                try
                {
                    // create documenter instance and run XsltDocumenter for xsl file
                    XsltDocumenter customXslDocumenter = new XsltDocumenter();
                    List<KeyValuePair<string, string>> resultKeyValueList = customXslDocumenter.DoMapDocumentation(xslFilePath);
                    // get generated HTML file for replacing empty contents
                    string htmlString = File.ReadAllText(docFile.FullName);
                    htmlDoc.LoadXml(htmlString);

                    // get XmlNode in HTML document to replace
                    XmlNode divNode = htmlDoc.SelectSingleNode(@"/html/body/div[@id='pageDirect1']");
                    XmlNode emptyTableHtmlDivNode = divNode.FirstChild;

                    // create Div HTML node to replace
                    StringBuilder sb = new StringBuilder();
                    sb.Append("<doc><table border=\"1\"><tr><th width=\"40 %\">From</th><th width=\"40%\">To</th><th width=\"20%\">Label</th></tr>");
                    foreach (var keyValue in resultKeyValueList)
                    {
                        sb.Append(string.Format("<tr><td>{0}</td><td>{1}</td><td /></tr>", keyValue.Key, keyValue.Value));
                    }
                    sb.Append("</table></doc>");

                    // Import and replace new node
                    XmlDocument dataDocument = new XmlDocument();
                    dataDocument.LoadXml(sb.ToString());
                    XmlNode dataHtmlDivNode = dataDocument.DocumentElement.FirstChild;
                    dataHtmlDivNode = htmlDoc.ImportNode(dataHtmlDivNode, true);
                    divNode.ReplaceChild(dataHtmlDivNode, emptyTableHtmlDivNode);
                }
                catch (Exception ex)
                {
                    DLog.TraceEvent(TraceEventType.Error, 1, "Unable process Custom XSLT documentation" + ex.Message);
                    throw;
                }

                try
                {
                    // remove old HTML file and create new
                    File.Delete(docFile.FullName);
                    string resultHtml = htmlDoc.OuterXml.Replace("Direct Node-to-Node Links (No Functoids)", "Custom XSLT Node Links").Replace("�", " ");
                    File.WriteAllText(docFile.FullName, resultHtml);
                }
                catch (Exception ex)
                {
                    DLog.TraceEvent(TraceEventType.Error, 1, "Can not read or write HTML doc file for mapp documentation" + ex.Message);
                    throw;
                }
            }
        }

        private String AddMapToIndex(FileInfo docFile)
        {
            String appName = GetChildFolderName(DocSourceDir, docFile.FullName);
            String key = "Map£" + DotEncode(appName) + "£" + DotEncode(docFile.Name.Replace(".html", ""));
            String lnk = CreateWikiLink(key, docFile.Name.Replace(".html", ""));

            if (Config.UploadRawMaps.HasValue && Config.UploadRawMaps.Value)
                lnk += CreateWikiFileLink(WikiBlob.Combine("File", key + "€btm"), " [btm]");

            if (AllApplications.ContainsKey(appName))
            {
                AllApplications[appName].Maps.Add(lnk);
            }
            return key;
        }

        private void DocumentSchema(FileInfo docFile, FileInfo orgFile, ref string hash)
        {
            if (docFile.Exists)
                File.Delete(docFile.FullName);

            UpdateHash(orgFile, docFile, ref hash);

            try
            {
                Constants.DisableConsole();
                Xslt.Load(DocRootDir + "\\" + TransformSchemaXsl);
                Xslt.Transform(orgFile.FullName, docFile.FullName);
                Constants.EnableConsole();
            }
            catch (XsltException ex)
            { 
                Constants.EnableConsole();
                Console.WriteLine();
                if(ex.Message.Contains("contains two dashes"))
                    DLog.TraceEvent(TraceEventType.Warning, DTEId, $"{Name}\\{EnvInfo.Name}: Schema transform failed for {orgFile.Name}. Invalid xml comment(s).");
                else
                    DLog.TraceEvent(TraceEventType.Warning, DTEId, $"{Name}\\{EnvInfo.Name}: Schema transform failed for {orgFile.Name}. {ex.Message}");
                DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"{Name}\\{EnvInfo.Name}: Schema transform failed for {orgFile.FullName}. {ex.ToString()}");
                File.WriteAllText(docFile.FullName, $"<p>Schema transform failed for this schema, most likely due to invalid xml.</p><pre>{ex.Message}</pre>");
            }
        }

        private String AddSchemaToIndex(FileInfo docFile)
        {
            String appName = GetChildFolderName(DocSourceDir, docFile.FullName);
            String key = "Schema£" + DotEncode(appName) + "£" + DotEncode(docFile.Name.Replace(".html", ""));
            String lnk = CreateWikiLink(key, docFile.Name.Replace(".html", ""));

            if (Config.UploadRawSchemas.HasValue && Config.UploadRawSchemas.Value)
                lnk += CreateWikiFileLink(WikiBlob.Combine("File", key + "€xsd"), " [xsd]");

            if (AllApplications.ContainsKey(appName))
            {
                AllApplications[appName].Schemas.Add(lnk);
            }
            return key;
        }

        private bool IsHashEqual(FileInfo docFile, FileInfo orgFile, ref string hash, bool createIfNotEqual = true)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = orgFile.OpenRead())
                {
                    hash = BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLower();
                    String hash2 = File.ReadAllText(docFile.FullName + ".hash").Replace(System.Environment.NewLine, "");
                    bool equal = hash.Equals(hash2, StringComparison.OrdinalIgnoreCase);
                    return equal;
                }
            }
        }

        public void SaveToAzure(FileInfo path, string key)
        {
            if ((SkipSchemaUpload && WikiBlob.IsSchema(key, true)) || (SkipMapUpload && WikiBlob.IsMap(key, true)))
                return;

            try
            {
                DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"{Name}\\{EnvInfo.Name}: Uploading to azure: {key}, File:{path.FullName}");
                AzureStorage.SetBlobContentToFile(key, path);
            }
            catch (Exception ex)
            {
                DLog.TraceEvent(TraceEventType.Error, DTEId, $"{Name}\\{EnvInfo.Name}: Azure upload failed: {ex.ToString()}");
            }
        }

        public void SaveToAzure(String content, string key)
        {
            try
            {
                DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"{Name}\\{EnvInfo.Name}: Uploading to azure: {key}, Content-Length:{content.Length}");
                AzureStorage.SetBlobContentAsString(key, content);
            }
            catch (Exception ex)
            {
                DLog.TraceEvent(TraceEventType.Error, DTEId, $"{Name}\\{EnvInfo.Name}: Azure upload failed: {ex.ToString()}");
            }
        }

        public void SaveToAzureAndLocal(string path, string key, string content)
        {
            try
            {
                DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"{Name}\\{EnvInfo.Name}: Uploading to azure and local: {key}, Content-Length:{content.Length}, Path:{path}");

                (new FileInfo(path)).Directory.Create();
                File.WriteAllText(path, content, UTF8Encoding.UTF8);

                AzureStorage.SetBlobContentAsString(key, content);
            }
            catch (Exception ex)
            {
                DLog.TraceEvent(TraceEventType.Error, DTEId, $"{Name}\\{EnvInfo.Name}: Azure upload failed: {ex.ToString()}");
            }
        }

        //private void MapMapSchemasToSchemas()
        //{
        //    foreach (var v in Schemas.Keys)
        //    {
        //        try
        //        {
        //            FileInfo fi = new FileInfo(v);
        //            FileInfo[] btproj = fi.Directory.GetFiles("*.btproj");
        //            if (btproj.Count() == 0)
        //            {
        //                btproj = fi.Directory.Parent.GetFiles("*.btproj");
        //                if (btproj.Count() == 0)
        //                {
        //                    btproj = fi.Directory.Parent.Parent.GetFiles("*.btproj");
        //                    if (btproj.Count() == 0)
        //                    {
        //                        Debug.WriteLine("No btproj found: " + fi.Directory.FullName);
        //                    }
        //                    else
        //                    {
        //                        String root = File.ReadAllText(btproj.First().FullName);
        //                        int idx = root.IndexOf("<RootNamespace>");
        //                        root = root.Substring(idx + "<RootNamespace>".Length, root.Length - idx - "<RootNamespace>".Length);
        //                        root = root.Substring(0, root.IndexOf("<"));
        //                        Console.WriteLine(root + "." + fi.Name);
        //                    }
        //                }
        //                else
        //                {
        //                    String root = File.ReadAllText(btproj.First().FullName);
        //                    int idx = root.IndexOf("<RootNamespace>");
        //                    root = root.Substring(idx + "<RootNamespace>".Length, root.Length - idx - "<RootNamespace>".Length);
        //                    root = root.Substring(0, root.IndexOf("<"));
        //                    Console.WriteLine(root + "." + fi.Name);
        //                }
        //            }
        //            else
        //            {
        //                String root = File.ReadAllText(btproj.First().FullName);
        //                int idx = root.IndexOf("<RootNamespace>");
        //                root = root.Substring(idx + "<RootNamespace>".Length, root.Length - idx - "<RootNamespace>".Length);
        //                root = root.Substring(0, root.IndexOf("<"));
        //                Console.WriteLine(root + "." + fi.Name);
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            Debug.WriteLine(ex.Message);
        //        }

        //    }
        //}

        public FileInfo GetNewPath(FileInfo f, string oldExt, string newExt)
        {
            String n = f.FullName.Replace(AppRootDir.FullName, DocSourceDir);
            return new FileInfo(n.Replace(oldExt, newExt));
        }

        private string _btdocPath;
        public String GetBizTalkDocumenterPath(string subfolder = null)
        {
            if (subfolder == null)
                return Path.Combine(Path.GetTempPath(), "DTFS", BizTalkDocumenterFolderName);
            else
                return Path.Combine(Path.GetTempPath(), "DTFS", BizTalkDocumenterFolderName, subfolder);
        }

        public bool ShouldAppBeIgnored(String appName)
        {
            return Config.IgnoreApps.FirstOrDefault(x => appName.IndexOf(x) >= 0) != null;
        }

        #region Static Methods

        public static String DotEncode(String str)
        {
            return str.Replace(".", "€");
        }

        public static String DotDecode(String str)
        {
            return str.Replace("€", ".");
        }

        /// <summary>
        /// Extracts a part of a string
        /// </summary>
        /// <param name="content">The string</param>
        /// <param name="prefix">The preceding characters</param>
        /// <param name="suffix">The succeding characters</param>
        /// <returns>A substring bewteen a prefix and a suffix</returns>
        public static string ExtractPart(string content, string prefix, string suffix)
        {
            int i = content.IndexOf(prefix);
            String name = content.Substring(i + prefix.Length);
            i = name.IndexOf(suffix);
            return name.Substring(0, i);
        }

        public static StringBuilder TrimFileStartAndEnd(FileInfo file, int lineStart, int lineEnd = 0)
        {
            int count = lineStart;
            string[] lines = File.ReadAllLines(file.FullName);

            if (lineStart + lineEnd > lines.Length)
                throw new ArgumentException("Cannot remove more lines than exists in the file!");

            StringBuilder sb = new StringBuilder();
            
            while(count < (lines.Length-lineEnd))
            {
                sb.AppendLine(lines[count++]);
            }
            return sb;
        }

        public static string GetChildFolderName(string parent, string path)
        {
            if (parent.ElementAt(parent.Length - 1) != '\\')
                parent = parent + "\\";
            String name = path.Replace(parent, "");
            return name.Substring(0, name.IndexOf(@"\"));
        }

        public static DirectoryInfo GetChildFolderPath(string parent, string path)
        {
            return new DirectoryInfo(Path.Combine(parent, GetChildFolderName(parent, path)));
        }

        public static DirectoryInfo GetChildFolderPath(DirectoryInfo parent, FileInfo path)
        {
            if (path.Directory.FullName == parent.FullName)
                return null;

            DirectoryInfo helper = path.Directory;

            do
            {
                if (helper.Parent.FullName == parent.FullName)
                {
                    return helper;
                }
                helper = helper.Parent;
            }
            while (helper.Parent != null);

            return null;
        }

        public static string CreateWikiLink(string lnk, string title = null, bool dotEncodeLink = false)
        {
            return HtmlGenerator.CreateLink("/Wiki/Page/" + lnk, title, null, null, dotEncodeLink);
        }

        public static string CreateWikiFileLink(string lnk, string title = null, bool dotEncodeLink = false)
        {
            return HtmlGenerator.CreateLink("/File/AutoUploaded/" + lnk, title, null, null, dotEncodeLink);
        }

        /// <summary>
        /// Replaces the image links generated by biztalk documenter
        /// with links relative to the wiki page.
        /// </summary>
        /// <param name="s">The s.</param>
        /// <returns></returns>
        public static String AlterImageLinks(String s)
        {
            return s.Replace("src=\"../", "src=\"/Content/Images/").Replace("SRC=\"../", "src=\"/Content/Images/");
        }

        public static String AlterImageLinks(FileInfo f)
        {
            return File.ReadAllText(f.FullName).Replace("src=\"../", "src=\"/Content/Images/").Replace("SRC=\"../", "src=\"/Content/Images/");
        }

        public static int AppendLinks(String header, StringBuilder sb, List<String> items, bool ignoreIfEmpty = false)
        {
            int added = 0;

            if (ignoreIfEmpty && items.Count == 0)
                return added;

            sb.Append("<h3>" + header + "</h3>");
            foreach (var m in items.OrderBy(x => x))
            {
                added++;
                sb.Append("<p>").Append(m).Append("</p>");
            }

            if (items.Count <= 0)
            {
                sb.Append("<p><em>Could not find any " + header + "</em></p>");
            }
            return added;
        }

        #endregion
    }
}