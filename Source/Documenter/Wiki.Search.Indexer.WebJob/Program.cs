using AzureStorage;
using SearchIndexer.v2;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wiki.Models;

namespace Wiki.Indexer.WebJob
{
    public class Program
    {
        public static Search Indexer { get; set; }

        static void Main(string[] args)
        {
            WikiConfiguration config = WikiConfiguration.GetConfiguration(true);
            AzureBlobStorage storage = StorageHelper.GetPrimaryStorage();
            StorageAzureTable table = new StorageAzureTable(storage, config.PrimaryAzureStorageTableIndex);
            Indexer = new Search(table, storage, config.PrimaryAzureStorageTableIndex);


            IndexPage("Integration£Global€Common", "Integration£Global€Common");

        }

        public static void IndexPage(string oldVersion, string currentPage)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            Indexer.RemoveDocumentFromIndex(oldVersion);
            sw.Stop();
            Console.WriteLine(sw.Elapsed);

            sw.Restart();
            Indexer.AddDocumentToIndex(currentPage);
            sw.Stop();
            Console.WriteLine(sw.Elapsed);

            Indexer.Clear();
        }

    }
}