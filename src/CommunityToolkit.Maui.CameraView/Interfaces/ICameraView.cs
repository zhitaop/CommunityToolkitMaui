﻿using CommunityToolkit.Maui.Core.Primitives;

namespace CommunityToolkit.Maui.Core;

/// <summary>
/// Represents a visual element that provides the ability to show a camera preview and capture images.
/// </summary>
public interface ICameraView : IView, IAvailability
{
	/// <summary>
	/// Gets the <see cref="CameraFlashMode"/>.
	/// </summary>
	CameraFlashMode CameraFlashMode { get; }
	
	/// <summary>
	/// Gets or sets the resolution at which the camera will capture images.
	/// </summary>
	Size CaptureResolution { get; }

	/// <summary>
	/// Gets a value indicating whether the torch is on.
	/// </summary>
	bool IsTorchOn { get; }
	
    /// <summary>
    /// Gets or sets the currently selected camera.
    /// </summary>
    /// <remarks>
    /// This property will be <c>null</c> if no camera is selected.
    /// </remarks>
    CameraInfo? SelectedCamera { get; internal set; }

    /// <summary>
    /// Gets or sets the current zoom factor of the camera.
    /// </summary>
    /// <remarks>
    /// If a camera is selected and the zoom factor is set to a value outside the range of the camera's supported zoom factors,
    /// the value will be coerced to the nearest supported zoom factor.
    /// If no camera is selected, the value will be set as-is.
    /// </remarks>
    float ZoomFactor { get; internal set; }

    /// <summary>
    /// Occurs when an image is captured by the camera.
    /// </summary>
    /// <param name="imageData">The image data held within a <see cref="Stream"/>.</param>
    void OnMediaCaptured(Stream imageData);

    /// <summary>
    /// Occurs when an image capture fails.
    /// </summary>
    void OnMediaCapturedFailed();

    /// <summary>
    /// Triggers the camera to capture an image.
    /// </summary>
    /// <remarks>
    /// To customize the behavior of the camera when capturing an image, consider overriding the behavior through
    /// <c>CameraViewHandler.CommandMapper.ReplaceMapping(nameof(ICameraView.Shutter), ADD YOUR METHOD);</c>.
    /// </remarks>
    void Shutter();

    /// <summary>
    /// Starts the camera preview.
    /// </summary>
    /// <remarks>
    /// To customize the behavior of starting the camera preview, consider overriding the behavior through
    /// <c>CameraViewHandler.CommandMapper.ReplaceMapping(nameof(ICameraView.Start), ADD YOUR METHOD);</c>.
    /// </remarks>
    void Start();

    /// <summary>
    /// Stops the camera preview.
    /// </summary>
    /// <remarks>
    /// To customize the behavior of stopping the camera preview, consider overriding the behavior through
    /// <c>CameraViewHandler.CommandMapper.ReplaceMapping(nameof(ICameraView.Stop), ADD YOUR METHOD);</c>.
    /// </remarks>
    void Stop();
}
