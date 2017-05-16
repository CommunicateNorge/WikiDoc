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
    public class WikiConfigurationValidator
    {

        private static void ValidateEnvironmentInfo(WikiConfiguration config)
        {
            DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"Config: Prop: {nameof(config.EnvironmentInfo)}.{nameof(config.EnvironmentInfo.Count)} Val: {config.EnvironmentInfo?.Count}");
            if (config.EnvironmentInfo.Count <= 0)
                throw new ArgumentException("Specify at least 1 environment", "EnvironmentInfo");

            Dictionary<String, String> environmentNames = new Dictionary<string, string>();
            Dictionary<String, String> environmentContainers = new Dictionary<string, string>();

            int c = 0;
            foreach (EnvironmentInfo einfo in config.EnvironmentInfo)
            {
                DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"Config: Prop: {nameof(config.EnvironmentInfo)}[{c}].{nameof(einfo.Name)} Val: {einfo.Name}");
                DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"Config: Prop: {nameof(config.EnvironmentInfo)}[{c++}].{nameof(einfo.Container)} Val: {einfo.Container}");


                if (String.IsNullOrEmpty(einfo.Name) || String.IsNullOrEmpty(einfo.Container))
                    throw new ArgumentException("Each environment must have a Name and a Container", "EnvironmentInfo");

                if (environmentNames.ContainsKey(einfo.Name))
                    throw new ArgumentException("Each environment name must be unique", "EnvironmentInfo");

                if (environmentContainers.ContainsKey(einfo.Name))
                    throw new ArgumentException("Each environment container must be unique", "EnvironmentInfo");

                environmentNames.Add(einfo.Name, "N/A");
                environmentContainers.Add(einfo.Container, "N/A");
            }
            if (config.GetPrimaryEnvironment().Container != config.PrimaryAzureStorageContainerName)
                throw new ArgumentException("The primary environment must be identical to the first EnvironmentInfo");
        }

        private static void ValidateCredentials(WikiConfiguration config)
        {
            Dictionary<String, Tuple<String, String>> creds = new Dictionary<string, Tuple<string, string>>();
            foreach (var item in config.BizTalk)
            {
                if (item.UseTFS())
                {
                    if (creds.ContainsKey(item.Domain))
                    {
                        item.User = item.User ?? creds[item.Domain].Item1;
                        item.Pwd = item.Pwd ?? creds[item.Domain].Item2;
                    }
                    else
                    {
                        item.User = item.User ?? Prompt($"{item.Name} TFS username:");
                        item.Pwd = item.Pwd ?? Prompt($"{item.Name} TFS password:", true);
                        creds.Add(item.Domain, new Tuple<String, String>(item.User, item.Pwd));
                    }
                }
            }

            foreach (var item in config.WebJobs)
            {
                if (item.UseTFS())
                {
                    if (creds.ContainsKey(item.Domain))
                    {
                        item.User = item.User ?? creds[item.Domain].Item1;
                        item.Pwd = item.Pwd ?? creds[item.Domain].Item2;
                    }
                    else
                    {
                        item.User = item.User ?? Prompt($"{item.Name} TFS username:");
                        item.Pwd = item.Pwd ?? Prompt($"{item.Name} TFS password:", true);
                        creds.Add(item.Domain, new Tuple<String, String>(item.User, item.Pwd));
                    }
                }
            }
        }

        private static void ValidateBizTalk(WikiConfiguration config)
        {
            int envMax = config.EnvironmentInfo.Count - 1;
            int c = 0;
            foreach (BizTalk bt in config.BizTalk)
            {
                DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"Config: Prop: {nameof(config.BizTalk)}[{c}].{nameof(bt.Name)} Val: {bt.Name}");
                DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"Config: Prop: {nameof(config.BizTalk)}[{c}].{nameof(bt.Type)} Val: {bt.Type}");
                DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"Config: Prop: {nameof(config.BizTalk)}[{c}].{nameof(bt.Domain)} Val: {bt.Domain}");
                DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"Config: Prop: {nameof(config.BizTalk)}[{c}].{nameof(bt.Environments)} Val: {String.Concat(bt.Environments)}");
                DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"Config: Prop: {nameof(config.BizTalk)}[{c}].{nameof(bt.IgnoreApps)} Val: {String.Concat(bt.IgnoreApps)}");
                DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"Config: Prop: {nameof(config.BizTalk)}[{c}].{nameof(bt.Pwd)} Val-Length: {bt.Pwd?.Length}");
                DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"Config: Prop: {nameof(config.BizTalk)}[{c}].{nameof(bt.TfsUri)} Val: {bt.TfsUri}");
                DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"Config: Prop: {nameof(config.BizTalk)}[{c++}].{nameof(bt.User)} Val: {bt.User}");

                if (bt.Type != "TFS" && bt.Type != "Local")
                    throw new ArgumentException("BizTalk must be of type \"TFS\" or \"Local\"", "BizTalk.Type");

                if (bt.Environments.FirstOrDefault(x => x.EnvNo > envMax || x.EnvNo < 0) != null)
                    throw new ArgumentException("Invalid environment number", "BizTalk.Environments");

                if (bt.Environments.Distinct().Count() != bt.Environments.Count)
                    throw new ArgumentException("An environment may not be repeated within a BizTalk configuration", "BizTalk.Environments");

                if (bt.Environments.FirstOrDefault(z => String.IsNullOrEmpty(z.Address)) != null)
                    throw new ArgumentException("An environment address may not be empty", "BizTalk.Environments");

                if (bt.Type == "TFS" && String.IsNullOrEmpty(bt.Domain))
                    throw new ArgumentException("Domain may not be empty when of type TFS", "BizTalk.Type");

                if (bt.Type == "TFS" && String.IsNullOrEmpty(bt.TfsUri))
                    throw new ArgumentException("TfsUri may not be empty when of type TFS", "BizTalk.Type");

                bt.Environments.ForEach(i =>
                {
                    VerifyBTDocumenterPath(config.DocDir.FullName, i.GetEnvironment(config.EnvironmentInfo).Name);
                });
            }
        }

        private static string Prompt(string v, bool hideInput = false)
        {
            Console.WriteLine(v);
            if (hideInput) return ReadPassword();
            return Console.ReadLine();
        }

        public static string ReadPassword()
        {
            string password = "";
            ConsoleKeyInfo info = Console.ReadKey(true);
            while (info.Key != ConsoleKey.Enter)
            {
                if (info.Key != ConsoleKey.Backspace)
                {
                    Console.Write("*");
                    password += info.KeyChar;
                }
                else if (info.Key == ConsoleKey.Backspace)
                {
                    if (!string.IsNullOrEmpty(password))
                    {
                        // remove one character from the list of password characters
                        password = password.Substring(0, password.Length - 1);
                        // get the location of the cursor
                        int pos = Console.CursorLeft;
                        // move the cursor to the left by one character
                        Console.SetCursorPosition(pos - 1, Console.CursorTop);
                        // replace it with space
                        Console.Write(" ");
                        // move the cursor to the left by one character again
                        Console.SetCursorPosition(pos - 1, Console.CursorTop);
                    }
                }
                info = Console.ReadKey(true);
            }
            // add a new line because user pressed enter at the end of their password
            Console.WriteLine();
            return password;
        }

        private static bool VerifyBTDocumenterPath(string doctoolsPath, string name)
        {
            //These directories are now created if they dont exist, no validation needed.
            return true;
            //DirectoryInfo btDocumenterPath = new DirectoryInfo(Path.Combine(doctoolsPath, "BTDocumenter_" + name));
            //DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"Config: Prop: {nameof(btDocumenterPath)} Val: {btDocumenterPath.FullName}");

            //if (btDocumenterPath.Exists)
            //    return true;

            //throw new ArgumentException("BizTalk Documenter path for " + name + " is missing. Should be: \"" + btDocumenterPath.FullName + "\"");
        }

        private static void ValidateOAuth(AuthenticationInfo authentication, String name, bool validateUserCreds = false)
        {
            if(validateUserCreds)
            {
                authentication.Username = authentication.Username ?? Prompt($"{name} Username:");
                authentication.Pwd = authentication.Pwd ?? Prompt($"{name} Pwd:", true);
            }
            if (String.IsNullOrEmpty(authentication.AuthenticationEndpoint))
                throw new ArgumentNullException($"{name}:{nameof(authentication)}.{nameof(authentication.AuthenticationEndpoint)}");

            if (String.IsNullOrEmpty(authentication.ClientId))
                throw new ArgumentNullException($"{name}:{nameof(authentication)}.{nameof(authentication.ClientId)}");

            if (String.IsNullOrEmpty(authentication.Resource))
                throw new ArgumentNullException($"{name}:{nameof(authentication)}.{nameof(authentication.Resource)}");

            if (String.IsNullOrEmpty(authentication.ClientSecret))
                throw new ArgumentNullException($"{name}:{nameof(authentication)}.{nameof(authentication.ClientSecret)}");
        }

        private static void ValidateApiServices(WikiConfiguration config)
        {
            int envMax = config.EnvironmentInfo.Count - 1;
            int c = 0;
            foreach (ApiService api in config.ApiServices)
            {
                AuthType type = api.GetAuthType();

                DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"Config: Prop: {nameof(config.ApiServices)}[{c}].{nameof(api.Name)} Val: {api.Name}");
                DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"Config: Prop: {nameof(config.ApiServices)}[{c}].{nameof(api.Type)} Val: {api.Type}");
                if (type != AuthType.None)
                {
                    DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"Config: Prop: {nameof(config.ApiServices)}[{c}].{nameof(api.Authentication.Pwd)} Val-Length: {api.Authentication.Pwd?.Length}");
                    DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"Config: Prop: {nameof(config.ApiServices)}[{c}].{nameof(api.Authentication.Username)} Val: {api.Authentication.Username}");
                    DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"Config: Prop: {nameof(config.ApiServices)}[{c}].{nameof(api.Authentication.Type)} Val: {api.Authentication.Type}");
                    DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"Config: Prop: {nameof(config.ApiServices)}[{c}].{nameof(api.Authentication.Resource)} Val: {api.Authentication.Resource}");
                    DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"Config: Prop: {nameof(config.ApiServices)}[{c}].{nameof(api.Authentication.AuthenticationEndpoint)} Val: {api.Authentication.AuthenticationEndpoint}");
                    DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"Config: Prop: {nameof(config.ApiServices)}[{c}].{nameof(api.Authentication.ClientId)} Val: {api.Authentication.ClientId}");
                    DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"Config: Prop: {nameof(config.ApiServices)}[{c}].{nameof(api.Authentication.ClientSecret)} Val-Length: {api.Authentication.ClientSecret?.Length}");
                }
                DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"Config: Prop: {nameof(config.ApiServices)}[{c++}].{nameof(api.Environments)} Val: {String.Concat(api.Environments)}");

                switch (type)
                {
                    case AuthType.Basic:
                        api.Authentication.Username = api.Authentication.Username ?? Prompt($"{api.Name} Username:");
                        api.Authentication.Pwd = api.Authentication.Pwd ?? Prompt($"{api.Name} Pwd:");
                        break;
                    case AuthType.OAuthApp:
                        ValidateOAuth(api.Authentication, api.Name);
                        break;
                    case AuthType.OAuthUser:
                        ValidateOAuth(api.Authentication, api.Name, true);
                        break;
                    case AuthType.None:
                        break;
					case AuthType.SAS:
						break;
					case AuthType.Invalid:
                        throw new ArgumentException($"Invalid authentication type \"{api.Authentication.Type}\" for API \"{api.Name}\". Possible values include: None, Basic, OAuthApp or OAuthUser.");
                }

                if (String.IsNullOrEmpty(api.Name))
                    throw new ArgumentNullException("ApiService.Name");

                if (api.Type != "Swagger" && api.Type != "ApiManagement")
                    throw new ArgumentException("Type must be swagger", "ApiService.Type");


                if (api.Environments.FirstOrDefault(x => x.EnvNo > envMax || x.EnvNo < 0) != null)
                    throw new ArgumentException("Invalid environment number", "ApiService.Environments");

                if (api.Environments.Distinct().Count() != api.Environments.Count)
                    throw new ArgumentException("An environment may not be repeated within a ApiService configuration", "ApiService.Environments");

                if (api.Environments.FirstOrDefault(z => String.IsNullOrEmpty(z.Address)) != null)
                    throw new ArgumentException("An environment address may not be empty", "ApiService.Environments");
            }
        }

        private static void ValidateWebJobs(WikiConfiguration config)
        {
            int envMax = config.EnvironmentInfo.Count - 1;
            int c = 0;
            foreach (WebJob wj in config.WebJobs)
            {
                DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"Config: Prop: {nameof(config.WebJobs)}[{c}].{nameof(wj.Name)} Val: {wj.Name}");
                DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"Config: Prop: {nameof(config.WebJobs)}[{c}].{nameof(wj.Pwd)} Val-Length: {wj.Pwd?.Length}");
                DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"Config: Prop: {nameof(config.WebJobs)}[{c}].{nameof(wj.Type)} Val: {wj.Type}");
                DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"Config: Prop: {nameof(config.WebJobs)}[{c}].{nameof(wj.User)} Val: {wj.User}");
                DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"Config: Prop: {nameof(config.WebJobs)}[{c}].{nameof(wj.TfsUri)} Val: {wj.TfsUri}");
                DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"Config: Prop: {nameof(config.WebJobs)}[{c++}].{nameof(wj.Environments)} Val: {String.Concat(wj.Environments)}");

                if (String.IsNullOrEmpty(wj.Name))
                    throw new ArgumentNullException($"{nameof(config.WebJobs)}.{nameof(wj.Name)}");

                if (wj.Environments.FirstOrDefault(x => x.EnvNo > envMax || x.EnvNo < 0) != null)
                    throw new ArgumentException("Invalid environment number", $"{nameof(config.WebJobs)}.{nameof(wj.Environments)}");

                if (wj.Environments.Distinct().Count() != wj.Environments.Count)
                    throw new ArgumentException($"An environment may not be repeated within a {nameof(config.WebJobs)} configuration", $"{nameof(config.WebJobs)}.{nameof(wj.Environments)}");

                if (wj.Environments.FirstOrDefault(z => String.IsNullOrEmpty(z.Address)) != null)
                    throw new ArgumentException("An environment address may not be empty", $"{nameof(config.WebJobs)}.{nameof(wj.Environments)}");
            }
        }

        private static void ValidateDatabases(WikiConfiguration config)
        {
            int envMax = config.EnvironmentInfo.Count - 1;
            int c = 0;
            foreach (Database db in config.Databases)
            {
                DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"Config: Prop: {nameof(config.Databases)}[{c}].{nameof(db.Name)} Val: {db.Name}");
                DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"Config: Prop: {nameof(config.Databases)}[{c++}].{nameof(db.Environments)} Val: {String.Concat(db.Environments)}");

                if (String.IsNullOrEmpty(db.Name))
                    throw new ArgumentNullException("Database.Name");

                if (db.Environments.FirstOrDefault(x => x.EnvNo > envMax || x.EnvNo < 0) != null)
                    throw new ArgumentException("Invalid environment number", "Database.Environments");

                if (db.Environments.Distinct().Count() != db.Environments.Count)
                    throw new ArgumentException("An environment may not be repeated within a Database configuration", "Database.Environments");

                if (db.Environments.FirstOrDefault(z => String.IsNullOrEmpty(z.Address)) != null)
                    throw new ArgumentException("An environment address may not be empty", "Database.Environments");
            }
        }

        /// <summary>
        /// Validates that a config isn't missing parameters and that it is consistent.
        /// Does not validate connectivity againts tfs, api's and databases.
        /// </summary>
        /// <param name="config">A wiki configuration</param>
        public static void Validate(WikiConfiguration config)
        {
            ValidateSections(config);

            ValidateCredentials(config);
            // Performes implicit validation because the DocDir property checks for the folder's existence, I know, bad form, but gah!
            DirectoryInfo di = config.DocDir;

            DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"Config: Prop: {nameof(config.Name)} Val: {config.Name}");
            if (String.IsNullOrEmpty(config.Name))
                throw new ArgumentNullException("Name");

            DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"Config: Prop: {nameof(config.PrimaryAzureStorage)} Val-Length: {config.PrimaryAzureStorage?.Length}");
            if (!Regex.IsMatch(config.PrimaryAzureStorage, "(.*)AccountName=(.+);AccountKey=(.+)"))
                throw new ArgumentException("Invalid connection-string", "PrimaryAzureStorage");

            DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"Config: Prop: {nameof(config.SecondaryAzureStorage)} Val-Length: {config.SecondaryAzureStorage?.Length}");
            if (!Regex.IsMatch(config.SecondaryAzureStorage, "(.*)AccountName=(.+);AccountKey=(.+)"))
                throw new ArgumentException("Invalid connection-string", "SecondaryAzureStorage");

            DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"Config: Prop: {nameof(config.PrimaryAzureStorageContainerName)} Val: {config.PrimaryAzureStorageContainerName}");
            if (String.IsNullOrEmpty(config.PrimaryAzureStorageContainerName))
                throw new ArgumentNullException("PrimaryAzureStorageContainerName");

            DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"Config: Prop: {nameof(config.PrimaryAzureStorageTableAudit)} Val: {config.PrimaryAzureStorageTableAudit}");
            if (String.IsNullOrEmpty(config.PrimaryAzureStorageTableAudit))
                throw new ArgumentNullException("PrimaryAzureStorageTableAudit");

            DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"Config: Prop: {nameof(config.PrimaryAzureStorageTableIndex)} Val: {config.PrimaryAzureStorageTableIndex}");
            if (String.IsNullOrEmpty(config.PrimaryAzureStorageTableIndex))
                throw new ArgumentNullException("PrimaryAzureStorageTableIndex");

            DLog.TraceEvent(TraceEventType.Verbose, DTEId, $"Config: Prop: {nameof(config.SecondaryAzureStorageContainerName)} Val: {config.SecondaryAzureStorageContainerName}");
            if (String.IsNullOrEmpty(config.SecondaryAzureStorageContainerName))
                throw new ArgumentNullException("SecondaryAzureStorageContainerName");

            if (config.TfsCacheDurationMin < 0)
                throw new ArgumentException("Value must be a positive integer", nameof(config.TfsCacheDurationMin));

            ValidateEnvironmentInfo(config);
            ValidateBizTalk(config);
            ValidateApiServices(config);
            ValidateDatabases(config);
            ValidateWebJobs(config);
        }

        private static void ValidateSections(WikiConfiguration config)
        {
            if (config.EnvironmentInfo == null)
            {
                throw new ArgumentException("No environments configured!");
            }

            if (config.ApiServices == null)
            { 
                DLog.TraceEvent(TraceEventType.Warning, DTEId, $"No API Services configured.");
                config.ApiServices = new List<ApiService>();
            }

            if (config.BizTalk == null)
            {
                DLog.TraceEvent(TraceEventType.Warning, DTEId, $"No BizTalk installations configured.");
                config.BizTalk = new List<BizTalk>();
            }
            if (config.CSharp == null)
            {
                config.CSharp = new List<CSharp>();
            }
            if (config.Databases == null)
            {
                DLog.TraceEvent(TraceEventType.Warning, DTEId, $"No Databases configured.");
                config.Databases = new List<Database>();
            }
            if (config.WebJobs == null)
            {
                DLog.TraceEvent(TraceEventType.Warning, DTEId, $"No WebJobs configured.");
                config.WebJobs = new List<WebJob>();
            }
        }
    }
}
