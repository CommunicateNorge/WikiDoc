using AzureStorage.Table.Models;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wiki.Models;
using Wiki.Utilities;

namespace AzureStorage
{

    /// <summary>
    /// Generic class used to retrieve, save and enumerate files in an azure storage blob.
    /// </summary>
    public class AzureBlobStorage
    {
        public CloudBlobClient Client { get; private set; }
        public CloudBlobContainer Container { get; private set; }
        public CloudStorageAccount StorageAccount { get; private set; }
        public String DefaultMimeType { get; set; }
        public CloudTableClient TableClient { get; private set; }
        public CloudTable Table { get; private set; }

        public const string TABLE_STORE_INDEX_PARTITION_ID = "index";
        public const string TABLE_STORE_AUDIT_PARTITION_ID = "audit";

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureBlobStorage"/> class.
        /// </summary>
        /// <param name="azureStorageConnInfo">The azure storage connection information, defined in the <see cref="AzureStorageInfo"/> class</param>
        /// <param name="createIfNotExists">if set to <c>true</c> creates the azure container if it doesn't exist.</param>
        /// <exception cref="System.ArgumentNullException">azureStorageConnInfo;The referenced container does not exist and the \createIfNotExists\-flag was false.</exception>
        public AzureBlobStorage(AzureStorageInfo azureStorageConnInfo, bool createIfNotExists = true, string container = null)
        {
            if (container != null)
                azureStorageConnInfo.Container = container;
            //Azure connection info stored in a connection string
            if (azureStorageConnInfo.Type == AzureStorageInfo.KeyNameType.FromConnectionString)
            {
                StorageAccount = CloudStorageAccount.Parse(azureStorageConnInfo.KeyNameConnectionString);
            }
            //Azure connection info stored in code as a key/value pair.
            else if (azureStorageConnInfo.Type == AzureStorageInfo.KeyNameType.FromKeyNamePair)
            {
                StorageCredentials creds = new StorageCredentials(azureStorageConnInfo.Name, azureStorageConnInfo.Key);
                StorageAccount = new CloudStorageAccount(creds, useHttps: true);
            }
            GetContainerReference(azureStorageConnInfo, createIfNotExists);
        }
      

        /// <summary>
        /// Gets the container reference.
        /// </summary>
        private void GetContainerReference(AzureStorageInfo azureStorageConnInfo, bool createIfNotExists)
        {
            Client = StorageAccount.CreateCloudBlobClient();
            Container = Client.GetContainerReference(azureStorageConnInfo.Container);

            if (createIfNotExists) Container.CreateIfNotExists();
            else
            {
                if (!Container.Exists())
                    throw new ArgumentNullException("azureStorageConnInfo", "The referenced container does not exist and the \"createIfNotExists\"-flag was false.");
            }

            BlobContainerPermissions permissions = new BlobContainerPermissions();
            permissions.PublicAccess = azureStorageConnInfo.Permission;
            Container.SetPermissions(permissions);
        }

        public void InitializeTable(String tableName, bool createIfNotExists = true)
        {
            if (Table != null && Table.Name == tableName)
                return;

            // Create the table client.            
            TableClient = StorageAccount.CreateCloudTableClient();

            // Create the table if it doesn't exist.
            Table = TableClient.GetTableReference(tableName);
            if(createIfNotExists) Table.CreateIfNotExists();
        }

        /// <summary>
        /// Returns a list of files in an Azure Storage Container
        /// </summary>
        /// <param name="max">The maximum number of files/blobs to return</param>
        public IEnumerable<String> GetBlobList(String prefix = null, int max = Int32.MaxValue)
        {
            IEnumerable<IListBlobItem> blobs = Container.ListBlobs(prefix);
            List<String> blobUris = new List<string>();

            foreach (var item in blobs.Take(max))
            {
                blobUris.Add(item.Uri.Segments.Last());
            }
            return blobUris;
        }

