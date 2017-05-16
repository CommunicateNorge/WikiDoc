using AzureStorage;
using AzureStorage.Table.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Wiki.Utilities;

namespace Wiki.DAL
{
	public class DataAccess
	{
		private AzureBlobStorage azureStorage;
		private string AuditTableName;

		public DataAccess(AzureBlobStorage azureStorage, string AuditTableName)
		{
			this.azureStorage = azureStorage;
			this.AuditTableName = AuditTableName;
		}
		public void LogAuditTrail(string blobKey, long newBlobLength, int? newBlobversion, string user, string ip)
		{
			DateTime dt = DateTime.Now;

			AuditTrailModel data = new AuditTrailModel()
			{
				Blob = blobKey,
				User = string.IsNullOrEmpty(user) ? Environment.UserName : user, //User.Identity.Name,
				RowKey = blobKey + "_" + dt.Year
						+ dt.Month.ToString().PrefixString("0", 2)
						+ dt.Day.ToString().PrefixString("0", 2)
						+ dt.Hour.ToString().PrefixString("0", 2)
						+ dt.Minute.ToString().PrefixString("0", 2)
						+ dt.Second.ToString().PrefixString("0", 2),
				Time = dt,
				Version = newBlobversion ?? 1,
				LengthInBytes = newBlobLength,
				UserHostAdr = ip //Request.UserHostAddress
			};

			azureStorage.AddTableEntry(AuditTableName, data);
		}

		/// <summary>
		/// Adds or replaces a manually created page to the blob-store.
		/// </summary>
		/// <param name="pageName"></param>
		/// <param name="content"></param>
		public void AddManualPageToBlob(string pageName, string content, string user, string ip)
		{
			//Blob is created for the first time
			if (azureStorage.GetBlobETag(pageName) == null)
			{
				azureStorage.SetBlobContentAsString(pageName, content);
				LogAuditTrail(pageName, content.Length, 1, user, ip);
				return;
			}

			List<string> blobs = azureStorage.GetBlobList().ToList();
			Regex r = new Regex(pageName + @"(£Version(\d+))$");
			int highestVersion = 0;

			foreach (var item in blobs)
			{
				string blobName = HttpUtility.UrlDecode(item);
				Match m = r.Match(blobName);

				if (m.Success)
				{
					int currentVersion = Int32.Parse(m.Groups[2].ToString());
					if (currentVersion > highestVersion)
					{
						highestVersion = currentVersion;
					}
				}
			}

			//2nd time the blob is updated
			if (highestVersion == 0)
			{
				string oldContent = azureStorage.GetBlobContentAsString(pageName);
				azureStorage.SetBlobContentAsString(pageName + "£Version1", oldContent);
				azureStorage.SetBlobContentAsString(pageName, content);
			}

			//Blob is updated for the nth time
			if (highestVersion > 0)
			{
				//Add old content to versioning 
				string oldContent = azureStorage.GetBlobContentAsString(pageName);
				azureStorage.SetBlobContentAsString(pageName + "£Version" + (highestVersion + 1), oldContent);

				//Overwrite the new-blob to have the new content
				azureStorage.SetBlobContentAsString(pageName, content);
			}
			LogAuditTrail(pageName, content.Length, highestVersion + 2, user, ip);
		}
	}
}
