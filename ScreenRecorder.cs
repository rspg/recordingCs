using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Media.Core;
using Windows.Media.Transcoding;
using Windows.Storage;
using Windows.Storage.Streams;
using Composition.WindowsRuntimeHelpers;

namespace recordingCs
{
	public class ScreenRecorder : IDisposable
	{
		public static ScreenRecorder Create(IntPtr hwnd)
		{
			var instance = new ScreenRecorder();
			if (instance.Initialize(hwnd))
				return instance;
			return null;
		}

		private bool Initialize(IntPtr hwnd)
		{
			captureItem = CaptureHelper.CreateItemForWindow(hwnd);
			captureItem.Closed += CaptureItem_Closed;

			direct3dDevice = Direct3D11Helper.CreateDevice();
			framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(direct3dDevice, Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, captureItem.Size);
			framePool.FrameArrived += FramePool_FrameArrived;

			var sourceDescripter = new VideoStreamDescriptor(
				VideoEncodingProperties.CreateUncompressed("BGRA8", (uint)captureItem.Size.Width, (uint)captureItem.Size.Height));
			streamSource = new MediaStreamSource(sourceDescripter);
			streamSource.IsLive = true;
			streamSource.BufferTime = TimeSpan.Zero;
			streamSource.CanSeek = false;
			streamSource.SampleRequested += StreamSource_SampleRequested;

			status = Status.Ready;

			return true;
		}

		public async Task StartAsync()
		{
			if (status != Status.Ready)
				throw new System.InvalidOperationException("");
			status = Status.Recording;

			var tempolaryPath = Path.GetTempFileName();
			tempolaryFile = await StorageFile.GetFileFromPathAsync(tempolaryPath);
			using (var tempolaryStream = await tempolaryFile.OpenAsync(FileAccessMode.ReadWrite))
			{
				var transcorder = new MediaTranscoder();
				transcorder.HardwareAccelerationEnabled = true;
				var encordingProfile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD720p);
				var result = await transcorder.PrepareMediaStreamSourceTranscodeAsync(streamSource, tempolaryStream, encordingProfile);
				if (result.CanTranscode)
				{
					using (var session = StartCapture())
					{
						await result.TranscodeAsync();
					}
				}

				await tempolaryStream.FlushAsync();
			}

			status = Status.Finish;
		}

		public async Task StopAsync()
		{
			lock (captureLock)
			{
				if (status != Status.Recording)
					throw new System.InvalidOperationException("");
				status = Status.Close;

				SetSample(null);
			}

			while (status != Status.Finish)
				await Task.Delay(1);
		}

		public async Task SaveAsync(string fileName, TimeSpan duration)
		{
			if (status != Status.Finish)
				throw new System.InvalidOperationException("");

			var storageFolder = KnownFolders.AppCaptures;
			var destination = await storageFolder.CreateFileAsync(fileName + ".mp4", CreationCollisionOption.ReplaceExisting);

			var mediaClip = await MediaClip.CreateFromFileAsync(tempolaryFile);
			if (mediaClip.OriginalDuration > duration)
			{
				mediaClip.TrimTimeFromStart = mediaClip.OriginalDuration - duration;

				var composition = new MediaComposition();
				composition.Clips.Add(mediaClip);
				await composition.RenderToFileAsync(destination, MediaTrimmingPreference.Fast);
			}
			else
			{
				await tempolaryFile.CopyAndReplaceAsync(destination);
			}
		}

		public void Dispose()
		{
			framePool?.Dispose();
			direct3dDevice?.Dispose();
			tempolaryFile.DeleteAsync().AsTask().Wait();
		}

		private GraphicsCaptureSession StartCapture()
		{
			var captureSession = framePool.CreateCaptureSession(captureItem);
			captureSession.StartCapture();
			return captureSession;
		}

		private void SetSample(Direct3D11CaptureFrame frame)
		{
			lock (captureLock)
			{
				if (sampleRequest != null && sampleDeferral != null)
				{
					sampleRequest.Sample = frame == null ? null : MediaStreamSample.CreateFromDirect3D11Surface(frame.Surface, frame.SystemRelativeTime - recordingStartTime);
					sampleDeferral.Complete();
					sampleDeferral = null;
				}
			}
		}

		private async void CaptureItem_Closed(GraphicsCaptureItem sender, object args)
		{
			await StopAsync();
		}

		private void FramePool_FrameArrived(Direct3D11CaptureFramePool sender, object args)
		{
			lock (captureLock)
			{
				using (var frame = sender.TryGetNextFrame())
				{
					if(recordingStartTime == TimeSpan.Zero)
						recordingStartTime = frame.SystemRelativeTime;
					SetSample(frame);
				}
			}
		}

		private void StreamSource_SampleRequested(MediaStreamSource sender, MediaStreamSourceSampleRequestedEventArgs args)
		{
			lock (captureLock)
			{
				if (status == Status.Close)
				{
					args.Request.Sample = null;
				}
				else
				{
					sampleDeferral = args.Request.GetDeferral();
					sampleRequest = args.Request;
				}
			}
		}

		public enum Status
		{
			None, Ready, Recording, Close, Finish
		}

		private MediaStreamSource streamSource = null;
		private GraphicsCaptureItem captureItem = null;
		private Direct3D11CaptureFramePool framePool = null;
		private IDirect3DDevice direct3dDevice = null;
		private StorageFile tempolaryFile = null;
		private Status status = Status.None;

		private object captureLock = new object();
		private MediaStreamSourceSampleRequestDeferral sampleDeferral = null;
		private MediaStreamSourceSampleRequest sampleRequest = null;
		private TimeSpan recordingStartTime = TimeSpan.Zero;
	}
}
