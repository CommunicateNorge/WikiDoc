using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static Wiki.Models.Constants;

namespace Wiki.Models
{
    public class EnvironmentInfo
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Container { get; set; }
        public int? Index { get; set; }
    }

    public class Environment : IComparable, IEqualityComparer<Environment>
    {
        public int EnvNo { get; set; }
        public string Address { get; set; }
        public EnvironmentInfo GetEnvironment(List<EnvironmentInfo> envs)
        {
            return envs.ElementAt(EnvNo);
        }

        public int CompareTo(object obj)
        {
            return EnvNo.CompareTo((obj as Environment).EnvNo);
        }

        public bool Equals(Environment x, Environment y)
        {
            return x.CompareTo(y) == 0;
        }

        public int GetHashCode(Environment obj)
        {
            return obj.EnvNo;
        }

        public override string ToString()
        {
            return $"{nameof(EnvNo)}:{EnvNo} {nameof(Address)}:{Address}";
        }
    }

    public class BizTalk : ITfsInfo
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string User { get; set; }
        public string Domain { get; set; }
        public string Pwd { get; set; }
        public string TfsUri { get; set; }

        public bool? UploadRawSchemas { get; set; }
        public bool? UploadRawMaps { get; set; }

        public bool UploadLogTexts { get; set; }

        public List<Environment> Environments { get; set; }
        public List<string> IgnoreApps { get; set; }

        public bool UseTFS()
        {
            return Type == "TFS";
        }
    }

    public enum AuthType
    {
        Basic,
        None,
        OAuthUser,
        OAuthApp,
        Invalid,
		SAS
    }

    public class ApiService
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public AuthenticationInfo Authentication { get; set; }
        public List<Environment> Environments { get; set; }
		public List<Header> Headers { get; set; }

		public bool UseTFS()
        {
            return Type == "TFS";
        }

        public AuthType GetAuthType()
        {
            if (Authentication == null)
                return AuthType.None;
            else
                return Authentication.GetAuthType();
        }
    }

    public class AuthenticationInfo
    {
        public String Username { get; set; }
        public String Pwd { get; set; }
        public String Type { get; set; }
        public String AuthenticationEndpoint { get; set; }
        public String Resource { get; set; }
        public String ClientId { get; set; }
        public String ClientSecret { get; set; }
        public String SAS { get; set; }

		public AuthType GetAuthType()
        {
            if (Type == "None")
                return AuthType.None;
            else if (Type == "Basic")
                return AuthType.Basic;
            else if (Type == "OAuthApp")
                return AuthType.OAuthApp;
            else if (Type == "OAuthUser")
                return AuthType.OAuthUser;
			else if (Type == "SAS")
				return AuthType.SAS;
			else
                return AuthType.Invalid;
        }
    }

    public class CSharp
    {
        public string Name { get; set; }
        public List<Environment> Environments { get; set; }
    }

    public class Database
    {
        public string Name { get; set; }
        public List<Environment> Environments { get; set; }
    }

	public class Header
	{
		public string Type { get; set; }
		public string Value { get; set; }
	}

	public class WebJob : ITfsInfo
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string User { get; set; }
        public string Domain { get; set; }
        public string Pwd { get; set; }
        public string TfsUri { get; set; }
        public List<Environment> Environments { get; set; }

        public bool UseTFS()
        {
            return Type == "TFS";
        }
    }

    public class WikiConfiguration
    {
        private WikiConfiguration(){ }
        private static WikiConfiguration me;
        public static WikiConfiguration GetConfiguration(bool skipValidation = false)
        {
            if (me != null)
                return me;

            ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App.config.json");
            DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"Config: Prop: {nameof(ConfigPath)} Val: {ConfigPath}");
            
            me = JsonConvert.DeserializeObject<WikiConfiguration>(File.ReadAllText(ConfigPath));
            if (me == null)
                throw new ArgumentException("Missing or malformed configuration!");

            if(!skipValidation)
                WikiConfigurationValidator.Validate(me);

            for (int i = 0; i < me.EnvironmentInfo.Count; i++)
            {
                if (me.EnvironmentInfo.ElementAt(i).Index == null)
                    me.EnvironmentInfo.ElementAt(i).Index = i;
            }

            return me;
        }

        public static string ConfigPath { get; set; }
        public string Name { get; set; }
        public string PrimaryAzureStorage { get; set; }
        public string SecondaryAzureStorage { get; set; }
        public string PrimaryAzureStorageContainerName { get; set; }
        public string PrimaryAzureStorageTableIndex { get; set; }
        public string PrimaryAzureStorageTableAudit { get; set; }
        public string SecondaryAzureStorageContainerName { get; set; }
        public string SandCastleFtpLocation { get; set; }
        public string SandCastleFtpUser { get; set; }
        public string SandCastleFtpPwd { get; set; }
        public long TfsCacheDurationMin { get; set; }
        public List<EnvironmentInfo> EnvironmentInfo { get; set; }
        public EnvironmentInfo GetEnvironment(Environment env)
        {
            return EnvironmentInfo.ElementAt(env.EnvNo);
        }
        public EnvironmentInfo GetEnvironment(String envName)
        {
            return EnvironmentInfo.Where(x => x.Name == envName).FirstOrDefault();
        }
        public EnvironmentInfo GetPrimaryEnvironment()
        {
            return EnvironmentInfo.ElementAt(0);
        }

        public List<BizTalk> BizTalk { get; set; }
        public List<ApiService> ApiServices { get; set; }
        public List<CSharp> CSharp { get; set; }
        public List<Database> Databases { get; set; }
        public List<WebJob> WebJobs { get; set; }


        private DirectoryInfo _solutionFolder;
        public DirectoryInfo SolutionFolder 
        { 
            get
            {
                if (_solutionFolder == null)
                {
                    DirectoryInfo runDir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                    DirectoryInfo solutionDir = runDir;

                    DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"Config: Prop: {nameof(solutionDir)} Val: {solutionDir.FullName}");

                    if (solutionDir.Exists)
                    {
                        _solutionFolder = new DirectoryInfo(solutionDir.FullName);
                    }
                    else
                    {
                        runDir = new FileInfo(new Uri(System.Reflection.Assembly.GetExecutingAssembly().CodeBase).LocalPath).Directory;
                        solutionDir = runDir.Parent.Parent.Parent;

                        DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"Config: Prop: {nameof(solutionDir)} Val: {solutionDir.FullName}");

                        if (solutionDir.Exists)
                            _solutionFolder = new DirectoryInfo(solutionDir.FullName);
                    }
                }
                if(_solutionFolder == null)
                    throw new DirectoryNotFoundException("Could not find solution directory");
                return _solutionFolder;
            }
        }

        private DirectoryInfo _docDir;
        public DirectoryInfo DocDir
        {
            get
            {
                if (_docDir == null)
                {
                    DirectoryInfo doctools = new DirectoryInfo(Path.Combine(SolutionFolder.FullName, "doctools"));
                    DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"Config: Prop: {nameof(doctools)} Val: {doctools.FullName}");

                    if (doctools.Exists)
                        _docDir = new DirectoryInfo(doctools.FullName);
                    else
                        throw new DirectoryNotFoundException("Could not find doctools directory");
                }
                return _docDir;
            }
        }
    }
}
