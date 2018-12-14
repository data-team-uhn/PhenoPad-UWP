using PhenoPad.CustomControl;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace PhenoPad.FileService
{
    [Serializable]
    public class EHRFormats
    {
        [XmlArray("inserts")]
        [XmlArrayItem("Range", typeof(List<int>))]
        public List<List<int>> inserts;

        [XmlArray("highlights")]
        [XmlArrayItem("Range", typeof(List<int>))]
        public List<List<int>> highlights;

        [XmlArray("deletes")]
        [XmlArrayItem("Range", typeof(List<int>))]
        public List<List<int>> deletes;

        [XmlArray("annotates")]
        [XmlArrayItem("Range", typeof(List<int>))]
        public List<List<int>> annotates;


        public EHRFormats() {
        }

        public EHRFormats(EHRPageControl ehr)
        {
            this.inserts = ehr.inserts;
            this.highlights = ehr.highlights;
            this.deletes = ehr.deletes;
            this.annotates = ehr.annotated;
        }

        public String Serialize()
        {
            var xs = new XmlSerializer(this.GetType()); 
            using (var sw = new StringWriter())
            {
                xs.Serialize(sw, this);
                var result = sw.ToString();
                return result;
            }
        }

        public static EHRFormats Deserialize(String xml) { 
            var xs = new XmlSerializer(typeof(EHRFormats)); 
            using (var sr = new StringReader(xml))
            {
                var result = (EHRFormats)(xs.Deserialize(sr));
                return result;
            }
        }
    }
}
