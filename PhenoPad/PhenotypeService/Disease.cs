using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhenoPad.PhenotypeService
{
    public class Disease
    {
        public string id { get; set; }
        private string _name;
        public string name {
            get { return _name; }
            set {
                _name = value.Substring(0,1).ToUpper() + value.Substring(1).ToLower();
            }
        }
        public string url { get; set; }
        public double score { get; set; }
        
    }
}
