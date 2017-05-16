using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wiki.Models;

namespace Documenter
{
    public interface IDocumenter
    {
        String Key { get; }
        String Name { get; }
        EnvironmentInfo EnvInfo { get; set; }

        String Link(string className);
        TimeSpan Start();
    }
}
