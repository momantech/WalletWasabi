using Avalonia.Media.Imaging;
using System.Threading.Tasks;
using OpenCvSharp;
using Avalonia;
using Avalonia.Media;
using System.Runtime.InteropServices;
using WalletWasabi.Logging;
using WalletWasabi.Userfacing;
using NBitcoin;
using Nito.AsyncEx;
using Avalonia.Platform;
using WalletWasabi.Fluent.Models.Windows;
using Avalonia.Controls;
using Microsoft.Extensions.Hosting;
using System.Threading;

namespace WalletWasabi.Fluent.Models;

public class WebcamQrReader : BackgroundService
{
	private const byte DefaultCameraId = 0;

	/// <summary>Whether user requested to stop webcamera to scan for QR codes.</summary>
	private volatile bool _requestEnd;

	public WebcamQrReader(Network network)
	{
		Network = network;
	}

	public event EventHandler<Bitmap>? NewImageArrived;

	public event EventHandler<string>? CorrectAddressFound;

	public event EventHandler<string>? InvalidAddressFound;

	public event EventHandler<Exception>? ErrorOccurred;

	private AsyncLock ScanningTaskLock { get; } = new();
	private Network Network { get; }
	private Task? ScanningTask { get; set; }
	public static bool IsOsPlatformSupported => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

	public async Task StartScanningAsync()
	{
		using (await ScanningTaskLock.LockAsync().ConfigureAwait(false))
		{
			if (ScanningTask is { })
			{
				return;
			}
			_requestEnd = false;
			ScanningTask = Task.Run(() =>
			{
				WindowsCapture? camera = null;
				try
				{
					string[] devices = WindowsCapture.FindDevices();
					if (devices.Length == 0)
					{
						return;
					}

					WindowsCapture.VideoFormat[] formats = WindowsCapture.GetVideoFormat(DefaultCameraId);

					camera = new WindowsCapture(DefaultCameraId, formats[0]);
					camera.Start();
					//VideoCapture? camera = null;
					//try
					//{
					//	if (!IsOsPlatformSupported)
					//	{
					//		throw new NotImplementedException("This operating system is not supported.");
					//	}
					//	camera = new();
					//	camera.SetExceptionMode(true);
					//	// Setting VideoCaptureAPI to DirectShow, to remove warning logs,
					//	// might need to be changed in the future for other operating systems
					//	if (!camera.Open(DefaultCameraId, VideoCaptureAPIs.DSHOW))
					//	{
					//		throw new InvalidOperationException("Could not open webcamera.");
					//	}
					KeepScanning(camera);
				}
				catch (OpenCVException ex)
				{
					Logger.LogError("Could not open camera. Reason: " + ex);
					ErrorOccurred?.Invoke(this, new NotSupportedException("Could not open camera."));
				}
				catch (Exception ex)
				{
					Logger.LogError("QR scanning stopped. Reason:", ex);
					ErrorOccurred?.Invoke(this, ex);
				}
				finally
				{
					camera?.Release();
				}
			});
		}
	}

	public async Task StopScanningAsync()
	{
		using (await ScanningTaskLock.LockAsync().ConfigureAwait(false))
		{
			if (ScanningTask is { } task)
			{
				_requestEnd = true;
				await task;

				ScanningTask = null;
			}
		}
	}

	private void KeepScanning(WindowsCapture camera)
	{
		//PixelSize pixelSize = new((int)camera.Size.Width, (int)camera.Size.Height);
		//Vector dpi = new(96, 96);
		//WriteableBitmap writeableBitmap = new(pixelSize, dpi, PixelFormat.Rgba8888, AlphaFormat.Unpremul);

		//int dataSize = (int)(camera.Size.Width * camera.Size.Height);
		//int[] helperArray = new int[dataSize];

		// Immediately after starting the USB camera,
		// GetBitmap() fails because image buffer is not prepared yet.
		var bmp = camera.GetBitmap();
		while (!_requestEnd)
		{
			try
			{
				//writeableBitmap = (WriteableBitmap)camera.GetBitmap();

				//if (!gotBackFrame || frame.Width == 0 || frame.Height == 0)
				//{
				//	continue;
				//}
				NewImageArrived?.Invoke(this, camera.GetBitmap());
				//string decodedText = qRCodeDetector.Decode(frame, points, tmpMat);
				//if (string.IsNullOrWhiteSpace(decodedText))
				//{
				//	continue;
				//}
				//if (AddressStringParser.TryParse(decodedText, Network, out _))
				//{
				//	CorrectAddressFound?.Invoke(this, decodedText);
				//	break;
				//}
				//else
				//{
				//	InvalidAddressFound?.Invoke(this, decodedText);
				//}
			}
			catch (Exception)
			{
				throw new InvalidOperationException("Could not read frames. Please make sure no other program uses your camera.");
			}
		}
	}

	protected override Task ExecuteAsync(CancellationToken stoppingToken)
	{
		ScanningTask = Task.Run(() =>
		{
			WindowsCapture? camera = null;
			try
			{
				string[] devices = WindowsCapture.FindDevices();
				if (devices.Length == 0)
				{
					ErrorOccurred?.Invoke(this, new NotSupportedException("Could not open camera."));
				}

				WindowsCapture.VideoFormat[] formats = WindowsCapture.GetVideoFormat(DefaultCameraId);

				camera = new WindowsCapture(DefaultCameraId, formats[0]);
				camera.Start();

				var bmp = camera.GetBitmap();
				ulong cnt = 0;
				while (!stoppingToken.IsCancellationRequested)
				{
					cnt++;
					if (cnt % 1000 == 0)
					{
						Logger.LogInfo("Pic taken");
						NewImageArrived?.Invoke(this, camera.GetBitmap());
					}
				}
			}
			catch (Exception)
			{
				var ex = new InvalidOperationException("Could not read frames. Please make sure no other program uses your camera.");
				ErrorOccurred?.Invoke(this, ex);
			}
			finally
			{
				camera?.Release();
			}
		}, stoppingToken);
		return Task.CompletedTask;
	}
}
