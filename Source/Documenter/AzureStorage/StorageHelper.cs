using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wiki.Models;

namespace AzureStorage
{
    public class StorageHelper
    {
        private static Dictionary<String, AzureBlobStorage> storage = new Dictionary<string, AzureBlobStorage>();

        public static AzureBlobStorage GetPrimaryStorage(EnvironmentInfo envInfo = null)
        {
            WikiConfiguration config = WikiConfiguration.GetConfiguration();
            String container = (envInfo == null) ? config.GetPrimaryEnvironment().Container : envInfo.Container;
            String connectionString = config.PrimaryAzureStorage;

            if (storage.ContainsKey(container + connectionString))
                return storage[container + connectionString];

            AzureStorageInfo asi = AzureStorageInfo.GenericConnection(connectionString, container);
            AzureBlobStorage abs = new AzureBlobStorage(asi);
            storage.Add(container + connectionString, abs);
            return abs;
        }

        public static AzureBlobStorage GetSecondaryStorage(EnvironmentInfo envInfo = null)
        {
            WikiConfiguration config = WikiConfiguration.GetConfiguration();
            String container = (envInfo == null) ? config.GetPrimaryEnvironment().Container : envInfo.Container;
            String connectionString = config.SecondaryAzureStorage;

            if (storage.ContainsKey(container + connectionString))
                return storage[container + connectionString];

            AzureStorageInfo asi = AzureStorageInfo.GenericConnection(connectionString, container);
            AzureBlobStorage abs = new AzureBlobStorage(asi);
            storage.Add(container + connectionString, abs);
            return abs;
        }
    }
}
