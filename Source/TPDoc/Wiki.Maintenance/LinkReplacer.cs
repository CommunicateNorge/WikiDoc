using AzureStorage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Wiki.DAL;
using Wiki.Utilities;

namespace Wiki.Maintenance
{
	public class LinkReplacer
	{
		AzureBlobStorage azureStorage;
		private DataAccess dal;

		const string webPrefix = @"/Wiki/Page/";

		public LinkReplacer(AzureBlobStorage azureStorage, DataAccess dal)
		{
			this.azureStorage = azureStorage;
			this.dal = dal;
		}

		public bool ReplaceFunction(CancellationToken cancellationToken, CloudAppendBlob logBlob, CloudBlockBlob blob, string oldId, string oldIdName, string newId, string user, string ip)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				logBlob.AppendText("[" + DateTime.Now.ToString() + "]" + " Operation cancelled on " + blob.Name + Environment.NewLine);
			}

			if (!WikiBlob.IsManualPage(blob.Name, false))
				return true;

			blob.Properties.ContentType = "text/plain";

			string blobContent = blob.DownloadText();
			string escapedOldId = Regex.Escape(oldId);
			string escapedUrlEncodedOldId = Regex.Escape(HttpUtility.UrlEncode(oldId));
			string urlEncodedName = Regex.Escape(webPrefix + HttpUtility.UrlEncode(oldIdName));
			int replecedCount = 0;

			try
			{
				blobContent = Regex.Replace(blobContent, escapedOldId + "\"|" + escapedUrlEncodedOldId + "\"|" + urlEncodedName + "\"", delegate (Match match)
				{
					replecedCount++;
					return newId + @"""";
				});

				if (replecedCount > 0)
				{
					logBlob.AppendText("[" + DateTime.Now.ToString() + "]" + " Old id=" + oldId + " replaced in " + blob.Name + " " + replecedCount + " times." + Environment.NewLine);
					dal.AddManualPageToBlob(blob.Name, blobContent, user, ip);
				}

				return true;
			}
			catch (Exception e)
			{
				logBlob.AppendText("[" + DateTime.Now.ToString() + "]" + " Replacement failed. Exception message: " + Environment.NewLine + e.ToString() + Environment.NewLine);
				throw new Exception("Replacement failed. See log for more details.", e);
			}
		}

		public bool CallbackFunction(CloudAppendBlob logBlob, string doneLogBlobName)
		{
			CloudAppendBlob blob = azureStorage.Container.GetAppendBlobReference(WikiBlob.Combine("Log", "LinkReplacer"));
			logBlob.AppendText("[" + DateTime.Now.ToString() + "]" + " Replecement Done" + Environment.NewLine);
			try
			{
				doneLogBlobName = WikiBlob.Combine(blob.Name, DateTime.Now.ToString("yyyyMMdd_HHmmss"), "Done");
				azureStorage.SetBlobContentAsString(doneLogBlobName, blob.DownloadText());
				blob.Delete();
			}
			catch (Exception e)
			{
				return false;
			}

			return true;
		}

		public async Task<string> Replace(string oldId, string newId, CancellationToken cancellationToken, string user, string ip)
		{
			string oldIdName = "";
			string newIdName = "";
			string doneLogBlobName = ""; 
			List<string> replacedLog = new List<string>();
			CloudAppendBlob logBlob = azureStorage.Container.GetAppendBlobReference(WikiBlob.Combine("Log", "LinkReplacer"));

			logBlob.CreateOrReplace();
			
			if (!oldId.Contains(webPrefix))
			{
				oldIdName = oldId;
				oldId = webPrefix + oldId;
			}
			else
				oldIdName = oldId.Replace(@"/Wiki/Page/", "");	

			if (!newId.Contains(webPrefix))
			{
				newIdName = newId;
				newId = webPrefix + newId;
			}
			else
				newIdName = newId.Replace(@"/Wiki/Page/", "");	

			if (!newId.Contains(webPrefix))
				newId = webPrefix + newId;
			
			Func<CloudBlockBlob, bool> replaceFunc = (blob) => ReplaceFunction(cancellationToken, logBlob, blob, oldId, oldIdName, newId, user, ip);

			Func<bool> callback = () => CallbackFunction(logBlob, doneLogBlobName);

			bool replaceSuccessfull = false;

			try
			{
				replaceSuccessfull = await azureStorage.ItarateAllBlobs(replaceFunc, callback);
			}
			catch (Exception e)
			{
				CloudAppendBlob blob = azureStorage.Container.GetAppendBlobReference(WikiBlob.Combine("Log", "LinkReplacer"));
				logBlob.AppendText("[" + DateTime.Now.ToString() + "]" + " Replecement failed. Moving log to new file..." + Environment.NewLine);
				logBlob.AppendText("[" + DateTime.Now.ToString() + "]" + " Failure reason: " + e.ToString() + Environment.NewLine);

				doneLogBlobName = WikiBlob.Combine(blob.Name, DateTime.Now.ToString("yyyyMMdd_HHmmss"), "Done");
				azureStorage.SetBlobContentAsString(doneLogBlobName, blob.DownloadText());
				blob.Delete();
			}

			try
			{
				if (replaceSuccessfull)
				{
					azureStorage.RenameBlob(oldIdName, newIdName, false);
					azureStorage.SetBlobContentAsString(oldIdName, "<p>This page has been moved here: <a href=\"" + newId + "\">" + newId + "</a><p>");
				}
			}
			catch (Exception ioe)
			{
				CloudAppendBlob doneLogBlob = azureStorage.Container.GetAppendBlobReference(doneLogBlobName);
				doneLogBlob.AppendText("[" + DateTime.Now.ToString() + "]" + " Renaming and replaceing blob failed with msg: "  + ioe.ToString());
				return "failure";
			}

			return "Done";
		}


	}
}
