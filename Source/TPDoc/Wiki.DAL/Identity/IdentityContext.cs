using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wiki.Models.Identity;

namespace Wiki.DAL.Identity
{
    class IdentityContext : DbContext
    {
        public IdentityContext(): base("name=IdentityDBConnectionString")
        {
            Database.SetInitializer<IdentityContext>(new CreateDatabaseIfNotExists<IdentityContext>());
        }

        public DbSet<Role> Roles { get; set; }
        public DbSet<Prefix> Prefixes { get; set; }
        public DbSet<User> Users { get; set; }
    }
}
