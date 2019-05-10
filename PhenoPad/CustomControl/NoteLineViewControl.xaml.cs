using PhenoPad.PhenotypeService;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Input.Inking;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace PhenoPad.CustomControl
{
    public sealed partial class NoteLineViewControl : UserControl
    {
        public DateTime keyTime;
        public int keyLine;
        public string type;
        public int pageID;

        public List<InkStroke> strokes;
        public AddInControl addin;
        public List<WordBlockControl> HWRs;

        public List<Phenotype> phenotypes;
        public List<TextMessage> chats;
            
        public NoteLineViewControl(DateTime time, int line, string type)
        {
            this.InitializeComponent();
            keyTime = time;
            keyLine = line;
            this.type = type;
        }

        public void UpdateUILayout() {
            Debug.WriteLine(keyTime.ToLongTimeString());
            KeyTime.Text = keyTime.ToLongTimeString();
            UpdateLayout();
        }


    }
}
