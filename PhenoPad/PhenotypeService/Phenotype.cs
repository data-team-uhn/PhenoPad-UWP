using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhenoPad.PhenotypeService
{
    public class Phenotype : IEquatable<Phenotype>, INotifyPropertyChanged
    {
        public string hpId { get; set; }
        public string name { get; set; }
        public List<string> alternatives { get; set; }
        public int state { get; set; } // NA: -1, Y: 1, N: 0
        public SourceType sourceType { get; set; }
        /**
        private int _state;
        public int state
        {
            get { return _state; }
            set
            {
                _state = value;
                RaisePropertyChanged("state");
            }
        }**/
    


        public Phenotype()
        {
        }

        public Phenotype(string hpid, string name, List<String> alter, int state)
        {
            this.hpId = hpid;
            this.name = name;
            this.alternatives = alter;
            this.state = state;
            sourceType = SourceType.None;
        }
        public Phenotype(string hpid, string name, int state)
        {
            this.hpId = hpid;
            this.name = name;
            this.alternatives = new List<string>();
            this.state = state;
            sourceType = SourceType.None;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void RaisePropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        public Phenotype Clone()
        {

            Phenotype p = new Phenotype(this.hpId, this.name, this.alternatives, this.state);
            return p;
        }



        // Initiate from json of Phenotips
        public Phenotype(PhenotypeInfo row)
        {
            this.hpId = row.id;
            this.name = row.name;
            this.alternatives = row.synonym;
            this.state = -1;
        }
        // Initiate from NCR
        public Phenotype(NCRPhenotype p)
        {
            this.hpId = p.hp_id;
            this.name = p.names[0];
            this.state = -1;
        }
        public Phenotype(SuggestPhenotype sp)
        {
            this.hpId = sp.id;
            this.name = sp.name;
            this.alternatives = new List<string>();
            this.state = -1;
        }
        public override bool Equals(object obj)
        {
            var phenotype = obj as Phenotype;
            return phenotype != null &&
                   hpId == phenotype.hpId;
        }

        public bool Equals(Phenotype other)
        {
            return other != null &&
                   hpId == other.hpId;
        }

        public override int GetHashCode()
        {
            return -1032463776 + EqualityComparer<string>.Default.GetHashCode(hpId);
        }

        public static bool operator ==(Phenotype phenotype1, Phenotype phenotype2)
        {
            return EqualityComparer<Phenotype>.Default.Equals(phenotype1, phenotype2);
        }

        public static bool operator !=(Phenotype phenotype1, Phenotype phenotype2)
        {
            return !(phenotype1 == phenotype2);
        }

    }
}
