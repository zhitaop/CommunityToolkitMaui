﻿using System.Diagnostics;
using System.Runtime.InteropServices;
using AVFoundation;
using Foundation;
using UIKit;
using CoreMedia;
using CommunityToolkit.Maui.Core.Primitives;
using CommunityToolkit.Maui.Extensions;

namespace CommunityToolkit.Maui.Core.Views;

public partial class CameraManager
{
    AVCaptureSession? captureSession;
    AVCapturePhotoOutput? photoOutput;
    AVCaptureInput? captureInput;
    AVCaptureDevice? captureDevice;

    // TODO: Check if we really need this
    NSDictionary<NSString, NSObject> codecSettings = new NSDictionary<NSString, NSObject>(
        new[] { AVVideo.CodecKey }, new[] { (NSObject)new NSString("jpeg") });
    AVCaptureFlashMode flashMode;

    // IN the future change the return type to be an alias
    public UIView CreatePlatformView()
    {
        captureSession = new AVCaptureSession
        {
            SessionPreset = AVCaptureSession.PresetPhoto
        };

        var previewView = new PreviewView();
        previewView.Session = captureSession;

        return previewView;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual partial void PlatformConnect()
    {
        if (cameraProvider.AvailableCameras.Count < 1)
        {
            throw new InvalidOperationException("There's no camera available on your device.");
        }

        PlatformStart();
    }

    protected virtual partial void PlatformStart()
    {
        if (currentCamera is null || captureSession is null)
        {
            return;
        }

        captureSession.BeginConfiguration();

        foreach (var input in captureSession.Inputs)
        {
            captureSession.RemoveInput(input);
            input.Dispose();
        }

        captureDevice = currentCamera.CaptureDevice ?? throw new NullReferenceException();
        captureInput = new AVCaptureDeviceInput(captureDevice, out var err);
        captureSession.AddInput(captureInput);

        if (photoOutput is null)
        {
            photoOutput = new AVCapturePhotoOutput();
            captureSession.AddOutput(photoOutput);
        }

        UpdateCaptureResolution(cameraView.CaptureResolution);

        captureSession.CommitConfiguration();
        captureSession.StartRunning();
        Initialized = true;
        Loaded?.Invoke();
    }

    protected virtual partial void PlatformStop()
    {
        if (captureSession is null)
        {
            return;
        }

        if (captureSession.Running)
        {
            captureSession.StopRunning();
        }

        Initialized = false;
    }

    protected virtual partial void PlatformDisconnect()
    {
    }

    protected virtual async partial void PlatformTakePicture()
    {
        ArgumentNullException.ThrowIfNull(photoOutput);

        var capturePhotoSettings = AVCapturePhotoSettings.FromFormat(codecSettings);
        capturePhotoSettings.FlashMode = photoOutput.SupportedFlashModes.Contains(flashMode) ? flashMode : photoOutput.SupportedFlashModes.First();

        var wrapper = new AVCapturePhotoCaptureDelegateWrapper();

        photoOutput.CapturePhoto(capturePhotoSettings, wrapper);

        var result = await wrapper.Task;
        var data = result.Photo.FileDataRepresentation;

        if (data is null)
        {
            // TODO: Pass NSError information
            cameraView.OnMediaCapturedFailed();
            return;
        }

        var dataBytes = new byte[data.Length];
        Marshal.Copy(data.Bytes, dataBytes, 0, (int)data.Length);

        cameraView.OnMediaCaptured(new MemoryStream(dataBytes));
    }

    public partial void UpdateFlashMode(CameraFlashMode flashMode)
    {
        this.flashMode = flashMode.ToPlatform();
    }

    public partial void UpdateZoom(float zoomLevel)
    {
        if (!Initialized || captureDevice is null)
        {
            return;
        }

        if (zoomLevel < (float)captureDevice.MinAvailableVideoZoomFactor || zoomLevel > (float)captureDevice.MaxAvailableVideoZoomFactor)
        {
            return;
        }

        captureDevice.LockForConfiguration(out NSError error);
        if (error is not null)
        {
            Console.WriteLine(error);
            Debug.WriteLine(error);
            return;
        }

        captureDevice.VideoZoomFactor = zoomLevel;
        captureDevice.UnlockForConfiguration();
    }

    public partial void UpdateCaptureResolution(Size resolution)
    {
        if (captureDevice is null || currentCamera is null)
        {
            return;
        }

        captureDevice.LockForConfiguration(out NSError error);
        if (error is not null)
        {
            Console.WriteLine(error);

            Debug.WriteLine(error);
            return;
        }

        var filteredFormatList = currentCamera.formats.Where(f =>
        {
            var d = ((CMVideoFormatDescription)f.FormatDescription).Dimensions;
            return d.Width <= resolution.Width && d.Height <= resolution.Height;
        });

        filteredFormatList = (filteredFormatList.Any() ? filteredFormatList : currentCamera.formats)
            .OrderByDescending(f =>
            {
                var d = ((CMVideoFormatDescription)f.FormatDescription).Dimensions;
                return d.Width * d.Height;
            });

        if (filteredFormatList.Any())
        {
            captureDevice.ActiveFormat = filteredFormatList.First();
        }

        captureDevice.UnlockForConfiguration();
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            captureSession?.StopRunning();
            captureSession?.Dispose();
            captureInput?.Dispose();
            photoOutput?.Dispose();
        }
    }

    class AVCapturePhotoCaptureDelegateWrapper : AVCapturePhotoCaptureDelegate
    {
        readonly TaskCompletionSource<CapturePhotoResult> taskCompletionSource = new();

        public Task<CapturePhotoResult> Task =>
            taskCompletionSource.Task;

        public override void DidFinishProcessingPhoto(AVCapturePhotoOutput output, AVCapturePhoto photo, NSError? error)
        {
            taskCompletionSource.TrySetResult(new() { Output = output, Photo = photo, Error = error });
        }
    }

    record CapturePhotoResult
    {
        public required AVCapturePhotoOutput Output { get; init; }

        public required AVCapturePhoto Photo { get; init; }

        public NSError? Error { get; init; }
    }

    class PreviewView : UIView
    {
        public PreviewView()
        {
            PreviewLayer.VideoGravity = AVLayerVideoGravity.ResizeAspectFill;
        }

        public AVCaptureVideoPreviewLayer PreviewLayer => (AVCaptureVideoPreviewLayer)Layer;

        public AVCaptureSession? Session
        {
            get => PreviewLayer.Session;
            set => PreviewLayer.Session = value;
        }

        [Export("layerClass")]
        public static ObjCRuntime.Class GetLayerClass()
        {
            return new ObjCRuntime.Class(typeof(AVCaptureVideoPreviewLayer));
        }

        public override void LayoutSubviews()
        {
            base.LayoutSubviews();

            if (PreviewLayer?.Connection == null)
            {
                return;
            }

            PreviewLayer.Connection.VideoOrientation = UIDevice.CurrentDevice.Orientation switch
            {
                UIDeviceOrientation.Portrait => AVCaptureVideoOrientation.Portrait,
                UIDeviceOrientation.PortraitUpsideDown => AVCaptureVideoOrientation.PortraitUpsideDown,
                UIDeviceOrientation.LandscapeLeft => AVCaptureVideoOrientation.LandscapeRight,
                UIDeviceOrientation.LandscapeRight => AVCaptureVideoOrientation.LandscapeLeft,
                _ => PreviewLayer.Connection.VideoOrientation
            };
        }
    }

}
