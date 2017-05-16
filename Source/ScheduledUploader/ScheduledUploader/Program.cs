using AzureStorage;
using System;
using System.IO;
using System.Linq;
using System.Text;
using Wiki.Models;
using Wiki.Utilities;

namespace ScheduledUploader
{
    class Program
    {

        static void Main(string[] args)
        {           
            try
            {
                WikiConfiguration conf = WikiConfiguration.GetConfiguration();
                EnvironmentInfo info = conf.GetEnvironment(System.Configuration.ConfigurationManager.AppSettings["BTEnvironment"]);
                BizTalk currentBTInst = conf.BizTalk.Where(x => x.Name == System.Configuration.ConfigurationManager.AppSettings["BTInstallation"]).FirstOrDefault();

                AzureBlobStorage azureStorage = azureStorage = StorageHelper.GetSecondaryStorage(info);
                StringBuilder log = new StringBuilder();
                try
                {
                    log.AppendLine("Starting upload of custom tables");
                    DBTableContentUploader uploader = new DBTableContentUploader();
                    uploader.StartDocumentingAllTables(azureStorage, log);
                    log.AppendLine("Upload of custom tables completed");

                    log.AppendLine("Starting upload of BTDocumenterFiles");
                    StartBtDocumentation(azureStorage, log, currentBTInst, info);
                    log.AppendLine("Upload of BTDocumenterFiles Done");
                }
                catch (Exception e)
                {
                    log.AppendLine("Error occured: " + e.ToString());
                }
                azureStorage.SetBlobContentAsString(WikiBlob.Combine("Log", "BTDocumenter", currentBTInst.Name, info.Name), log.ToString());
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(Path.GetTempPath(), DateTime.Now.ToString("yyyyMMdd_HHmmss_") + "BTDocumenter.log"), ex.ToString());
            }      
        }

        static void StartBtDocumentation(AzureBlobStorage azureStorage, StringBuilder log, BizTalk currentBTInst, EnvironmentInfo info)
        {
            BTDocumenterUploader bt = new BTDocumenterUploader();
            bt.StartDocumenterAndUploadToBlob(log, azureStorage, currentBTInst, info);
        }
    }
}
