﻿using PhenoPad.FileService;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Input.Inking;
using Windows.UI.Input.Inking.Analysis;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace PhenoPad.CustomControl
{
    public sealed partial class AddInImageControl : UserControl
    {
        InkAnalyzer inkAnalyzer = new InkAnalyzer();
        IReadOnlyList<InkStroke> inkStrokes = null;
        InkAnalysisResult inkAnalysisResults = null;

        public string name { get; set; }
        public string notebookId { get; set; }
        public string pageId { get; set; }
        public double height { get; set; }
        public double width { get; set; }
        public double canvasLeft { get; set; }
        public double canvasTop { get; set; }
        public InkCanvas inkCan
        {
            get {
                return this.inkCanvas;
            }
        }

        public AddInImageControl()
        {
            this.InitializeComponent();
        }
        
        public AddInImageControl(string notebookId, string pageId, string name)
        {
            this.InitializeComponent();  
        }

        public void initialize(string notebookId, string pageId, string name)
        {
            this.name = name;
            // Set supported inking device types.
            inkCanvas.InkPresenter.InputDeviceTypes =
                Windows.UI.Core.CoreInputDeviceTypes.Mouse |
                Windows.UI.Core.CoreInputDeviceTypes.Pen;

            // Set initial ink stroke attributes.
            InkDrawingAttributes drawingAttributes = new InkDrawingAttributes();
            drawingAttributes.Color = Windows.UI.Colors.Black;
            drawingAttributes.IgnorePressure = false;
            drawingAttributes.FitToCurve = true;
            inkCanvas.InkPresenter.UpdateDefaultDrawingAttributes(drawingAttributes);
        }

        private bool hide = false;
        public void deleteAsHide()
        {
            hide = true;
        }

        public Image getImageControl()
        {
            return imageControl;
        }
        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (this.hide)
            {
                this.Visibility = Visibility.Collapsed;
            }
            else
            {
                ((Panel)this.Parent).Children.Remove(this);
            }
        }

        public async void SaveToDisk()
        {
            StorageFile file = await FileManager.getSharedFileManager().GetNoteFile(notebookId, pageId, NoteFileType.ImageAnnotation, name);
            if (file != null)
                await FileManager.getSharedFileManager().saveStrokes(file, this.inkCan);
        }
    }
}