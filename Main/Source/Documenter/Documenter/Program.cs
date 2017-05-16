using AzureStorage;
using SearchIndexer;
//using SearchIndexer.v2;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Web;
using Wiki.Models;
using Wiki.Utilities;
using static Wiki.Models.Constants;

namespace Documenter
{
    class Program
    {
        public static WikiConfiguration ValidateConfig()
        {
            WikiConfiguration config = null;
            try
            {
                DLog.TraceEvent(TraceEventType.Information, DTEId, "Validating configuration");
                config = WikiConfiguration.GetConfiguration();
                DLog.TraceEvent(TraceEventType.Information, DTEIdM, "Configuration validated OK!");
            }
            catch (Exception ex)
            {
                DLog.TraceEvent(TraceEventType.Critical, DTEId, "Configuration validation failed!");
                DLog.TraceEvent(TraceEventType.Error, DTEId, "Error: " + ex.ToString());
                Console.ReadKey();
            }
            return config;
        }

        static void Main(string[] args)
        {
            DLog.TraceEvent(TraceEventType.Verbose, DTEId, "Documenter executable startet");
            DLog.Flush();
            WikiConfiguration config = ValidateConfig();
            if(config == null)
                return;

            Console.Title = "Documenting: " + config.Name;

            Stopwatch sw = new Stopwatch();
            Console.WriteLine("Document everything (d), Backup blob (b), Rebuild index (r) or All (a)?");
            ConsoleKeyInfo key = new ConsoleKeyInfo('q', ConsoleKey.Q, false, false, false);

            if (args.Length == 0)
            { 
                key = Console.ReadKey(true);
                DLog.TraceEvent(TraceEventType.Verbose, DTEId, "User input key: " + key.KeyChar);
            }
            else
            {
                DLog.TraceEvent(TraceEventType.Verbose, DTEId, "Documenter run with command-line arguments: " + String.Concat(args));
            }

            sw.Start();

            if (args.Contains("-backup") || (key.Key == ConsoleKey.B && Confirm("Backup blob?")))
            {
                DoBackup(AutoDocumenter.GetPrimaryStorage(), AutoDocumenter.GetSecondaryStorage());
            }

            if (args.Contains("-document") || (key.Key == ConsoleKey.D && Confirm("Start documenting?")))
            {
                StartDocumenting();
            }

            if (args.Contains("-reindex") || (key.Key == ConsoleKey.R && Confirm("Rebuild index?")))
            {
                RebuildSearchIndex(AutoDocumenter.GetPrimaryStorage());
            }

            if (args.Contains("-all") || (key.Key == ConsoleKey.A && Confirm("Run backup, documenter and indexer?")))
            {
                if(DoBackup(AutoDocumenter.GetPrimaryStorage(), AutoDocumenter.GetSecondaryStorage()))
                    if(StartDocumenting())
                        RebuildSearchIndex(AutoDocumenter.GetPrimaryStorage());
            }

            sw.Stop();
            DLog.TraceEvent(TraceEventType.Information, DTEId, $"Done! Total elapsed time for all operations: {sw.Elapsed.ToString()}");

            if (args.Length == 0)
                Console.ReadKey();
        }

        //SandCastleUploader scu = new SandCastleUploader("Sp€Api", null, "Main", new AzureBlobStorage(azureInfo));
        //scu.ParseSandCastleFiles();

        private static bool Confirm(String str)
        {
            Console.WriteLine(str + " (y/n)");
            if (Console.ReadKey(true).Key == ConsoleKey.Y)
                return true;
            return false;
        }

        private static bool DoBackup(AzureBlobStorage from, AzureBlobStorage to)
        {
            try
            {
                DLog.TraceEvent(TraceEventType.Information, DTEId, $"Backup initiated from {from.Container.StorageUri.PrimaryUri.ToString()} to {to.Container.StorageUri.PrimaryUri.ToString()}");
                AzureBlobBackuper bck = new AzureBlobBackuper(from, to);
                bck.Migrate(true, true, true);
                return true;
            }
            catch (Exception ex)
            {
                DLog.TraceEvent(TraceEventType.Critical, DTEId, $"Urecoverable error occured when backup-ing the documentation. {ex.ToString()}");
                return false;
            }
        }

        private static bool StartDocumenting()
        {
            try
            {
                AutoDocumenter ad = new AutoDocumenter();
                ad.Start();
                return true;
            }
            catch (Exception ex)
            {
                DLog.TraceEvent(TraceEventType.Critical, DTEId, $"Urecoverable error occured when documenting system. {ex.ToString()}");
                return false;
            }
        }

