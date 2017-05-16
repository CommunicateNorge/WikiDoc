using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SearchIndexer.v2
{
    public interface IStorage
    {
        string GetContentAsString(string groupingEntityName, string blobKey);

        T Retrive<T>(string groupingEntityName, string key, string keyName);

        T[] RetriveBatch<T>(string groupingEntityName, IEnumerable<string> keys, string keyName = null);

        bool Upsert<T>(string groupingEntityName, IEnumerable<T> entityToUpdateOrInsert);
    }
}
