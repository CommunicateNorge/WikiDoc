using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wiki.Models
{
    public class BlobUriAndModified : IComparable<BlobUriAndModified>
    {
        public DateTime Modified { get; set; }
        public string URI { get; set; }

        public int CompareTo(BlobUriAndModified obj)
        {
            return Modified.CompareTo(obj.Modified);
        }

    }
}
