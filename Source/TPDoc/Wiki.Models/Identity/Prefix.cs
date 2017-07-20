using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wiki.Models.Identity
{
    public class Prefix
    {
        public int PrefixId { get; set; }
        public string PrefixName { get; set; }

        public ICollection<Role> Roles { get; set; }
    }
}