		public async Task<bool> ItarateAllBlobs(Func<CloudBlockBlob, bool> function, Func<bool> callback)
		{
			Stopwatch sw = new Stopwatch();
			sw.Start();
			int? maxResults = null;
			BlobRequestOptions options = new BlobRequestOptions() { DisableContentMD5Validation = true, ParallelOperationThreadCount = 30 };
			BlobContinuationToken continuationToken = new BlobContinuationToken();
			do
			{
				BlobResultSegment resultSegment = await Container.ListBlobsSegmentedAsync(null, true, BlobListingDetails.None, maxResults, continuationToken, options, null);
				continuationToken = resultSegment.ContinuationToken;
				IEnumerable<CloudBlockBlob> blobSegment = resultSegment.Results.OfType<CloudBlockBlob>();

				foreach (var item in blobSegment)
				{
					function(item);
				}			
			}
			while (continuationToken != null);
			sw.Stop();
			callback();
			return true;
		}

		public bool DeleteBlob(string key)
		{
			CloudBlockBlob blob = Container.GetBlockBlobReference(key);

			if (blob.Exists())
			{
				blob.Delete();
				return true;
			}

			return false;
		}

		public string RenameBlob(string oldKey, string newKey, bool deleteOld = false)
		{
			CloudBlockBlob oldBlob = Container.GetBlockBlobReference(oldKey);
			CloudBlockBlob newBlob = Container.GetBlockBlobReference(newKey);

			if (newBlob.Exists())
			{
				throw new InvalidOperationException("New blob already exists");
			}
			else if(!oldBlob.Exists())
			{
				throw new InvalidOperationException("Old blob does not exist exists");
			}
			else
			{
				newBlob.UploadText(oldBlob.DownloadText());

				if (deleteOld)
					oldBlob.Delete();
			}
			return newBlob.Name;
		}

		/// <summary>
		/// Returns a list of files in an Azure Storage Container
		/// </summary>
		/// <param name="max">The maximum number of files/blobs to return</param>
		public IEnumerable<IListBlobItem> GetBlobListWithProperties(int max = Int32.MaxValue)
        {
            return Container.ListBlobs();
        }

        public string GetBlobETag(string key)
        {
            CloudBlockBlob blob = Container.GetBlockBlobReference(key);
            if(blob.Exists())
            {
                return blob.Properties.ETag;
            }
            return null;
        }

        public bool BlobExists(string key, out CloudBlockBlob blob)
        {
            blob = Container.GetBlockBlobReference(key);
            if (blob.Exists())
            {
                return true;
            }
            //blob = null;
            return false;
        }

		public bool BlobExists(string key)
		{
			CloudBlockBlob blob = null;

			return BlobExists(key, out blob);
		}

		public DateTime? GetBlobLastModified(string key)
        {
            CloudBlockBlob blob = Container.GetBlockBlobReference(key);
            if (blob.Exists())
            {
                return blob.Properties.LastModified.Value.LocalDateTime;
            }
            return null;
        }

        public delegate bool BlobFilter(string blobName);

        public List<BlobUriAndModified> GetTopNLatestBlobs(int n, BlobFilter filter, string prefixFilter = null)
        {
            var blobs = Container.ListBlobs(prefixFilter, false, BlobListingDetails.Metadata, null, null);

            C5.IntervalHeap<BlobUriAndModified> newestBlobs = new C5.IntervalHeap<BlobUriAndModified>();

            int count = 0;

            foreach (var item in blobs)
            {              
                if (filter.Invoke(item.Uri.Segments.Last()))
                {
                    CloudBlob t = item as CloudBlob;
                    DateTime currentBlobTime;
                    bool success = DateTime.TryParse(t.Properties.LastModified.ToString(), out currentBlobTime);

                    if (success)
                    {
                        if (count++ < n)
                        {
                            newestBlobs.Add(new BlobUriAndModified() { Modified = currentBlobTime, URI = t.Uri.Segments.Last() });
                        }
                        else if (currentBlobTime > newestBlobs.FindMin().Modified)
                        {
                            newestBlobs.DeleteMin();
                            newestBlobs.Add(new BlobUriAndModified() { Modified = currentBlobTime, URI = t.Uri.Segments.Last() });
                        }
                    }
                }
            }

            return newestBlobs.ToList();
        }

