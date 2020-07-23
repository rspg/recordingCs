using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Composition.WindowsRuntimeHelpers;
using Windows.System;

namespace recordingCs
{
	/// <summary>
	/// App.xaml の相互作用ロジック
	/// </summary>
	public partial class App : Application
	{
		public App()
		{
			_controller = CoreMessagingHelper.CreateDispatcherQueueControllerForCurrentThread();
		}

		private DispatcherQueueController _controller;
	}
}
