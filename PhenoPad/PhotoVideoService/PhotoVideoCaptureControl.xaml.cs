﻿
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Graphics.Display;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Phone.UI.Input;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.System.Display;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;


// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace PhenoPad.PhotoVideoService
{
    public sealed partial class PhotoVideoCaptureControl : UserControl
    {
        #region Class Attributes
        // Rotation metadata to apply to the preview stream and recorded videos (MF_MT_VIDEO_ROTATION)
        // Reference: http://msdn.microsoft.com/en-us/library/windows/apps/xaml/hh868174.aspx
        private static readonly Guid RotationKey = new Guid("C380465D-2271-428C-9B83-ECEA3B4A85C1");

        // Folder in which the captures will be stored (initialized in SetupUiAsync)
        private StorageFolder _captureFolder = null;
        // Prevent the screen from sleeping while the camera is running
        private readonly DisplayRequest _displayRequest = new DisplayRequest();

        // For listening to media property changes
        private readonly SystemMediaTransportControls _systemMediaControls = SystemMediaTransportControls.GetForCurrentView();

        // MediaCapture and its state variables
        private MediaCapture _mediaCapture;
        private bool _isInitialized;
        private bool _isPreviewing;
        private bool _isRecording;

        // UI state
        private bool _isSuspending;
        private bool _isActivePage;
        private bool _isUIActive;
        private Task _setupTask = Task.CompletedTask;

        // Information about the camera device
        private bool _mirroringPreview;
        private bool _externalCamera;

        // Rotation Helper to simplify handling rotation compensation for the camera streams
        private CameraRotationHelper _rotationHelper;

        private DispatcherTimer VideoTimer;
        #endregion

        public PhotoVideoCaptureControl()
        {
            this.InitializeComponent();
            VideoTimer = new DispatcherTimer();
            VideoTimer.Interval = TimeSpan.FromSeconds(30);//by default can record for 30 sec
            VideoTimer.Tick += OnTimeUpStopRecording;
        }



        public async void setUp()
        {
            _isActivePage = true;
            await SetUpBasedOnStateAsync();
        }
        public async void unSetUp()
        {
            _isActivePage = false;
            await SetUpBasedOnStateAsync();
        }
        #region Event handlers
        private async void Window_VisibilityChanged(object sender, VisibilityChangedEventArgs args)
        {
            await SetUpBasedOnStateAsync();
        }

    

        private async void PhotoButton_Click(object sender, RoutedEventArgs e)
        {
            await TakePhotoAsync("","","");
        }

        private async void VideoButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isRecording)
            {
                //await StartRecordingAsync();
            }
            else
            {
                //await StopRecordingAsync();
            }

            // After starting or stopping video recording, update the UI to reflect the MediaCapture state
            UpdateCaptureControls();
        }


        private async void MediaCapture_RecordLimitationExceeded(MediaCapture sender)
        {
            // This is a notification that recording has to stop, and the app is expected to finalize the recording

            await StopRecordingAsync();

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => UpdateCaptureControls());
        }

        private async void MediaCapture_Failed(MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs)
        {
            Debug.WriteLine("MediaCapture_Failed: (0x{0:X}) {1}", errorEventArgs.Code, errorEventArgs.Message);
            try
            {
                await CleanupCameraAsync();

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => UpdateCaptureControls());
            }
            catch (Exception) {

            }
           
        }

        #endregion Event handlers


        #region MediaCapture methods

        /// <summary>
        /// Initializes the MediaCapture, registers events, gets camera device information for mirroring and rotating, starts preview and unlocks the UI
        /// </summary>
        /// <returns></returns>
        private async Task InitializeCameraAsync()
        {
            Debug.WriteLine("InitializeCameraAsync");

            if (_mediaCapture == null)
            {
                // Attempt to get the back camera if one is available, but use any camera device if not
                var cameraDevice = await FindCameraDeviceByPanelAsync(Windows.Devices.Enumeration.Panel.Back);

                if (cameraDevice == null)
                {
                    Debug.WriteLine("No camera device found!");
                    return;
                }

                // Create MediaCapture and its settings
                _mediaCapture = new MediaCapture();

                // Register for a notification when video recording has reached the maximum time and when something goes wrong
                _mediaCapture.RecordLimitationExceeded += MediaCapture_RecordLimitationExceeded;
                _mediaCapture.Failed += MediaCapture_Failed;

                var settings = new MediaCaptureInitializationSettings { VideoDeviceId = cameraDevice.Id };

                // Initialize MediaCapture
                try
                {
                    await _mediaCapture.InitializeAsync(settings);
                    _isInitialized = true;
                }
                catch (UnauthorizedAccessException)
                {
                    Debug.WriteLine("The app was denied access to the camera");
                }

                // If initialization succeeded, start the preview
                if (_isInitialized)
                {
                    // Figure out where the camera is located
                    if (cameraDevice.EnclosureLocation == null || cameraDevice.EnclosureLocation.Panel == Windows.Devices.Enumeration.Panel.Unknown)
                    {
                        // No information on the location of the camera, assume it's an external camera, not integrated on the device
                        _externalCamera = true;
                    }
                    else
                    {
                        // Camera is fixed on the device
                        _externalCamera = false;

                        // Only mirror the preview if the camera is on the front panel
                        _mirroringPreview = (cameraDevice.EnclosureLocation.Panel == Windows.Devices.Enumeration.Panel.Front);
                    }

                    // Initialize rotationHelper
                    _rotationHelper = new CameraRotationHelper(cameraDevice.EnclosureLocation);
                    _rotationHelper.OrientationChanged += RotationHelper_OrientationChanged;

                    await StartPreviewAsync();

                    UpdateCaptureControls();
                }
            }
        }

        /// <summary>
        /// Handles an orientation changed event
        /// </summary>
        private async void RotationHelper_OrientationChanged(object sender, bool updatePreview)
        {
            if (updatePreview)
            {
                await SetPreviewRotationAsync();
            }
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => UpdateButtonOrientation());
        }

        /// <summary>
        /// Uses the current device orientation in space and page orientation on the screen to calculate the rotation
        /// transformation to apply to the controls
        /// </summary>
        private void UpdateButtonOrientation()
        {
            // Rotate the buttons in the UI to match the rotation of the device
            var angle = CameraRotationHelper.ConvertSimpleOrientationToClockwiseDegrees(_rotationHelper.GetUIOrientation());
            var transform = new RotateTransform { Angle = angle };

            // The RenderTransform is safe to use (i.e. it won't cause layout issues) in this case, because these buttons have a 1:1 aspect ratio
            PhotoButton.RenderTransform = transform;
            VideoButton.RenderTransform = transform;
        }

        /// <summary>
        /// Starts the preview and adjusts it for for rotation and mirroring after making a request to keep the screen on
        /// </summary>
        /// <returns></returns>
        private async Task StartPreviewAsync()
        {
            // Prevent the device from sleeping while the preview is running
            _displayRequest.RequestActive();

            // Set the preview source in the UI and mirror it if necessary
            PreviewControl.Source = _mediaCapture;
            PreviewControl.FlowDirection = _mirroringPreview ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

            // Start the preview
            await _mediaCapture.StartPreviewAsync();
            _isPreviewing = true;

            // Initialize the preview to the current orientation
            if (_isPreviewing)
            {
                await SetPreviewRotationAsync();
            }
        }

        /// <summary>
        /// Gets the current orientation of the UI in relation to the device (when AutoRotationPreferences cannot be honored) and applies a corrective rotation to the preview
        /// </summary>
        private async Task SetPreviewRotationAsync()
        {
            // Only need to update the orientation if the camera is mounted on the device
            if (_externalCamera) return;

            // Add rotation metadata to the preview stream to make sure the aspect ratio / dimensions match when rendering and getting preview frames
            var rotation = _rotationHelper.GetCameraPreviewOrientation();
            var props = _mediaCapture.VideoDeviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview);
            props.Properties.Add(RotationKey, CameraRotationHelper.ConvertSimpleOrientationToClockwiseDegrees(rotation));
            await _mediaCapture.SetEncodingPropertiesAsync(MediaStreamType.VideoPreview, props, null);
        }

        /// <summary>
        /// Stops the preview and deactivates a display request, to allow the screen to go into power saving modes
        /// </summary>
        /// <returns></returns>
        private async Task StopPreviewAsync()
        {
            // Stop the preview
            _isPreviewing = false;
            await _mediaCapture.StopPreviewAsync();

            // Use the dispatcher because this method is sometimes called from non-UI threads
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                // Cleanup the UI
                PreviewControl.Source = null;

                // Allow the device screen to sleep now that the preview is stopped
                _displayRequest.RequestRelease();
            });
        }

        /// <summary>
        /// Takes a photo to a StorageFile and adds rotation metadata to it,returns the photo file path after encoding.
        /// </summary>
        public async Task<String> TakePhotoAsync(string notebookId, string pageId, string name)
        {

          try { 
                // While taking a photo, keep the video button enabled only if the camera supports simultaneously taking pictures and recording video
                VideoButton.IsEnabled = _mediaCapture.MediaCaptureSettings.ConcurrentRecordAndPhotoSupported;

                // Make the button invisible if it's disabled, so it's obvious it cannot be interacted with
                VideoButton.Opacity = VideoButton.IsEnabled ? 1 : 0;

                var stream = new InMemoryRandomAccessStream();
                Debug.WriteLine("Taking a photo...");
                await _mediaCapture.CapturePhotoToStreamAsync(ImageEncodingProperties.CreateJpeg(), stream);

                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
                SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync();
                SoftwareBitmap softwareBitmapBGR8 = SoftwareBitmap.Convert(softwareBitmap,
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied);
                SoftwareBitmapSource bitmapSource = new SoftwareBitmapSource();
                await bitmapSource.SetBitmapAsync(softwareBitmapBGR8);
                //imageControl.Source = bitmapSource;

            
                //var file = await _captureFolder.CreateFileAsync("SimplePhoto.jpg", CreationCollisionOption.GenerateUniqueName);
                var file = await PhenoPad.FileService.FileManager.getSharedFileManager().CreateImageFileForPage(notebookId, pageId, name);
                if (file == null) {
                    LogService.MetroLogger.getSharedLogger().Info("Failed to create file for saving photo: file is null.");
                    return null;
                }

                LogService.MetroLogger.getSharedLogger().Info($"Photo saving to {file.Path}");

                var photoOrientation = CameraRotationHelper.ConvertSimpleOrientationToPhotoOrientation(_rotationHelper.GetCameraCaptureOrientation());

                await ReencodeAndSavePhotoAsync(stream, file, photoOrientation);
                Debug.WriteLine("Photo saved!");
                // Done taking a photo, so re-enable the button
                VideoButton.IsEnabled = true;
                VideoButton.Opacity = 1;

                return file.Path;

            }
            catch (Exception ex)
            {
                // File I/O errors are reported as exceptions
                Debug.WriteLine("Exception when taking a photo: " + ex.ToString());
                return null;
            }


        }

        public async void OnTimeUpStopRecording(object sender = null, object e = null)
        {
            //VideoTimer.Stop();
            await StopRecordingAsync();
        }

        /// <summary>
        /// Records an MP4 video to a StorageFile and adds rotation metadata to it
        /// </summary>
        /// <returns></returns>
        public async Task<StorageFile> StartRecordingAsync(string notebookId, string pageId, string name)
        {
            try
            {
                // Create storage file for the capture
                var videoFile = await FileService.FileManager.getSharedFileManager().CreateVideoFileForPage(notebookId, pageId, name);

                var encodingProfile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Auto);

                // Calculate rotation angle, taking mirroring into account if necessary
                var rotationAngle = CameraRotationHelper.ConvertSimpleOrientationToClockwiseDegrees(_rotationHelper.GetCameraCaptureOrientation());
                encodingProfile.Video.Properties.Add(RotationKey, PropertyValue.CreateInt32(rotationAngle));

                Debug.WriteLine("Starting recording to " + videoFile.Path);

                await _mediaCapture.StartRecordToStorageFileAsync(encodingProfile, videoFile);
                _isRecording = true;
                VideoButton.Visibility = Visibility.Collapsed;
                PhotoButton.Visibility = Visibility.Collapsed;

                //VideoTimer.Start();
                Debug.WriteLine("Started recording!");
                return videoFile;
            }
            catch (Exception ex)
            {
                // File I/O errors are reported as exceptions
                Debug.WriteLine("Exception when starting video recording: " + ex.ToString());
                return null;
            }
            
        }

        /// <summary>
        /// Stops recording a video
        /// </summary>
        /// <returns></returns>
        private async Task StopRecordingAsync()
        {
            Debug.WriteLine("Stopping recording...");

            _isRecording = false;
            await _mediaCapture.StopRecordAsync();
            Debug.WriteLine("Stopped recording!");
        }

        /// <summary>
        /// Cleans up the camera resources (after stopping any video recording and/or preview if necessary) and unregisters from MediaCapture events
        /// </summary>
        /// <returns></returns>
        private async Task CleanupCameraAsync()
        {
            Debug.WriteLine("CleanupCameraAsync");

            if (_isInitialized)
            {
                // If a recording is in progress during cleanup, stop it to save the recording
                if (_isRecording)
                {
                    await StopRecordingAsync();
                }

                if (_isPreviewing)
                {
                    // The call to stop the preview is included here for completeness, but can be
                    // safely removed if a call to MediaCapture.Dispose() is being made later,
                    // as the preview will be automatically stopped at that point
                    await StopPreviewAsync();
                }

                _isInitialized = false;
            }

            if (_mediaCapture != null)
            {
                _mediaCapture.RecordLimitationExceeded -= MediaCapture_RecordLimitationExceeded;
                _mediaCapture.Failed -= MediaCapture_Failed;
                _mediaCapture.Dispose();
                _mediaCapture = null;
            }

            if (_rotationHelper != null)
            {
                _rotationHelper.OrientationChanged -= RotationHelper_OrientationChanged;
                _rotationHelper = null;
            }
        }

        #endregion MediaCapture methods


        #region Helper functions

        /// <summary>
        /// Initialize or clean up the camera and our UI,
        /// depending on the page state.
        /// </summary>
        /// <returns></returns>
        private async Task SetUpBasedOnStateAsync()
        {
            // Avoid reentrancy: Wait until nobody else is in this function.
            while (!_setupTask.IsCompleted)
            {
                await _setupTask;
            }

            // We want our UI to be active if
            // * We are the current active page.
            // * The window is visible.
            // * The app is not suspending.
            bool wantUIActive = _isActivePage && Window.Current.Visible && !_isSuspending;

            if (_isUIActive != wantUIActive)
            {
                _isUIActive = wantUIActive;

                Func<Task> setupAsync = async () =>
                {
                    if (wantUIActive)
                    {
                        await SetupUiAsync();
                        await InitializeCameraAsync();
                    }
                    else
                    {
                        await CleanupCameraAsync();
                        CleanupUiAsync();
                    }
                };
                _setupTask = setupAsync();
            }

            await _setupTask;
        }

        /// <summary>
        /// Attempts to lock the page orientation, hide the StatusBar (on Phone) and registers event handlers for hardware buttons and orientation sensors
        /// </summary>
        /// <returns></returns>
        private async Task SetupUiAsync()
        {
            // Attempt to lock page to landscape orientation to prevent the CaptureElement from rotating, as this gives a better experience
            DisplayInformation.AutoRotationPreferences = DisplayOrientations.Landscape;

          

            RegisterEventHandlers();
            var picturesLibrary = await StorageLibrary.GetLibraryAsync(KnownLibraryId.Pictures);
            // Fall back to the local app storage if the Pictures Library is not available
            _captureFolder = picturesLibrary.SaveFolder ?? ApplicationData.Current.LocalFolder;
        }

        /// <summary>
        /// Unregisters event handlers for hardware buttons and orientation sensors, allows the StatusBar (on Phone) to show, and removes the page orientation lock
        /// </summary>
        /// <returns></returns>
        private void CleanupUiAsync()
        {
            UnregisterEventHandlers();

            // Revert orientation preferences
            DisplayInformation.AutoRotationPreferences = DisplayOrientations.None;
        }

        /// <summary>
        /// This method will update the icons, enable/disable and show/hide the photo/video buttons depending on the current state of the app and the capabilities of the device
        /// </summary>
        private void UpdateCaptureControls()
        {
            // The buttons should only be enabled if the preview started sucessfully
            PhotoButton.IsEnabled = _isPreviewing;
            VideoButton.IsEnabled = _isPreviewing;

            // Update recording button to show "Stop" icon instead of red "Record" icon
            StartRecordingIcon.Visibility = _isRecording ? Visibility.Collapsed : Visibility.Visible;
            StopRecordingIcon.Visibility = _isRecording ? Visibility.Visible : Visibility.Collapsed;

            // If the camera doesn't support simultaneosly taking pictures and recording video, disable the photo button on record
            if (_isInitialized && !_mediaCapture.MediaCaptureSettings.ConcurrentRecordAndPhotoSupported)
            {
                PhotoButton.IsEnabled = !_isRecording;

                // Make the button invisible if it's disabled, so it's obvious it cannot be interacted with
                PhotoButton.Opacity = PhotoButton.IsEnabled ? 1 : 0;
            }
        }

        /// <summary>
        /// Registers event handlers for hardware buttons and orientation sensors, and performs an initial update of the UI rotation
        /// </summary>
        private void RegisterEventHandlers()
        {
        }

        /// <summary>
        /// Unregisters event handlers for hardware buttons and orientation sensors
        /// </summary>
        private void UnregisterEventHandlers()
        {
            
        }

        /// <summary>
        /// Attempts to find and return a device mounted on the panel specified, and on failure to find one it will return the first device listed
        /// </summary>
        /// <param name="desiredPanel">The desired panel on which the returned device should be mounted, if available</param>
        /// <returns></returns>
        private static async Task<DeviceInformation> FindCameraDeviceByPanelAsync(Windows.Devices.Enumeration.Panel desiredPanel)
        {
            // Get available devices for capturing pictures
            var allVideoDevices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);

            // Get the desired camera by panel
            DeviceInformation desiredDevice = allVideoDevices.FirstOrDefault(x => x.EnclosureLocation != null && x.EnclosureLocation.Panel == desiredPanel);

            // If there is no device mounted on the desired panel, return the first device found
            return desiredDevice ?? allVideoDevices.FirstOrDefault();
        }

        /// <summary>
        /// Applies the given orientation to a photo stream and saves it as a StorageFile
        /// </summary>
        /// <param name="stream">The photo stream</param>
        /// <param name="file">The StorageFile in which the photo stream will be saved</param>
        /// <param name="photoOrientation">The orientation metadata to apply to the photo</param>
        /// <returns></returns>
        private static async Task ReencodeAndSavePhotoAsync(IRandomAccessStream stream, StorageFile file, PhotoOrientation photoOrientation)
        {
            using (var inputStream = stream)
            {
                var decoder = await BitmapDecoder.CreateAsync(inputStream);

                using (var outputStream = await file.OpenAsync(FileAccessMode.ReadWrite))
                {
                    var encoder = await BitmapEncoder.CreateForTranscodingAsync(outputStream, decoder);

                    var properties = new BitmapPropertySet { { "System.Photo.Orientation", new BitmapTypedValue(photoOrientation, PropertyType.UInt16) } };
                    Debug.WriteLine($"Taken in orientation:{photoOrientation}");
                    await encoder.BitmapProperties.SetPropertiesAsync(properties);
                    //encoder.BitmapTransform.Rotation = AdjustImageRotation(photoOrientation);
                    await encoder.FlushAsync();
                }
            }
        }
        /// <summary>
        /// Used to deal with case when application reads image without using meta data, will manually save the photo in its taken orientation to file
        /// https://stackoverflow.com/questions/39306215/image-rotated-when-saved
        /// </summary>
        private static BitmapRotation AdjustImageRotation(PhotoOrientation photoOrientation) {
            switch (photoOrientation) {
                case PhotoOrientation.Rotate90:
                    return BitmapRotation.Clockwise270Degrees;
                case PhotoOrientation.Rotate180:
                    return BitmapRotation.Clockwise180Degrees;
                case PhotoOrientation.Rotate270:
                    return BitmapRotation.Clockwise90Degrees;
                default:
                    return BitmapRotation.None;
            }
        }
        #endregion Helper functions

    }

}