        private static bool RebuildSearchIndex(AzureBlobStorage storage)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            Console.Title = "Rebuilding the search-index";
            try
            {
                RecreateAndReindex(storage);
                DLog.TraceEvent(TraceEventType.Information, DTEId, $"Rebuilding the index completed in {sw.ElapsedMilliseconds} ms.");
                return true;
            }
            catch (Exception ex)
            {
                DLog.TraceEvent(TraceEventType.Critical, DTEId, $"Exception when rebuilding index. {ex.ToString()}");
                return false;
            }
            finally
            {
                sw.Stop();
            }
        }

        private static void RecreateAndReindex(AzureBlobStorage azure)
        {
            if (!Search.RecreateTable(azure))
                throw new Exception("Could not recreate table!");
            Search s = new Search(azure);
            //Search s = new Search(new StorageSQLite(""), azure, "tpdocindex");
            //Search s = new Search(new StorageAzureTable(azure, "tpdocindex2"), azure, "index");

            foreach (var str in azure.GetBlobList())
            {
                try
                {
                    string asd = HttpUtility.UrlDecode(str);

                    if (!WikiBlob.IsOldVersion(asd) && !WikiBlob.IsImage(asd, true) && !WikiBlob.IsFile(asd, true) && !WikiBlob.IsTransformation(asd, true) && !asd.EndsWith("£Code"))
                    {
                        if (WikiBlob.IsIntegration(asd, true))
                        {
                            DLog.TraceEvent(TraceEventType.Information, DTEId, $"Indexing {asd}");
                            s.AddDocumentToIndex(asd);
                        }
                        if (WikiBlob.IsMap(asd, true))
                        {
                            //Console.WriteLine(asd);
                            //s.AddDocumentToIndex(asd);
                        }
                        else if (WikiBlob.IsSchema(asd, true))
                        {
                            //Console.WriteLine(asd);
                            //s.AddDocumentToIndex(asd);
                        }
                        else if (WikiBlob.IsWebJob(asd, true))
                        {
                            DLog.TraceEvent(TraceEventType.Information, DTEId, $"Indexing {asd}");
                            s.AddDocumentToIndex(asd);
                        }
                        else if (WikiBlob.IsOrchestration(asd, true))
                        {
                            DLog.TraceEvent(TraceEventType.Information, DTEId, $"Indexing {asd}");
                            s.AddDocumentToIndex(asd);
                        }
                        else if (WikiBlob.IsPipeline(asd, true))
                        {
                            DLog.TraceEvent(TraceEventType.Information, DTEId, $"Indexing {asd}");
                            s.AddDocumentToIndex(asd);
                        }
                        else if (WikiBlob.IsRecievePort(asd, true))
                        {
                            DLog.TraceEvent(TraceEventType.Information, DTEId, $"Indexing {asd}");
                            s.AddDocumentToIndex(asd);
                        }
                        else if (WikiBlob.IsSQL(asd, true))
                        {
                            DLog.TraceEvent(TraceEventType.Information, DTEId, $"Indexing {asd}");
                            s.AddDocumentToIndex(asd);
                        }
                        else if (WikiBlob.IsSendPort(asd, true))
                        {
                            DLog.TraceEvent(TraceEventType.Information, DTEId, $"Indexing {asd}");
                            s.AddDocumentToIndex(asd);
                        }
                        else if (WikiBlob.IsManualPage(asd, true))
                        {
                            DLog.TraceEvent(TraceEventType.Information, DTEId, $"Indexing {asd}");
                            s.AddDocumentToIndex(asd);
                        }
                    }
                }
                catch (Exception e)
                {
                    s.Clear();
                    DLog.TraceEvent(TraceEventType.Error, DTEId, $"Exception when indexing: {e.ToString()}");
                }
            }
        }

        private static String GetAbsolutePath(String relativePath, String basePath)
        {
            if (relativePath == null)
                return null;
            if (basePath == null)
                basePath = Path.GetFullPath("."); // quick way of getting current working directory
            else
                basePath = GetAbsolutePath(basePath, null); // to be REALLY sure ;)
            // specific for windows paths starting on \ - they need the drive added to them.
            // I constructed this piece like this for possible Mono support.
            if (!Path.IsPathRooted(relativePath) || "\\".Equals(Path.GetPathRoot(relativePath)))
            {
                if (relativePath.StartsWith(Path.DirectorySeparatorChar.ToString()))
                    return Path.GetFullPath(Path.Combine(Path.GetPathRoot(basePath), relativePath.TrimStart(Path.DirectorySeparatorChar)));
                else
                    return Path.GetFullPath(Path.Combine(basePath, relativePath));
            }
            else
                return Path.GetFullPath(relativePath); // resolves any internal "..\" to get the true full path.
        }

    }

}
