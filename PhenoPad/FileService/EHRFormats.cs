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


        public EHRFormats() {
        }

        public EHRFormats(List<List<int>> inserts, List<List<int>> highlights, List<List<int>> deletes)
        {
            this.inserts = inserts;
            this.highlights = highlights;
            this.deletes = deletes;
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
