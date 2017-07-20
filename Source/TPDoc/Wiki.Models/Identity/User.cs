using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wiki.Models.Identity
{
    public class User
    {
        public int UserId { get; set; }
        public string UserName { get; set; }

        public ICollection<Role> Roles { get; set; }
    }
}
