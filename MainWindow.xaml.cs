using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Graphics.Capture;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Media.Core;
using Windows.Media.Transcoding;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Composition;
using Composition.WindowsRuntimeHelpers;
using System.Windows.Interop;
using System.Numerics;

namespace recordingCs
{
	/// <summary>
	/// MainWindow.xaml の相互作用ロジック
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			
		}

		private async void StartButton_Click(object sender, RoutedEventArgs e)
		{
			var hwnd = new IntPtr(0x00000000000404F8);
			//var hwnd = new IntPtr(0x0000000000DE0FF6);
			try
			{
				recorder = ScreenRecorder.Create(hwnd);
				recorder.CaptureFinished += Recorder_CaptureFinished;
				await recorder.StartAsync();
			}
			catch (Exception)
			{
				recorder?.Dispose();
				recorder = null;
			}
		}

		private async void Recorder_CaptureFinished(ScreenRecorder obj)
		{
			try
			{
				await recorder.SaveAsync("test", TimeSpan.FromSeconds(5));
			}
			catch(Exception)
			{

			}

			recorder.Dispose();
			recorder = null;
		}

		private void StopButton_Click(object sender, RoutedEventArgs e)
		{
			if (recorder != null)
			{
				recorder.Stop();
			}
		}

		private ScreenRecorder recorder = null;
	}
}
