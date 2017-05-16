using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Documenter.Helpers
{
    /// <summary>
    /// TODO: Implement!
    /// </summary>
    public class CSharpDoc
    {
        public CSharpDoc(String name, String solutionPath)
        {

            DirectoryInfo di = new DirectoryInfo(Path.GetDirectoryName(solutionPath));




        }

        public void BuildCTree(DirectoryInfo di)
        {

            foreach (var item in di.EnumerateDirectories())
            {

            }




        }
    }

    public enum CDocType
    {
        Project,
        Folder,
        File
    }

    public class Node
    {
        public Node()
        {
            
        }
        public int Type { get; set; }
        public string Path { get; set; }
        public string Name { get; set; }
        public List<Node> Children { get; set; }

        public void AddChild(Node child)
        {
            if (Children == null)
                Children = new List<Node>();
            Children.Add(child);
        }

    }


}
