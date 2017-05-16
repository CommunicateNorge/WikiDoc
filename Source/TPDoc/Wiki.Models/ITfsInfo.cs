using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wiki.Models
{
    public interface ITfsInfo
    {
        string User { get; set; }
        string Domain { get; set; }
        string Pwd { get; set; }
        string TfsUri { get; set; }
        string Name { get; set; }
    }
}
