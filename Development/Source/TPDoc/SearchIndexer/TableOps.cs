using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage.Table.Protocol;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SearchIndexer
{
    public static class TableOps
    {

        public static bool SafeCreateIfNotExists(this CloudTable table, double seconds, TableRequestOptions requestOptions = null, OperationContext operationContext = null)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            do
            {
                Console.WriteLine("Trying...");
                try
                {
                    return table.CreateIfNotExists(requestOptions, operationContext);
                }
                catch (StorageException e)
                {
                    if ((e.RequestInformation.HttpStatusCode == 409) && (e.RequestInformation.ExtendedErrorInformation.ErrorCode.Equals(TableErrorCodeStrings.TableBeingDeleted)))
                        Thread.Sleep(3000);// The table is currently being deleted. Try again until it works.
                    else
                        throw;
                }
            } while (sw.Elapsed.TotalSeconds < seconds);
            return false;
        }
    }
}
