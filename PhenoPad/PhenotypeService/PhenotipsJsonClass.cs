using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhenoPad.PhenotypeService
{
    public class Link
    {
        public string rel { get; set; }
        public string href { get; set; }
        public List<string> allowedMethods { get; set; }
    }

    public class Parent
    {
        public string name_translated { get; set; }
        public string name { get; set; }
        public string id { get; set; }
    }

    public class PhenotypeInfo
    {
        public double score { get; set; }
        public List<string> synonym { get; set; }
        public List<string> xref { get; set; }
        public string name_translated { get; set; }
        public string name { get; set; }
        public List<string> term_category { get; set; }
        public string comment { get; set; }
        public List<object> links { get; set; }
        public string id { get; set; }
        public List<string> is_a { get; set; }
        public List<Parent> parents { get; set; }
        public string name_es { get; set; }
        public List<string> associated_genes { get; set; }
        public string def { get; set; }
        public string def_translated { get; set; }
    }

    public class RootObject
    {
        public List<Link> links { get; set; }
        public List<PhenotypeInfo> rows { get; set; }
    }

    public class SuggestPhenotype
    {
        public string id { get; set; }
        public string name { get; set; }
        public double score { get; set; }
    }

    public class NCRPhenotype
    {
        public int end { get; set; }
        public string hp_id { get; set; }
        public List<string> names { get; set; }
        public string score { get; set; }
        public int start { get; set; }
    }
    public class NCRResult
    {
        public List<NCRPhenotype> matches { get; set; }
    }
}
