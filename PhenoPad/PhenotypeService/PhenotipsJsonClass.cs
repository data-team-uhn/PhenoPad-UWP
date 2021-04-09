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
        public List<string> allowedMethods { get; set; }
        public string href { get; set; }
    }

    public class Parent
    {
        public string name_translated { get; set; }
        public string name { get; set; }
        public string id { get; set; }
    }

    public class Row
    {
        public string name_es { get; set; }
        public string name_fr { get; set; }
        public List<string> xref { get; set; }
        public string def { get; set; }
        public string name_translated { get; set; }
        public string def_translated { get; set; }
        public List<string> term_category { get; set; }
        public List<string> synonym_fr { get; set; }
        public double score { get; set; }
        public List<string> synonym { get; set; }
        public List<string> synonym_it { get; set; }
        public string name { get; set; }
        public List<string> synonym_de { get; set; }
        public string comment { get; set; }
        public List<object> links { get; set; }
        public string id { get; set; }
        public List<string> is_a { get; set; }
        public string name_it { get; set; }
        public List<string> associated_genes { get; set; }
        public List<Parent> parents { get; set; }
        public string name_de { get; set; }
        public List<string> alt_id { get; set; }
        public List<string> synonym_es { get; set; }
    }

    public class RootObject
    {
        public List<Link> links { get; set; }
        public List<Row> rows { get; set; }
    }

    public class SuggestPhenotype
    {
        public string id { get; set; }
        public string name { get; set; }
        public double score { get; set; }
    }

    public class NCRPhenotype : IEquatable<NCRPhenotype>
    {
        public int end { get; set; }
        public string hp_id { get; set; }
        public List<string> names { get; set; }
        public string score { get; set; }
        public int start { get; set; }
        

        public bool Equals(NCRPhenotype other)
        {
            return other.hp_id == hp_id &&
                other.start == start
                && other.end == end;
        }
        public override bool Equals(object obj)
        {
            var phenotype = obj as NCRPhenotype;
            return phenotype != null &&
                   hp_id.Equals(phenotype.hp_id);
        }
        public static bool operator ==(NCRPhenotype phenotype1, NCRPhenotype phenotype2)
        {
            return EqualityComparer<NCRPhenotype>.Default.Equals(phenotype1, phenotype2);
        }

        public static bool operator !=(NCRPhenotype phenotype1, NCRPhenotype phenotype2)
        {
            return !(phenotype1 == phenotype2);
        }

        public override int GetHashCode()
        {
            return -1032463776 + EqualityComparer<string>.Default.GetHashCode(hp_id);
        }
    }
    public class NCRResult
    {
        public List<NCRPhenotype> matches { get; set; }
    }
}
