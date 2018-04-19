using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace PhenoPad.FileService
{
    public class Class1
    {
        public string id { get; set; }
        public string name { get; set; }

        public Class1(string i, string n) {
            id = i;
            name = n;
        }

        public Class1()
        {
            id = "default";
            name = "default";
        }
    }
}
