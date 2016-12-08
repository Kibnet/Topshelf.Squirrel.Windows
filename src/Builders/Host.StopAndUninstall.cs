using System;
using System.Diagnostics;
using System.Linq;
using System.Management;
using Topshelf.Builders;
using Topshelf.Runtime;
using Topshelf.Runtime.Windows;

namespace Topshelf.Squirrel.Windows.Builders
{
	public sealed class StopAndUninstallHostBuilder : HostBuilder
	{
		private readonly StopBuilder _stopBuilder;
		private readonly UninstallBuilder _uninstallBuilder;
		private readonly int _processId;

		public HostEnvironment Environment { get; }
		public HostSettings Settings { get; }

		public StopAndUninstallHostBuilder(HostEnvironment environment, HostSettings settings, string version = null)
		{
			Environment = environment;

			var serviceName = settings.Name;
			if (version != null)
			{
				serviceName = $"{serviceName.Split('-')[0]}-{version}";
			}

			var serviceInfo = GetWmiServiceInfo(serviceName);
			if (serviceInfo != null)
			{
				serviceName = Convert.ToString(serviceInfo["Name"].Value);
				_processId = Convert.ToInt32(serviceInfo["ProcessId"].Value);
			}

			Settings = new WindowsHostSettings
			{
				Name = serviceName,

				// squirrel hook wait only 15 seconds
				// so we can't wait for service stop more that 5 seconds
				StopTimeOut = TimeSpan.FromSeconds(5),
			};

			_stopBuilder = new StopBuilder(Environment, Settings);
			_uninstallBuilder = new UninstallBuilder(Environment, Settings);
			_uninstallBuilder.Sudo();
		}

		public Host Build(ServiceBuilder serviceBuilder)
		{
			return new StopAndUninstallHost(_stopBuilder.Build(serviceBuilder), _uninstallBuilder.Build(serviceBuilder), _processId);
		}

		public void Match<T>(Action<T> callback) where T : class, HostBuilder
		{
			_stopBuilder.Match(callback);
			_uninstallBuilder.Match(callback);
		}

		private static PropertyDataCollection GetWmiServiceInfo(string serviceNamePattern)
		{
			var searcher = new ManagementObjectSearcher($@"SELECT * FROM Win32_Service WHERE Name='{serviceNamePattern}' or Name like '%{serviceNamePattern}[^0-9a-z]%' and startmode!='disabled'");
			var collection = searcher.Get();
			if (collection.Count == 0)
			{
				return null;
			}

			var managementBaseObject = collection.Cast<ManagementBaseObject>().Last();
			return managementBaseObject.Properties;
		}

		private sealed class StopAndUninstallHost : Host
		{
			private readonly Host _stopHost;
			private readonly Host _uninstallHost;
			private readonly int _processId;

			public StopAndUninstallHost(Host stopHost, Host uninstallHost, int processId)
			{
				_stopHost = stopHost;
				_uninstallHost = uninstallHost;
				_processId = processId;
			}

			public TopshelfExitCode Run()
			{
				var exitCode = _stopHost.Run();

				if (exitCode == TopshelfExitCode.ServiceNotInstalled)
				{
					return TopshelfExitCode.Ok;
				}

				if (exitCode == TopshelfExitCode.StopServiceFailed)
				{
					Process.GetProcessById(_processId).Kill();
					exitCode = TopshelfExitCode.Ok;
				}

				if (exitCode == TopshelfExitCode.Ok)
				{
					exitCode = _uninstallHost.Run();
				}

				return exitCode;
			}
		}
	}
}
