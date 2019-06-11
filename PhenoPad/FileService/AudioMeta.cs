using System;
using System.Collections.Generic;

using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Windows.Media.Core;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Controls;

namespace PhenoPad.FileService
{
    public class AudioMeta
    {
        [XmlArray("Audios")]
        [XmlArrayItem("name")]
        public List<string> names { get; set; }


        /// <summary>
        /// Creates a new NotePage instance for serilization.
        /// </summary>
        public AudioMeta()
        {
        }
        /// <summary>
        /// Creates and initializes a new NotePage instance based on given Notebook ID and Notepage ID.
        /// </summary>
        public AudioMeta(List<String> names)
        {
            this.names = names;
        }
    }
}
