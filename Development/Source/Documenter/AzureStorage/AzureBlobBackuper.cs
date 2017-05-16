using AzureStorage.Table.Models;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Wiki.Utilities;

namespace AzureStorage
{
    public class AzureBlobBackuper
    {
        public AzureBlobStorage OriginalBlob { get; set; }
        public AzureBlobStorage BackupBlob { get; set; }

        public AzureBlobBackuper(AzureBlobStorage fromBlob, AzureBlobStorage toBlob)
        {
            OriginalBlob = fromBlob;
            BackupBlob = toBlob;
        }

        public void Migrate(bool appendDate = true, bool skipOldVersions = true, bool skipAutoDocs = true)
        {
            DateTime d = DateTime.Now;
            int count = 0;
            foreach (String blob in OriginalBlob.GetBlobList())
            {
                String decodedName = null;
                try
                {
                    decodedName = HttpUtility.UrlDecode(blob);

                    if (skipOldVersions && WikiBlob.IsOldVersion(decodedName))
                        continue;
 
                    if (skipAutoDocs && !WikiBlob.IsManualPage(decodedName, false) && !WikiBlob.IsImage(decodedName, false) && !WikiBlob.IsFile(decodedName, false) && !WikiBlob.IsTFS(decodedName, false) && !WikiBlob.IsTemplate(decodedName, false))
                        continue;

                    CloudBlockBlob blobA = OriginalBlob.GetBlockBlob(decodedName);

                    if (appendDate)
                        decodedName += "_" + d.Year + PrefixString(d.Month.ToString(), "0", 2) + PrefixString(d.Day.ToString(), "0", 2) + "_" + PrefixString(d.Hour.ToString(), "0", 2) + PrefixString(d.Minute.ToString(), "0", 2);
                    CloudBlockBlob blobB = BackupBlob.GetBlockBlob(decodedName);


                    Console.WriteLine(count++ + " : " + decodedName);

                    using (Stream streamA = blobA.OpenRead())
                    using (CloudBlobStream streamB = blobB.OpenWrite())
                    {
                        streamA.CopyTo(streamB);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
            }
        }

        public int Backup(bool appendDate = true)
        {
            int blobsBackuped = 0;

            foreach (String blob in OriginalBlob.GetBlobList())
            {
                String decodedName = HttpUtility.UrlDecode(blob);
                if (decodedName.StartsWith("Manual£", StringComparison.OrdinalIgnoreCase) || decodedName.EndsWith("£Manual", StringComparison.OrdinalIgnoreCase))
                {
                    DateTime d = DateTime.Now;
                    string bckKey = decodedName;
                    if (appendDate)
                        bckKey += "_" + d.Year + PrefixString(d.Month.ToString(), "0", 2) + PrefixString(d.Day.ToString(), "0", 2) + "_" + PrefixString(d.Hour.ToString(), "0", 2) + PrefixString(d.Minute.ToString(), "0", 2);
                    Console.WriteLine(bckKey);
                    BackupBlob.SetBlobContentAsString(bckKey, OriginalBlob.GetBlobContentAsString(decodedName));
                    blobsBackuped++;
                }
                else if (decodedName.StartsWith("Image£", StringComparison.OrdinalIgnoreCase))
                {
                    DateTime d = DateTime.Now;
                    string bckKey = decodedName;
                    if (appendDate)
                        bckKey += "_" + d.Year + PrefixString(d.Month.ToString(), "0", 2) + PrefixString(d.Day.ToString(), "0", 2) + "_" + PrefixString(d.Hour.ToString(), "0", 2) + PrefixString(d.Minute.ToString(), "0", 2);
                    Console.WriteLine(bckKey);
                    BackupBlob.SetBlobContentAsByteArray(bckKey, OriginalBlob.GetBlobContentAsByteArray(decodedName));
                    blobsBackuped++;
                }
            }
            return blobsBackuped;
        }

        public static string PrefixString(String o, string prefix, int length)
        {
            while (o.Length < length)
            {
                o = prefix + o;
            }
            return o;
        }


        public static void FixCurrentVersions(AzureStorageInfo azure)
        {
            AzureBlobStorage azureStorage = new AzureBlobStorage(azure);
            List<AuditTrailModel> audits99 = azureStorage.GetAuditEntriesByVersion("tpdocaudit", AzureBlobStorage.TABLE_STORE_AUDIT_PARTITION_ID, "999").ToList();

            foreach (var item in audits99)
            {
                try
                { 
                    IEnumerable<AuditTrailModel> audits = azureStorage.GetAuditEntries("tpdocaudit", AzureBlobStorage.TABLE_STORE_AUDIT_PARTITION_ID, item.Blob);
                    List<AuditTrailModel> auditss = audits.OrderByDescending(x => x.Version).ToList();

                    AuditTrailModel audit = auditss.FirstOrDefault();
                    audit.Version = auditss.Count;

                    azureStorage.UpdateTableEntry("tpdocaudit", audit);
                    Console.WriteLine(audit.Blob);
                }
                catch(Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }

        public static void BootstrapAuditTrails(AzureStorageInfo azure)
        {
            AzureBlobStorage azureStorage = new AzureBlobStorage(azure);
            var blobs = azureStorage.GetBlobList();

            Regex r = new Regex(@"(Manual£.*)£Version(\d+)$");
            Regex r2 = new Regex(@"(.*£Manual)£Version(\d+)$");

            foreach (string item in blobs)
            {
                try
                {
                    string blobKey = HttpUtility.UrlDecode(item);

                    Match m = r.Match(blobKey);
                    if (!m.Success)
                        m = r2.Match(blobKey);

                    CloudBlockBlob b = null;
                    if (!blobKey.StartsWith("Image£") && (m.Success || blobKey.StartsWith("Manual£") || blobKey.EndsWith("£Manual")) && azureStorage.BlobExists(blobKey, out b))
                    {
                        if (m.Success)
                            blobKey = m.Groups[1].Value.ToString();

                        DateTime dt = b.Properties.LastModified.Value.DateTime;

                        AuditTrailModel data = new AuditTrailModel()
                        {
                            Blob = blobKey,
                            User = null,
                            RowKey = blobKey + "_" + dt.Year
                                    + dt.Month.ToString().Prefix("0", 2)
                                    + dt.Day.ToString().Prefix("0", 2)
                                    + dt.Hour.ToString().Prefix("0", 2)
                                    + dt.Minute.ToString().Prefix("0", 2)
                                    + dt.Second.ToString().Prefix("0", 2)
                                    + "_" + Guid.NewGuid().ToString().Substring(0, 8),
                            Time = dt,
                            Version = (m.Success) ? Convert.ToInt32(m.Groups[2].Value.ToString()) : 999,
                            LengthInBytes = b.Properties.Length,
                            UserHostAdr = null
                        };

                        azureStorage.AddTableEntry("tpdocaudit", data);
                        Console.WriteLine(blobKey + " V.: " + data.Version);
                    }
                }
                catch(Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }

        }

    }

    public static class ExtensionsCustom
    {
        public static string Prefix(this String me, string prefix, int length)
        {
            while (me.Length < length)
            {
                me = prefix + me;
            }
            return me;
        }
    }
}
