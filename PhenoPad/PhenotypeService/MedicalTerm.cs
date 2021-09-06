using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhenoPad.PhenotypeService
{
    //public class MedicalTermRaw
    //{
    //    public int n;
    //    public string start;
    //    public string end;
    //    public string speaker;
    //    public string text;
    //    public Dictionary<string, Dictionary<string, List<string>>> parse_result;
    //}

    public class TranscriptRaw
    {
        public List<SpeechTranscriptRaw> transcripts;
        public Dictionary<string, MedicalConceptRaw> concepts;
    }

    public class MedicalConceptRaw
    {
        public string type;
        public List<int> line_ids;
        public List<int> starts;
        public List<int> ends;
        public List<string> text;
    }

    public class SpeechTranscriptRaw
    {
        public int n;
        public string start;
        public string end;
        public string speaker;
        public string text;
    }


    public class MedicalTerm : IEquatable<MedicalTerm>, INotifyPropertyChanged
    {
        private List<string> type_list = new List<string>() { "AnatomicalSiteMention", "DiseaseDisorderMention", "MedicationMention", "ProcedureMention", "SignSymptomMention" };

        public string Id { get; set; }
        public string Name { get; set; }
        public List<int> MessageIndexList { get; set; }

        private string type;
        public string Type
        {
            get { return type; }
            set { type = value; }
        }
        public int TypeId
        {
            get { return type_list.IndexOf(type); }
        }

        public int PageSource { get; set; }
        public SourceType SourceType { get; set; }
        public List<string> Text { get; set; }

        public DateTime time;


        public MedicalTerm()
        {
        }

        // Initiate from phenopad
        public MedicalTerm(string Id, string Name, string Type, List<string> Text, List<int> MessageIndexList, SourceType st = SourceType.None)
        {
            this.Id = Id;
            this.Name = Name;
            this.Type = Type;
            this.Text = Text;
            this.MessageIndexList = MessageIndexList;
        }



        [System.Xml.Serialization.XmlIgnore]
        public Action<MedicalTerm> OnRemoveCallback { get; set; }
        public void OnRemove()
        {
            OnRemoveCallback(this);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void RaisePropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }


        public override bool Equals(object obj)
        {
            var term = obj as MedicalTerm;
            return term != null &&
                   Text.Equals(term.Text);
        }

        public bool Equals(MedicalTerm other)
        {
            return other != null &&
                   Text.Equals(other.Text);
        }

        public override int GetHashCode()
        {
            return -1032463776 + EqualityComparer<string>.Default.GetHashCode(Text[0]);
        }

        public static bool operator ==(MedicalTerm phenotype1, MedicalTerm phenotype2)
        {
            return EqualityComparer<MedicalTerm>.Default.Equals(phenotype1, phenotype2);
        }

        public static bool operator !=(MedicalTerm phenotype1, MedicalTerm phenotype2)
        {
            return !(phenotype1 == phenotype2);
        }
    }


    //public class MedicalTerm : IEquatable<MedicalTerm>, INotifyPropertyChanged
    //{
    //    private List<string> type_list = new List<string>() { "Sign or Symptom", "Disease or Syndrome", "", "Clinical Drug", "Body Location or Region", "Body Part, Organ, or Organ Component", "Organic Chemical,Pharmacologic Substance" };

    //    public string Id { get; set; }
    //    public string Name { get; set; }
    //    public int MessageIndex { get; set; }

    //    private string type;
    //    public string Type
    //    {
    //        get { return type; }
    //        set { type = value; }
    //    }
    //    public int TypeId
    //    {
    //        get { return type_list.IndexOf(type); }
    //    }

    //    public int PageSource { get; set; }
    //    public SourceType SourceType { get; set; }
    //    public string Text { get; set; }

    //    public DateTime time;


    //    public MedicalTerm()
    //    {
    //    }

    //    // Initiate from phenopad
    //    public MedicalTerm(string Id, string Name, string Type, string Text, int MessageIndex, SourceType st = SourceType.None)
    //    {
    //        this.Id = Id;
    //        this.Name = Name;
    //        this.Type = Type;
    //        this.Text = Text;
    //        this.MessageIndex = MessageIndex;
    //    }



    //    [System.Xml.Serialization.XmlIgnore]
    //    public Action<MedicalTerm> OnRemoveCallback { get; set; }
    //    public void OnRemove()
    //    {
    //        OnRemoveCallback(this);
    //    }

    //    public event PropertyChangedEventHandler PropertyChanged;
    //    protected void RaisePropertyChanged(string name)
    //    {
    //        if (PropertyChanged != null)
    //        {
    //            PropertyChanged(this, new PropertyChangedEventArgs(name));
    //        }
    //    }


    //    public override bool Equals(object obj)
    //    {
    //        var term = obj as MedicalTerm;
    //        return term != null &&
    //               Text.Equals(term.Text);
    //    }

    //    public bool Equals(MedicalTerm other)
    //    {
    //        return other != null &&
    //               Text.Equals(other.Text);
    //    }

    //    public override int GetHashCode()
    //    {
    //        return -1032463776 + EqualityComparer<string>.Default.GetHashCode(Text);
    //    }

    //    public static bool operator ==(MedicalTerm phenotype1, MedicalTerm phenotype2)
    //    {
    //        return EqualityComparer<MedicalTerm>.Default.Equals(phenotype1, phenotype2);
    //    }

    //    public static bool operator !=(MedicalTerm phenotype1, MedicalTerm phenotype2)
    //    {
    //        return !(phenotype1 == phenotype2);
    //    }
    //}
}
