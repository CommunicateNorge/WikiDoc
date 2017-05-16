using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wiki.Utilities;

namespace Documenter
{
    public class BizTalkApplication
    {
        private string _key;
        public String Key
        {
            get
            {
                if(_key == null) 
                    _key = "Integration£" + WikiBlob.SanitizeV2(Name);
                return _key;
            }
        }
        public String Link(String className = null)
        {
            return WikiBlob.GetAsWikiLink(Key, false, Name, className);
        }

        public BizTalkApplication(String path, string name, DirectoryInfo orgPath = null)
        {
            this.Schemas = new List<string>();
            this.Maps = new List<string>();
            this.Orchestrations = new List<string>();
            this.Pipelines = new List<string>();
            this.SendPorts = new List<string>();
            this.RecievePorts = new List<string>();
            this.CSharpDocs = new List<string>();
            this.Transformations = new List<string>();
            this.Path = path;
            this.Name = name;
            this.OrgPath = orgPath;
        }
        public String Path { get; set; }
        public DirectoryInfo OrgPath { get; set; }
        public String Name { get; set; }
        public String Extra { get; set; }
        public List<String> CSharpDocs { get; set; }
        public List<String> Schemas { get; set; }
        public List<String> Maps { get; set; }
        public List<String> Orchestrations { get; set; }
        public List<String> Pipelines { get; set; }
        public List<String> SendPorts { get; set; }
        public List<String> RecievePorts { get; set; }
        public List<String> Transformations { get; set; }

    }
}