        public string SetBlobContentFromStream(string key, Stream inputStream)
        {
            CloudBlockBlob blob = Container.GetBlockBlobReference(key);
            blob.UploadFromStream(inputStream);
            return blob.Uri.ToString();
        }

        public async Task<string> SetBlobContentFromStreamAsync(string key, Stream inputStream)
        {
            CloudBlockBlob blob = Container.GetBlockBlobReference(key);
            await blob.UploadFromStreamAsync(inputStream);
            return blob.Uri.ToString();
        }

        public string SetBlobContentAsByteArray(string key, byte[] fileData)
        {
            CloudBlockBlob blob = Container.GetBlockBlobReference(key);
            blob.UploadFromByteArray(fileData, 0, fileData.Length);
            return blob.Uri.ToString();
        }

        public async Task<string> SetBlobContentAsByteArrayAsync(string key, byte[] fileData)
        {
            CloudBlockBlob blob = Container.GetBlockBlobReference(key);
            await blob.UploadFromByteArrayAsync(fileData, 0, fileData.Length);
            return blob.Uri.ToString();
        }

        public byte[] GetBlobContentAsByteArray(String key)
        {
            CloudBlockBlob blob = Container.GetBlockBlobReference(key);

            using (var memoryStream = new MemoryStream())
            {
                blob.DownloadToStream(memoryStream);
                return memoryStream.ToArray();
            }
        }

        public async Task<byte[]> GetBlobContentAsByteArrayAsync(String key)
        {
            CloudBlockBlob blob = Container.GetBlockBlobReference(key);

            using (var memoryStream = new MemoryStream())
            {
                await blob.DownloadToStreamAsync(memoryStream);
                return memoryStream.ToArray();
            }
        }

        public Stream GetBlobContentAsStream(String key)
        {
            CloudBlockBlob blob = Container.GetBlockBlobReference(key);

            using (var memoryStream = new MemoryStream())
            {
                blob.DownloadToStream(memoryStream);
                return memoryStream;
            }
        }

        public async Task<Stream> GetBlobContentAsStreamAsync(String key)
        {
            CloudBlockBlob blob = Container.GetBlockBlobReference(key);

            using (var memoryStream = new MemoryStream())
            {
                await blob.DownloadToStreamAsync(memoryStream);
                return memoryStream;
            }
        }

        public bool SetBlobContentIfNotExists(String key, FileInfo file)
        {
            CloudBlockBlob blob = null;
            if(!BlobExists(key, out blob))
            {
                blob.UploadFromFile(file.FullName, FileMode.Open);
                return true;
            }
            return false;
        }

        public string SetBlobContentToFile(String key, FileInfo file, String mimeType = null)
        {
            CloudBlockBlob blob = Container.GetBlockBlobReference(key);

            if (mimeType != null)
                blob.Properties.ContentType = mimeType;

            blob.UploadFromFile(file.FullName, FileMode.Open);

            return blob.Uri.ToString();
        }

        public async Task<string> SetBlobContentToFileAsync(String key, FileInfo file)
        {
            CloudBlockBlob blob = Container.GetBlockBlobReference(key);
            await blob.UploadFromFileAsync(file.FullName, FileMode.Open);
            return blob.Uri.ToString();
        }

