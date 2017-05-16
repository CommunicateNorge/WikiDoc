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
    public class AuditTrailModel : TableEntity
    {

        public AuditTrailModel()
        {
            this.PartitionKey = AzureBlobStorage.TABLE_STORE_AUDIT_PARTITION_ID;
        }

        public DateTime Time { get; set; }

        public string User { get; set; }

        public string Blob { get; set; }

        public string UserHostAdr { get; set; }

        public int Version { get; set; }

        public long LengthInBytes { get; set; }
    }
}