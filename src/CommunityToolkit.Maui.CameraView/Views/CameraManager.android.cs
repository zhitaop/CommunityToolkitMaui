﻿using Java.Lang;
using Java.Util.Concurrent;
using Android.Content;
using AndroidX.Core.Content;
using AndroidX.Lifecycle;
using AndroidX.Camera.Core;
using AndroidX.Camera.Lifecycle;
using AndroidX.Camera.View;
using AndroidX.Camera.Core.Impl.Utils.Futures;
using static Android.Media.Image;
using AndroidX.Camera.Core.ResolutionSelector;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Core.Primitives;

namespace CommunityToolkit.Maui.Core.Views;

public partial class CameraManager
{
    readonly Context context = mauiContext.Context ?? throw new InvalidOperationException("Invalid context");
	
    NativePlatformCameraPreviewView? previewView;
    IExecutorService? cameraExecutor;
    ProcessCameraProvider? processCameraProvider;
    ImageCapture? imageCapture;
    ImageCallBack? imageCallback;
    ICamera? camera;
    ICameraControl? cameraControl;
    Preview? cameraPreview;
    ResolutionSelector? resolutionSelector;
    ResolutionFilter? resolutionFilter;

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

    // IN the future change the return type to be an alias
    public NativePlatformCameraPreviewView CreatePlatformView()
    {
        imageCallback = new ImageCallBack(cameraView);
        previewView = new NativePlatformCameraPreviewView(context);
        if (NativePlatformCameraPreviewView.ScaleType.FitCenter is not null)
        {
            previewView.SetScaleType(NativePlatformCameraPreviewView.ScaleType.FitCenter);
        }
        cameraExecutor = Executors.NewSingleThreadExecutor() ?? throw new NullReferenceException();

        return previewView;
    }
	
	protected virtual void Dispose(bool disposing)
	{
		if (disposing)
		{
			camera?.Dispose();
			camera = null;
			
			cameraControl?.Dispose();
			cameraControl = null;
			
			cameraPreview?.Dispose();
			cameraPreview = null;
			
			cameraExecutor?.Dispose();
			cameraExecutor = null;
			
			imageCapture?.Dispose();
			imageCapture = null;
			
			imageCallback?.Dispose();
			imageCallback = null;
			
			previewView?.Dispose();
			previewView = null;
			
			processCameraProvider?.Dispose();
			processCameraProvider = null;
			
			resolutionSelector?.Dispose();
			resolutionSelector = null;
			
			resolutionFilter?.Dispose();
			resolutionFilter = null;
		}
	}

    protected virtual partial void PlatformConnect()
    {
        var cameraProviderFuture = ProcessCameraProvider.GetInstance(context);
        if (previewView is null)
        {
            return;
        }

        cameraProviderFuture.AddListener(new Runnable(() =>
        {
            processCameraProvider = (ProcessCameraProvider)(cameraProviderFuture.Get() ?? throw new NullReferenceException());

            if (cameraProvider.AvailableCameras.Count < 1)
            {
                throw new InvalidOperationException("There's no camera available on your device.");
            }

            StartUseCase();

        }), ContextCompat.GetMainExecutor(context));
    }

    protected void StartUseCase()
    {
        if (resolutionSelector is null)
        {
            return;
        }

        PlatformStop();

        cameraPreview?.Dispose();
        imageCapture?.Dispose();

        cameraPreview = new Preview.Builder().SetResolutionSelector(resolutionSelector).Build();
        cameraPreview.SetSurfaceProvider(previewView?.SurfaceProvider);

        imageCapture = new ImageCapture.Builder()
        .SetCaptureMode(ImageCapture.CaptureModeMaximizeQuality)
        .SetResolutionSelector(resolutionSelector)
        .Build();

        PlatformStart();
    }

    protected virtual partial void PlatformStart()
    {
        if (currentCamera is null || previewView is null || processCameraProvider is null || cameraPreview is null || imageCapture is null)
        {
            return;
        }

        var cameraSelector = currentCamera.CameraSelector ?? throw new NullReferenceException();

        var owner = (ILifecycleOwner)context;
        camera = processCameraProvider.BindToLifecycle(owner, cameraSelector, cameraPreview, imageCapture);

        cameraControl = camera.CameraControl;

        //start the camera with AutoFocus
        MeteringPoint point = previewView.MeteringPointFactory.CreatePoint(previewView.Width / 2, previewView.Height / 2, 0.1F);
        FocusMeteringAction action = new FocusMeteringAction.Builder(point)
                                                            .DisableAutoCancel()
                                                            .Build();
        camera.CameraControl.StartFocusAndMetering(action);

        IsInitialized = true;
        OnLoaded?.Invoke();
    }

