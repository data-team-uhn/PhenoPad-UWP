﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

using Windows.Graphics.Imaging;
using Windows.UI.Xaml.Media.Imaging;
using Microsoft.Graphics.Canvas;

using MyScript.IInk.UIReferenceImplementation;
using MyScript.IInk.UIReferenceImplementation.UserControls;
using MyScript.IInk;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.Storage;
using System.IO;
using Windows.UI.Popups;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace PhenoPad.CustomControl
{
    public sealed partial class MyScriptTextEditor : UserControl
    {
        //MyScript
        private Engine _engine;

        private Editor _editor => UcEditor.Editor;

        private MyScript.IInk.Graphics.Point _lastPointerPosition;
        private ContentBlock _lastSelectedBlock;

        private int _filenameIndex;
        private string _packageName;

        public MyScriptTextEditor()
        {
            this.InitializeComponent();

            //MyScript
            _engine = App.Engine;

            // Folders "conf" and "resources" are currently parts of the layout
            // (for each conf/res file of the project => properties => "Build Action = content")
            var confDirs = new string[1];
            confDirs[0] = "conf";
            _engine.Configuration.SetStringArray("configuration-manager.search-path", confDirs);

            var localFolder = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
            var tempFolder = System.IO.Path.Combine(localFolder, "tmp");
            _engine.Configuration.SetString("content-package.temp-folder", tempFolder);

            // Initialize the editor with the engine
            UcEditor.Engine = _engine;
            UcEditor.SmartGuide.MoreClicked += ShowSmartGuideMenu;

            // Force pointer to be a pen, for an automatic detection, set InputMode to AUTO
            SetInputMode(InputMode.PEN);

            //NewFile();
        }

        // MyScript
        private void SetInputMode(InputMode inputMode)
        {
            UcEditor.InputMode = inputMode;
            penModeToggleButton.IsChecked = (inputMode == InputMode.PEN);
            touchModeToggleButton.IsChecked = (inputMode == InputMode.TOUCH);
            autoModeToggleButton.IsChecked = (inputMode == InputMode.AUTO);
        }

        private void AppBar_UndoButton_Click(object sender, RoutedEventArgs e)
        {
            _editor.Undo();
        }

        private void AppBar_RedoButton_Click(object sender, RoutedEventArgs e)
        {
            _editor.Redo();
        }

        private void AppBar_ClearButton_Click(object sender, RoutedEventArgs e)
        {
            _editor.Clear();
        }

        private async void AppBar_ConvertButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _editor.Convert(null, _editor.GetSupportedTargetConversionStates(null)[0]);
            }
            catch (Exception ex)
            {
                var msgDialog = new MessageDialog(ex.ToString());
                await msgDialog.ShowAsync();
            }
        }

        private void AppBar_PenModeButton_Click(object sender, RoutedEventArgs e)
        {
            var isChecked = ((ToggleButton)(sender)).IsChecked;
            if (isChecked != null && (bool)isChecked)
            {
                SetInputMode(InputMode.PEN);
            }
        }

        private void AppBar_TouchModeButton_Click(object sender, RoutedEventArgs e)
        {
            var isChecked = ((ToggleButton)(sender)).IsChecked;
            if (isChecked != null && (bool)isChecked)
            {
                SetInputMode(InputMode.TOUCH);
            }
        }

        private void AppBar_AutoModeButton_Click(object sender, RoutedEventArgs e)
        {
            var isChecked = ((ToggleButton)(sender)).IsChecked;
            if (isChecked != null && (bool)isChecked)
            {
                SetInputMode(InputMode.AUTO);
            }
        }

        private void AppBar_NewPackageButton_Click(object sender, RoutedEventArgs e)
        {
            NewFile();
        }

        private async void AppBar_NewPartButton_Click(object sender, RoutedEventArgs e)
        {
            if (_editor.Part == null)
            {
                NewFile();
                return;
            }

            var partType = await ChoosePartType(true);

            if (!string.IsNullOrEmpty(partType))
            {
                // Reset viewing parameters
                UcEditor.ResetView(false);

                // Create package and part
                var package = _editor.Part.Package;
                var part = package.CreatePart(partType);
                _editor.Part = part;
                Title.Text = _packageName + " - " + part.Type;
            }
        }

        private void AppBar_PreviousPartButton_Click(object sender, RoutedEventArgs e)
        {
            var part = _editor.Part;

            if (part != null)
            {
                var index = part.Package.IndexOfPart(part);

                if (index > 0)
                {
                    // Reset viewing parameters
                    UcEditor.ResetView(false);

                    // Select new part
                    var newPart = part.Package.GetPart(index - 1);
                    _editor.Part = newPart;
                    Title.Text = _packageName + " - " + newPart.Type;
                }
            }
        }

        private void AppBar_NextPartButton_Click(object sender, RoutedEventArgs e)
        {
            var part = _editor.Part;

            if (part != null)
            {
                var index = part.Package.IndexOfPart(part);

                if (index < part.Package.PartCount - 1)
                {
                    // Reset viewing parameters
                    UcEditor.ResetView(false);

                    // Select new part
                    var newPart = part.Package.GetPart(index + 1);
                    _editor.Part = newPart;
                    Title.Text = _packageName + " - " + newPart.Type;
                }
            }
        }
        private void AppBar_ResetViewButton_Click(object sender, RoutedEventArgs e)
        {
            UcEditor.ResetView(true);
        }

        private void AppBar_ZoomInButton_Click(object sender, RoutedEventArgs e)
        {
            UcEditor.ZoomIn(1);
        }

        private void AppBar_ZoomOutButton_Click(object sender, RoutedEventArgs e)
        {
            UcEditor.ZoomOut(1);
        }

        private async void AppBar_OpenPackageButton_Click(object sender, RoutedEventArgs e)
        {
            List<string> files = new List<string>();

            // List iink files inside LocalFolders
            var localFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
            var items = await localFolder.GetItemsAsync();
            foreach (var item in items)
            {
                if (item.IsOfType(StorageItemTypes.File) && item.Path.EndsWith(".iink"))
                    files.Add(item.Name.ToString());
            }
            if (files.Count == 0)
                return;

            // Display file list
            ListBox fileList = new ListBox
            {
                ItemsSource = files,
                SelectedIndex = 0
            };
            ContentDialog fileNameDialog = new ContentDialog
            {
                Title = "Select Package Name",
                Content = fileList,
                IsSecondaryButtonEnabled = true,
                PrimaryButtonText = "Ok",
                SecondaryButtonText = "Cancel",
            };
            if (await fileNameDialog.ShowAsync() == ContentDialogResult.Secondary)
                return;

            var fileName = fileList.SelectedValue.ToString();
            var filePath = System.IO.Path.Combine(localFolder.Path.ToString(), fileName);

            // Open package and select first part
            _editor.Part = null;
            var package = _engine.OpenPackage(filePath);
            var part = package.GetPart(0);
            _editor.Part = part;
            _packageName = fileName;
            Title.Text = _packageName + " - " + part.Type;
        }

        private void AppBar_SavePackageButton_Click(object sender, RoutedEventArgs e)
        {
            var part = _editor.Part;

            part?.Package.Save();
        }

        private async void AppBar_SaveAsButton_Click(object sender, RoutedEventArgs e)
        {
            var localFolder = Windows.Storage.ApplicationData.Current.LocalFolder;

            // Show file name input dialog
            TextBox inputTextBox = new TextBox
            {
                AcceptsReturn = false,
                Height = 32
            };
            ContentDialog fileNameDialog = new ContentDialog
            {
                Title = "Enter New Package Name",
                Content = inputTextBox,
                IsSecondaryButtonEnabled = true,
                PrimaryButtonText = "Ok",
                SecondaryButtonText = "Cancel",
            };

            if (await fileNameDialog.ShowAsync() == ContentDialogResult.Secondary)
                return;

            var fileName = inputTextBox.Text;
            if (fileName == null || fileName == "")
                return;

            // Add iink extension if needed
            if (!fileName.EndsWith(".iink"))
                fileName = fileName + ".iink";

            // Display overwrite dialog (if needed)
            string filePath = null;
            var item = await localFolder.TryGetItemAsync(fileName);
            if (item != null)
            {
                ContentDialog overwriteDialog = new ContentDialog
                {
                    Title = "File Already Exists",
                    Content = "A file with that name already exists, overwrite it?",
                    PrimaryButtonText = "Cancel",
                    SecondaryButtonText = "Overwrite"
                };

                ContentDialogResult result = await overwriteDialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                    return;

                filePath = item.Path.ToString();
            }
            else
            {
                filePath = System.IO.Path.Combine(localFolder.Path.ToString(), fileName);
            }

            // Get current package
            var part = _editor.Part;
            if (part == null)
                return;
            var package = part.Package;

            // Save Package with new name
            package.SaveAs(filePath);

            // Update internals
            _packageName = fileName;
            Title.Text = _packageName + " - " + part.Type;
        }

        private void DisplayContextualMenu(Windows.Foundation.Point globalPos)
        {
            var contentBlock = _lastSelectedBlock;

            var supportedTypes = _editor.SupportedAddBlockTypes;
            var supportedExports = _editor.GetSupportedExportMimeTypes(contentBlock);
            var supportedImportTypes = _editor.GetSupportedImportMimeTypes(contentBlock);

            var isContainer = contentBlock.Type == "Container";
            var isRoot = contentBlock.Id == _editor.GetRootBlock().Id;

            var displayConvert = !isContainer && !_editor.IsEmpty(contentBlock);
            var displayAddBlock = supportedTypes != null && supportedTypes.Any() && isContainer;
            var displayAddImage = false; // supportedTypes != null && supportedTypes.Any() && isContainer;
            var displayRemove = !isRoot && !isContainer;
            var displayCopy = !isRoot && !isContainer;
            var displayPaste = supportedTypes != null && supportedTypes.Any() && isContainer;
            var displayImport = supportedImportTypes != null && supportedImportTypes.Any();
            var displayExport = supportedExports != null && supportedExports.Any();
            var displayOfficeClipboard = (supportedExports != null) && supportedExports.Contains(MimeType.OFFICE_CLIPBOARD);

            var flyoutMenu = new MenuFlyout();

            if (displayConvert)
            {
                var command = new FlyoutCommand("Convert", (cmd) => { Popup_CommandHandler_Convert(cmd); });
                var flyoutItem = new MenuFlyoutItem { Text = "Convert", Command = command };
                flyoutMenu.Items.Add(flyoutItem);
            }

            if (displayRemove)
            {
                var command = new FlyoutCommand("Remove", (cmd) => { Popup_CommandHandler_Remove(cmd); });
                var flyoutItem = new MenuFlyoutItem { Text = "Remove", Command = command };
                flyoutMenu.Items.Add(flyoutItem);
            }

            if (displayCopy)
            {
                var command = new FlyoutCommand("Copy", (cmd) => { Popup_CommandHandler_Copy(cmd); });
                var flyoutItem = new MenuFlyoutItem { Text = "Copy", Command = command };
                flyoutMenu.Items.Add(flyoutItem);
            }

            if (displayPaste)
            {
                var command = new FlyoutCommand("Paste", (cmd) => { Popup_CommandHandler_Paste(cmd); });
                var flyoutItem = new MenuFlyoutItem { Text = "Paste", Command = command };
                flyoutMenu.Items.Add(flyoutItem);
            }

            if (displayOfficeClipboard)
            {
                var command = new FlyoutCommand("Copy To Clipboard (Microsoft Office)", (cmd) => { Popup_CommandHandler_OfficeClipboard(cmd); });
                var flyoutItem = new MenuFlyoutItem { Text = "Copy To Clipboard (Microsoft Office)", Command = command };
                flyoutMenu.Items.Add(flyoutItem);
            }

            if (displayAddBlock || displayAddImage)
            {
                var flyoutSubItem = new MenuFlyoutSubItem { Text = "Add..." };

                if (displayAddBlock)
                {
                    for (var i = 0; i < supportedTypes.Count(); ++i)
                    {
                        var command = new FlyoutCommand(supportedTypes[i], (cmd) => { Popup_CommandHandler_AddBlock(cmd); });
                        var flyoutItem = new MenuFlyoutItem { Text = "Add " + supportedTypes[i], Command = command };
                        flyoutSubItem.Items.Add(flyoutItem);
                    }
                }

                if (displayAddImage)
                {
                    var command = new FlyoutCommand("Image", (cmd) => { Popup_CommandHandler_AddImage(cmd); });
                    var flyoutItem = new MenuFlyoutItem { Text = "Add Image", Command = command };
                    flyoutSubItem.Items.Add(flyoutItem);
                }

                flyoutMenu.Items.Add(flyoutSubItem);
            }

            if (displayImport || displayExport)
            {
                var flyoutSubItem = new MenuFlyoutSubItem { Text = "Import/Export..." };

                if (displayImport)
                {
                    var command = new FlyoutCommand("Import", (cmd) => { Popup_CommandHandler_Import(cmd); });
                    var flyoutItem = new MenuFlyoutItem { Text = "Import", Command = command };
                    flyoutSubItem.Items.Add(flyoutItem);
                }

                if (displayExport)
                {
                    var command = new FlyoutCommand("Export", (cmd) => { Popup_CommandHandler_Export(cmd); });
                    var flyoutItem = new MenuFlyoutItem { Text = "Export", Command = command };
                    flyoutSubItem.Items.Add(flyoutItem);
                }

                flyoutMenu.Items.Add(flyoutSubItem);
            }

            flyoutMenu.ShowAt(null, globalPos);
        }

        private void UcEditor_RightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            var pos = e.GetPosition(UcEditor);

            _lastPointerPosition = new MyScript.IInk.Graphics.Point((float)pos.X, (float)pos.Y);
            _lastSelectedBlock = _editor.HitBlock(_lastPointerPosition.X, _lastPointerPosition.Y);

            if (_lastSelectedBlock == null)
                _lastSelectedBlock = _editor.GetRootBlock();

            if (_lastSelectedBlock != null)
            {
                var globalPos = e.GetPosition(null);
                DisplayContextualMenu(globalPos);
                e.Handled = true;
            }
        }

        private void ShowSmartGuideMenu(Windows.Foundation.Point globalPos)
        {
            _lastSelectedBlock = UcEditor.SmartGuide.ContentBlock;

            if (_lastSelectedBlock != null)
                DisplayContextualMenu(globalPos);
        }

        private async void Popup_CommandHandler_Convert(FlyoutCommand command)
        {
            try
            {
                if (_lastSelectedBlock != null)
                {
                    var supportedStates = _editor.GetSupportedTargetConversionStates(_lastSelectedBlock);

                    if ((supportedStates != null) && (supportedStates.Count() > 0))
                        _editor.Convert(_lastSelectedBlock, supportedStates[0]);
                }
            }
            catch (Exception ex)
            {
                var msgDialog = new MessageDialog(ex.ToString());
                await msgDialog.ShowAsync();
            }
        }

        private async void Popup_CommandHandler_AddBlock(FlyoutCommand command)
        {
            try
            {
                // Uses Id as block type
                var blockType = command.Id.ToString();
                var mimeTypes = _editor.GetSupportedAddBlockDataMimeTypes(blockType);
                var useDialog = (mimeTypes != null) && (mimeTypes.Count() > 0);

                if (!useDialog)
                {
                    _editor.AddBlock(_lastPointerPosition.X, _lastPointerPosition.Y, blockType);
                    UcEditor.Invalidate(LayerType.LayerType_ALL);
                }
                else
                {
                    var result = await EnterImportData("Add Content Block", mimeTypes);

                    if (result != null)
                    {
                        var idx = result.Item1;
                        var data = result.Item2;

                        if ((idx >= 0) && (idx < mimeTypes.Count()) && (String.IsNullOrWhiteSpace(data) == false))
                        {
                            _editor.AddBlock(_lastPointerPosition.X, _lastPointerPosition.Y, blockType, mimeTypes[idx], data);
                            UcEditor.Invalidate(LayerType.LayerType_ALL);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var msgDialog = new MessageDialog(ex.ToString());
                await msgDialog.ShowAsync();
            }

        }

        private void Popup_CommandHandler_AddImage(FlyoutCommand command)
        {
            // TODO
        }

        private async void Popup_CommandHandler_Remove(FlyoutCommand command)
        {
            try
            {
                if (_lastSelectedBlock != null && _lastSelectedBlock.Type != "Container")
                    _editor.RemoveBlock(_lastSelectedBlock);
            }
            catch (Exception ex)
            {
                var msgDialog = new MessageDialog(ex.ToString());
                await msgDialog.ShowAsync();
            }
        }

        private async void Popup_CommandHandler_Copy(FlyoutCommand command)
        {
            try
            {
                if (_lastSelectedBlock != null)
                    _editor.Copy(_lastSelectedBlock);
            }
            catch (Exception ex)
            {
                var msgDialog = new MessageDialog(ex.ToString());
                await msgDialog.ShowAsync();
            }
        }

        private async void Popup_CommandHandler_Paste(FlyoutCommand command)
        {
            try
            {
                _editor.Paste(_lastPointerPosition.X, _lastPointerPosition.Y);
            }
            catch (Exception ex)
            {
                var msgDialog = new MessageDialog(ex.ToString());
                await msgDialog.ShowAsync();
            }
        }

        private async void Popup_CommandHandler_Import(FlyoutCommand command)
        {
            var part = _editor.Part;
            if (part == null)
                return;

            if (_lastSelectedBlock == null)
                return;

            var mimeTypes = _editor.GetSupportedImportMimeTypes(_lastSelectedBlock);

            if (mimeTypes == null)
                return;

            if (mimeTypes.Count() == 0)
                return;

            var result = await EnterImportData("Import", mimeTypes);

            if (result != null)
            {
                var idx = result.Item1;
                var data = result.Item2;

                if ((idx >= 0) && (idx < mimeTypes.Count()) && (String.IsNullOrWhiteSpace(data) == false))
                {
                    try
                    {
                        _editor.Import_(mimeTypes[idx], data, _lastSelectedBlock);
                    }
                    catch (Exception ex)
                    {
                        var msgDialog = new MessageDialog(ex.ToString());
                        await msgDialog.ShowAsync();
                    }
                }

            }
        }

        private async void Popup_CommandHandler_Export(FlyoutCommand command)
        {
            var part = _editor.Part;
            if (part == null)
                return;

            if (_lastSelectedBlock == null)
                return;

            var mimeTypes = _editor.GetSupportedExportMimeTypes(_lastSelectedBlock);

            if (mimeTypes == null)
                return;

            if (mimeTypes.Count() == 0)
                return;

            // Show export dialog
            var fileName = await ChooseExportFilename(mimeTypes);

            if (!string.IsNullOrEmpty(fileName))
            {
                var localFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
                var item = await localFolder.TryGetItemAsync(fileName);
                string filePath = null;

                if (item != null)
                {
                    ContentDialog overwriteDialog = new ContentDialog
                    {
                        Title = "File Already Exists",
                        Content = "A file with that name already exists, overwrite it?",
                        PrimaryButtonText = "Cancel",
                        SecondaryButtonText = "Overwrite"
                    };

                    ContentDialogResult result = await overwriteDialog.ShowAsync();
                    if (result == ContentDialogResult.Primary)
                        return;

                    filePath = item.Path.ToString();
                }
                else
                {
                    filePath = System.IO.Path.Combine(localFolder.Path.ToString(), fileName);
                }

                try
                {
                    var drawer = new ImageDrawer(_editor.Renderer.DpiX, _editor.Renderer.DpiY);

                    drawer.ImageLoader = UcEditor.ImageLoader;

                    _editor.WaitForIdle();
                    _editor.Export_(_lastSelectedBlock, filePath, drawer);

                    var file = await StorageFile.GetFileFromPathAsync(filePath);
                    await Windows.System.Launcher.LaunchFileAsync(file);
                }
                catch (Exception ex)
                {
                    var msgDialog = new MessageDialog(ex.ToString());
                    await msgDialog.ShowAsync();
                }
            }
        }

        private async void Popup_CommandHandler_OfficeClipboard(FlyoutCommand command)
        {
            try
            {
                MimeType[] mimeTypes = null;

                if (_lastSelectedBlock != null)
                    mimeTypes = _editor.GetSupportedExportMimeTypes(_lastSelectedBlock);

                if (mimeTypes != null && mimeTypes.Contains(MimeType.OFFICE_CLIPBOARD))
                {
                    // export block to a file
                    var localFolder = ApplicationData.Current.LocalFolder.Path;
                    var clipboardPath = System.IO.Path.Combine(localFolder.ToString(), "tmp/clipboard.gvml");
                    var drawer = new ImageDrawer(_editor.Renderer.DpiX, _editor.Renderer.DpiY);

                    drawer.ImageLoader = UcEditor.ImageLoader;

                    _editor.Export_(_lastSelectedBlock, clipboardPath.ToString(), MimeType.OFFICE_CLIPBOARD, drawer);

                    // read back exported data
                    var clipboardData = File.ReadAllBytes(clipboardPath);
                    var clipboardStream = new MemoryStream(clipboardData);

                    // store the data into clipboard
                    Windows.ApplicationModel.DataTransfer.Clipboard.Clear();
                    var clipboardContent = new Windows.ApplicationModel.DataTransfer.DataPackage();
                    clipboardContent.SetData(MimeTypeF.GetTypeName(MimeType.OFFICE_CLIPBOARD), clipboardStream.AsRandomAccessStream());
                    Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(clipboardContent);
                }
            }
            catch (Exception ex)
            {
                MessageDialog msgDialog = new MessageDialog(ex.ToString());
                await msgDialog.ShowAsync();
            }
        }

        public async void NewFile()
        {
            var cancelable = _editor.Part != null;
            var partType = await ChoosePartType(cancelable);
            if (string.IsNullOrEmpty(partType))
                return;

            var packageName = MakeUntitledFilename();

            // Create package and part
            _editor.Part = null;
            var package = _engine.CreatePackage(packageName);
            var part = package.CreatePart(partType);
            _editor.Part = part;
            _packageName = System.IO.Path.GetFileName(packageName);
            Title.Text = _packageName + " - " + part.Type;
        }

        private string MakeUntitledFilename()
        {
            var localFolder = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
            string name;

            do
            {
                var baseName = "File" + (++_filenameIndex) + ".iink";
                name = System.IO.Path.Combine(localFolder, baseName);
            }
            while (System.IO.File.Exists(name));

            return name;
        }


        private async System.Threading.Tasks.Task<string> ChoosePartType(bool cancelable)
        {
            var types = _engine.SupportedPartTypes.ToList();

            if (types.Count == 0)
                return null;

            var view = new ListView
            {
                ItemsSource = types,
                IsItemClickEnabled = true,
                SelectionMode = ListViewSelectionMode.Single,
                SelectedIndex = -1
            };

            var grid = new Grid();
            grid.Children.Add(view);

            var dialog = new ContentDialog
            {
                Title = "Choose type of content",
                Content = grid,
                PrimaryButtonText = "OK",
                SecondaryButtonText = cancelable ? "Cancel" : "",
                IsPrimaryButtonEnabled = false,
                IsSecondaryButtonEnabled = cancelable
            };

            view.ItemClick += (sender, args) => { dialog.IsPrimaryButtonEnabled = true; };
            dialog.PrimaryButtonClick += (sender, args) => { if (view.SelectedIndex < 0) args.Cancel = true; };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
                return types[view.SelectedIndex];
            else
                return null;
        }

        private async System.Threading.Tasks.Task<Tuple<int, string>> EnterImportData(string title, MimeType[] mimeTypes)
        {
            var mimeTypeTextBlock = new TextBlock
            {
                Text = "Choose a mime type",
                MaxLines = 1,
                TextWrapping = TextWrapping.NoWrap,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 0),
                Width = 300,
            };

            var mimeTypeComboBox = new ComboBox
            {
                IsTextSearchEnabled = true,
                SelectedIndex = -1,
                Margin = new Thickness(0, 5, 0, 0),
                Width = 300
            };

            foreach (var mimeType in mimeTypes)
                mimeTypeComboBox.Items.Add(MimeTypeF.GetTypeName(mimeType));

            mimeTypeComboBox.SelectedIndex = 0;

            var dataTextBlock = new TextBlock
            {
                Text = "Enter some text",
                MaxLines = 1,
                TextWrapping = TextWrapping.NoWrap,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0),
                Width = 300
            };

            var dataTextBox = new TextBox
            {
                Text = "",
                AcceptsReturn = false,
                MaxLength = 1024 * 1024,
                TextWrapping = TextWrapping.NoWrap,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 10),
                Width = 300
            };

            var panel = new StackPanel
            {
                Margin = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

            panel.Children.Add(mimeTypeTextBlock);
            panel.Children.Add(mimeTypeComboBox);
            panel.Children.Add(dataTextBlock);
            panel.Children.Add(dataTextBox);


            var dialog = new ContentDialog
            {
                Title = title,
                Content = panel,
                PrimaryButtonText = "OK",
                SecondaryButtonText = "Cancel",
                IsPrimaryButtonEnabled = true,
                IsSecondaryButtonEnabled = true
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
                return new Tuple<int, string>(mimeTypeComboBox.SelectedIndex, dataTextBox.Text);

            return null;
        }

        private async System.Threading.Tasks.Task<string> ChooseExportFilename(MimeType[] mimeTypes)
        {
            var mimeTypeTextBlock = new TextBlock
            {
                Text = "Choose a mime type",
                MaxLines = 1,
                TextWrapping = TextWrapping.NoWrap,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 0),
                Width = 300,
            };

            var mimeTypeComboBox = new ComboBox
            {
                IsTextSearchEnabled = true,
                SelectedIndex = -1,
                Margin = new Thickness(0, 5, 0, 0),
                Width = 300
            };

            foreach (var mimeType in mimeTypes)
                mimeTypeComboBox.Items.Add(MimeTypeF.GetTypeName(mimeType));

            mimeTypeComboBox.SelectedIndex = 0;

            var nameTextBlock = new TextBlock
            {
                Text = "Enter Export File Name",
                MaxLines = 1,
                TextWrapping = TextWrapping.NoWrap,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0),
                Width = 300
            };

            var nameTextBox = new TextBox
            {
                Text = "",
                AcceptsReturn = false,
                MaxLength = 1024 * 1024,
                TextWrapping = TextWrapping.NoWrap,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 10),
                Width = 300
            };

            var panel = new StackPanel
            {
                Margin = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

            panel.Children.Add(mimeTypeTextBlock);
            panel.Children.Add(mimeTypeComboBox);
            panel.Children.Add(nameTextBlock);
            panel.Children.Add(nameTextBox);


            var dialog = new ContentDialog
            {
                Title = "Export",
                Content = panel,
                PrimaryButtonText = "OK",
                SecondaryButtonText = "Cancel",
                IsPrimaryButtonEnabled = true,
                IsSecondaryButtonEnabled = true
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var fileName = nameTextBox.Text;
                var extIndex = mimeTypeComboBox.SelectedIndex;
                var extensions = MimeTypeF.GetFileExtensions(mimeTypes[extIndex]).Split(',');

                int ext;
                for (ext = 0; ext < extensions.Count(); ++ext)
                {
                    if (fileName.EndsWith(extensions[ext], StringComparison.OrdinalIgnoreCase))
                        break;
                }

                if (ext >= extensions.Count())
                    fileName += extensions[0];

                return fileName;
            }

            return null;
        }
    }
}
