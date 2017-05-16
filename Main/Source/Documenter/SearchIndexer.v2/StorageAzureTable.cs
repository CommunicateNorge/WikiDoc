using AzureStorage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SearchIndexer.v2
{
    public class StorageAzureTable : IStorage
    {
        public AzureBlobStorage Storage { get; set; }
        public CloudTable Table { get; set; }

        private int batchSize = MaxBatchSize;
        public int BatchSize 
        {
            get { return batchSize; }
            set 
            {
                if (value > MaxBatchSize)
                    batchSize = MaxBatchSize;
                else if (value < 1)
                    batchSize = 1;
                else
                    batchSize = value;
            }
        }
        private const int MaxBatchSize = 99;

        public StorageAzureTable(AzureBlobStorage azureStorage, String tableName)
        {
            Storage = azureStorage;
            CloudTableClient tableClient = Storage.StorageAccount.CreateCloudTableClient();
            Table = tableClient.GetTableReference(tableName);
            Table.CreateIfNotExists();
        }

        public string GetContentAsString(string groupingEntityName, string blobKey)
        {
            throw new NotImplementedException();
        }


        public T Retrive<T>(string groupingEntityName, string key, string keyName = null)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<AzureStorage.Table.Models.SearchIndexModel> GetSearchEntries(string partitionKey, IEnumerable<string> keys, bool startsWith = false)
        {
            TableQuery<AzureStorage.Table.Models.SearchIndexModel> auditQuery = new TableQuery<AzureStorage.Table.Models.SearchIndexModel>().Where(TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, keys.First()));
            foreach (var key in keys)
            {
                if(startsWith)
                {
                    var prefixCondition = TableQuery.CombineFilters(
                    TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.GreaterThanOrEqual, key),
                    TableOperators.And,
                    TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.LessThan, key + "åå")
                    );
                    auditQuery.OrWhere<AzureStorage.Table.Models.SearchIndexModel>(prefixCondition); 
                }
                else
                    auditQuery.OrWhere<AzureStorage.Table.Models.SearchIndexModel>(TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, key));   
            }
            return Table.ExecuteQuery<AzureStorage.Table.Models.SearchIndexModel>(auditQuery);
        }

        public AzureStorage.Table.Models.SearchIndexModel[] RetriveSearch(string groupingEntityName, IEnumerable<string> keys, string keyName = null)
        {
            List<AzureStorage.Table.Models.SearchIndexModel> res = new List<AzureStorage.Table.Models.SearchIndexModel>();
            int count = keys.Count();
            int processed = 0;
            while (processed < count)
            {
                IEnumerable<AzureStorage.Table.Models.SearchIndexModel> results = GetSearchEntries(groupingEntityName, keys.Skip(processed).Take(Math.Min(BatchSize, count - processed)), true);
                res.AddRange(results);
                processed += BatchSize;
            }
            return res.ToArray();
        }

        public T[] RetriveBatch<T>(string groupingEntityName, IEnumerable<string> keys, string keyName = null)
        {
            throw new NotImplementedException();
        }

        public bool Upsert<T>(string groupingEntityName, IEnumerable<T> entitiesToUpdateOrInsert)
        {
            if (entitiesToUpdateOrInsert.Count() == 0)
                return true;

            if (!(entitiesToUpdateOrInsert.First() is TableEntity))
                throw new ArgumentException("Entities upserted to Azure Table Storage must extend the TableEntity class.");

            List<Task> tasks = new List<Task>();
            int count = entitiesToUpdateOrInsert.Count();
            int processed = 0;
            while(processed < count)
            { 
                Task t = UpsertValues<T>(groupingEntityName, entitiesToUpdateOrInsert.Skip(processed).Take(Math.Min(BatchSize, count - processed)));
                tasks.Add(t);
                processed += BatchSize;
            }
            Task.WaitAll(tasks.ToArray());
            return true;
        }

        private async Task<bool> UpsertValues<T>(string groupingEntityName, IEnumerable<T> entitiesToUpdateOrInsert)
        {
            TableBatchOperation tbo = new TableBatchOperation();
            foreach (var item in entitiesToUpdateOrInsert)
            {
                tbo.Add(TableOperation.InsertOrReplace(item as TableEntity));
            }
            TableRequestOptions aasd = new TableRequestOptions();
            await Table.ExecuteBatchAsync(tbo);
            return true;
        }
    }
}