    protected virtual partial void PlatformStop()
    {
        if (processCameraProvider is null)
        {
            return;
        }

        processCameraProvider.UnbindAll();
        IsInitialized = false;
    }

    protected virtual partial void PlatformDisconnect()
    {

    }

    protected virtual partial void PlatformTakePicture()
    {
        imageCapture?.TakePicture(cameraExecutor!, imageCallback!);
    }

    public partial void UpdateFlashMode(CameraFlashMode flashMode)
    {
        if (imageCapture is null)
        {
            return;
        }

        imageCapture.FlashMode = flashMode.ToPlatform();
    }

    public partial void UpdateZoom(float zoomLevel)
    {
        if (cameraControl is null)
        {
            return;
        }

        cameraControl.SetZoomRatio(zoomLevel);
    }

    public partial void UpdateCaptureResolution(Size resolution)
    {
        if (resolutionFilter?.TargetSize.Width == resolution.Width && resolutionFilter?.TargetSize.Height == resolution.Height)
        {
            return;
        }

        var targetSize = new Android.Util.Size((int)resolution.Width, (int)resolution.Height);

        if (resolutionFilter is null)
        {
            resolutionFilter = new ResolutionFilter(targetSize);
        }
        else
        {
            resolutionFilter.TargetSize = targetSize;
        }

        resolutionSelector?.Dispose();

        resolutionSelector = new ResolutionSelector.Builder()
        .SetAllowedResolutionMode(ResolutionSelector.PreferHigherResolutionOverCaptureRate)
        .SetResolutionFilter(resolutionFilter)
        .Build();

        if (IsInitialized)
        {
            StartUseCase();
        }
    }

    sealed class FutureCallback(Action<Java.Lang.Object?> action, Action<Throwable?> failure) : Java.Lang.Object, IFutureCallback
	{
		public void OnSuccess(Java.Lang.Object? value)
        {
            action.Invoke(value);
        }

        public void OnFailure(Throwable? throwable)
        {
            failure.Invoke(throwable);
        }
    }

    sealed class ImageCallBack(ICameraView cameraView) : ImageCapture.OnImageCapturedCallback
	{
		public override void OnCaptureSuccess(IImageProxy image)
        {
            base.OnCaptureSuccess(image);
            var img = image.Image;

            if (img is null)
            {
                return;
            }

            var buffer = GetFirstPlane(img.GetPlanes())?.Buffer;

            if (buffer is null)
            {
                image.Close();
                return;
            }

            var imgData = new byte[buffer.Capacity()];
            try
            {
                buffer.Get(imgData);
                var memStream = new MemoryStream(imgData);
                cameraView.OnMediaCaptured(memStream);
            }
            finally
            {
                image.Close();
            }

            static Plane? GetFirstPlane(Plane[]? planes)
            {
                if (planes is null || planes.Length is 0)
                {
                    return null;
                }

                return planes[0];
            }
        }

        public override void OnError(ImageCaptureException exception)
        {
            base.OnError(exception);
            cameraView.OnMediaCapturedFailed();
        }
    }
}

public class ResolutionFilter : Java.Lang.Object, IResolutionFilter
{
    public Android.Util.Size TargetSize { get; set; }

    public ResolutionFilter(Android.Util.Size size)
    {
        TargetSize = size;
    }

    public IList<Android.Util.Size> Filter(IList<Android.Util.Size> supportedSizes, int rotationDegrees)
    {
        var filteredList = supportedSizes.Where(size => size.Width <= TargetSize.Width && size.Height <= TargetSize.Height)
            .OrderByDescending(size => size.Width * size.Height);

        if (!filteredList.Any())
        {
            return supportedSizes;
        }

        return filteredList.ToList();
    }
}

public class Observer : Java.Lang.Object, IObserver
{
    Action<Java.Lang.Object?> observerAction = (Java.Lang.Object? o) => { };

    public Observer(Action<Java.Lang.Object?> action)
    {
        observerAction = action;
    }

    public void OnChanged(Java.Lang.Object? value)
    {
        observerAction?.Invoke(value);
    }
}