        /// <summary>
        /// Sets the content of a file/BLOB as a string.
        /// </summary>
        /// <param name="fileName">Name of the file/blob</param>
        /// <param name="content">The content</param>
        public string SetBlobContentAsString(String key, String content)
        {
            CloudBlockBlob blob = Container.GetBlockBlobReference(key);
            if (DefaultMimeType != null)
                blob.Properties.ContentType = DefaultMimeType;

            blob.UploadText(content);
            return blob.Uri.ToString();
        }

		public async Task<string> SetBlobContentAsStringAsync(String key, String content)
        {
            CloudBlockBlob blob = Container.GetBlockBlobReference(key);
            if (DefaultMimeType != null)
                blob.Properties.ContentType = DefaultMimeType;

            await blob.UploadTextAsync(content);
            return blob.Uri.ToString();
        }

        /// <summary>
        /// Gets the BLOB/file content as a string.
        /// </summary>
        /// <param name="fileName">Name of the file/blob</param>
        /// <param name="mimeType">MIME-type [optional]</param>
        public String GetBlobContentAsString(String key, string mimeType = "text/plain")
        {
            CloudBlockBlob blob = Container.GetBlockBlobReference(key);
            blob.Properties.ContentType = mimeType;
            return blob.Exists() ? blob.DownloadText() : null;
        }

        public async Task<String> GetBlobContentAsStringAsync(String key, string mimeType = "text/plain")
        {
            CloudBlockBlob blob = Container.GetBlockBlobReference(key);
            blob.Properties.ContentType = mimeType;
            return await blob.DownloadTextAsync();
        }

        public bool GetBlobContentToFile(String key, String path)
        {
            CloudBlockBlob blob = Container.GetBlockBlobReference(key);
            if (blob.Exists())
            {
                blob.DownloadToFile(path, FileMode.Create);
                return true;
            }
            else
                return false;
        }

        public async Task<bool> GetBlobContentToFileAsync(String key, String path)
        {
            CloudBlockBlob blob = Container.GetBlockBlobReference(key);
            if (blob.Exists())
            {
                await blob.DownloadToFileAsync(path, FileMode.Create);
                return true;
            }
            else
                return false;
        }

        internal CloudBlockBlob GetBlockBlob(string key)
        {
            return Container.GetBlockBlobReference(key);
        }


        public TableResult AddTableEntry(string tableName, TableEntity data)
        {
            if (Table == null || Table.Name != tableName)
                InitializeTable(tableName);

            TableOperation newRow = TableOperation.Insert(data);
            return Table.Execute(newRow);
        }

        public TableResult UpdateTableEntry(string tableName, TableEntity data)
        {
            InitializeTable(tableName);
            TableOperation newRow = TableOperation.InsertOrReplace(data);
            return Table.Execute(newRow);
        }

        //public T GetTableEntry<T>(string tableName, string partitionKey, string rowKey) where T : ITableEntity
        //{
        //    InitializeTable(tableName);

        //    TableOperation retrieveOperation = TableOperation.Retrieve<T>(partitionKey, rowKey);
        //    return (T)(Table.Execute(retrieveOperation)).Result;
        //}

        #region ImplementationSpecific

