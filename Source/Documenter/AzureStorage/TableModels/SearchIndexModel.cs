using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace AzureStorage.Table.Models
{
    /// <summary>
    /// Table entity for storing search indexes
    /// </summary>
    public class SearchIndexModel : TableEntity
    {

        public SearchIndexModel()
        {
            this.PartitionKey = AzureBlobStorage.TABLE_STORE_INDEX_PARTITION_ID;
        }

        public string matches { get; set; }
    }
}