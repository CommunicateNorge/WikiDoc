using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wiki.Models.Identity
{
    public class Role
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; }

        public ICollection<Prefix> Prefixes { get; set; }

        public ICollection<User> Users { get; set; }
    }
}