        public IEnumerable<AuditTrailModel> GetAuditEntries(string tableName, string partitionKey, string blobName)
        {
            InitializeTable(tableName);

            TableQuery<AuditTrailModel> auditQuery = new TableQuery<AuditTrailModel>().Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, partitionKey));
            auditQuery.AndWhere<AuditTrailModel>(TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.GreaterThanOrEqual, blobName));
            auditQuery.AndWhere<AuditTrailModel>(TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.LessThan, blobName + "_99999999999999"));

            return Table.ExecuteQuery<AuditTrailModel>(auditQuery);
        }

        public IEnumerable<AuditTrailModel> GetAuditEntriesByVersion(string tableName, string partitionKey, string version)
        {
            InitializeTable(tableName);

            return (from entity in Table.CreateQuery<AuditTrailModel>()
                         where entity.Version == Convert.ToInt32(version)
                         select entity);
        }

        public IEnumerable<AuditTrailModel> GetAuditEntriesByUser(string tableName, string user)
        {
            InitializeTable(tableName);

            return (from entity in Table.CreateQuery<AuditTrailModel>()
                    where entity.User == user
                    select entity);
        }
        #endregion
    }

    /// <summary>
    /// Class that stores the different connection details used when connecting to an Azure storage container and/or blob.
    /// </summary>
    public class AzureStorageInfo
    {
        internal enum KeyNameType
        {
            FromConnectionString,
            FromKeyNamePair
        }

        private AzureStorageInfo(string name, string key, string containerName, BlobContainerPublicAccessType permission)
        {
            Name = name;
            Key = key;
            Container = containerName;
            Type = KeyNameType.FromKeyNamePair;
            Permission = permission;
        }

        private AzureStorageInfo(string connectionString, string containerName, BlobContainerPublicAccessType permission)
        {
            KeyNameConnectionString = connectionString;
            Container = containerName;
            Type = KeyNameType.FromConnectionString;
            Permission = permission;
        }

        internal KeyNameType Type { get; private set; }
        internal string Name { get; private set; }
        internal string Key { get; private set; }
        internal string KeyNameConnectionString { get; private set; }
        internal string Container { get; set; }
        internal BlobContainerPublicAccessType Permission { get; private set; }

        //To add another azure connection, duplicate this property with your own connection string.

        public static AzureStorageInfo GenericConnection(string connectionString, string container)
        {
            return new AzureStorageInfo(connectionString, container, BlobContainerPublicAccessType.Off);
        }

        public static AzureStorageInfo WikiFilesPrimary
        {
            get
            {
                return new AzureStorageInfo(ConfigurationManager.ConnectionStrings["PrimaryAzureStorage"].ConnectionString,
                                            ConfigurationManager.AppSettings["PrimaryAzureStorageContainerName"],
                                            BlobContainerPublicAccessType.Off);
            }
        }

        public static AzureStorageInfo WikiFilesSecondary
        {
            get
            {
                return new AzureStorageInfo(ConfigurationManager.ConnectionStrings["SecondaryAzureStorage"].ConnectionString,
                                            ConfigurationManager.AppSettings["PrimaryAzureStorageContainerName"],
                                            BlobContainerPublicAccessType.Off);
            }
        }

        public static AzureStorageInfo WikiFilesBackup
        {
            get
            {
                return new AzureStorageInfo(ConfigurationManager.ConnectionStrings["SecondaryAzureStorage"].ConnectionString,
                                            "docublobbackup",
                                            BlobContainerPublicAccessType.Off);
            }
        }

        //Example usage: storing connection info directly in code, instead of in web.config.
        //public static AzureStorageInfo PublicFilesKeyVersion 
        //{ 
        //    get 
        //    {
        //        return new AzureStorageInfo("picturestorage", "e3ioFRe70q0jyfr7jxVAwzIT1edYpDXxQ8pI8wywklL2jufj2PuRvnRtyKWPSTdlU3KnABIhN4ycHNtlNpJnGw==", "publicfiles"); 
        //    }
        //}

    }

    public static class TableQueryExtensions
    {
        public static TableQuery<TElement> AndWhere<TElement>(this TableQuery<TElement> @this, string filter)
        {
            @this.FilterString = TableQuery.CombineFilters(@this.FilterString, TableOperators.And, filter);
            return @this;
        }

        public static TableQuery<TElement> OrWhere<TElement>(this TableQuery<TElement> @this, string filter)
        {
            @this.FilterString = TableQuery.CombineFilters(@this.FilterString, TableOperators.Or, filter);
            return @this;
        }

        public static TableQuery<TElement> NotWhere<TElement>(this TableQuery<TElement> @this, string filter)
        {
            @this.FilterString = TableQuery.CombineFilters(@this.FilterString, TableOperators.Not, filter);
            return @this;
        }
    }
}