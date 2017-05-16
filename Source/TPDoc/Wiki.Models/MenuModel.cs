using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wiki.Models
{
	public class MenuItem
	{
		public string Name { get; set; }
		public string Link { get; set; }
		public List<MenuItem> Children { get; set; }
	}

	public class MenuModel
	{
		public List<MenuItem> items { get; set; }
	}

}
