using System;
using System.Diagnostics;
using System.Linq;
using System.Management;
using Topshelf.Builders;
using Topshelf.Runtime;
using Topshelf.Runtime.Windows;

namespace Topshelf.Squirrel.Windows.Builders
{
	public sealed class UpdateHostBuilder : HostBuilder
	{
		private readonly StopBuilder _stopOldHostBuilder;
		private readonly StartBuilder _startOldHostBuilder;
		private readonly UninstallBuilder _uninstallOldHostBuilder;
		private readonly InstallAndStartHostBuilder _installAndStartNewHostBuilder;
		private readonly StopAndUninstallHostBuilder _stopAndUninstallNewHostBuilder;

		public HostEnvironment Environment { get; }
		public HostSettings Settings { get; }

		public bool WithOverlapping { get; }

		public UpdateHostBuilder(HostEnvironment environment, HostSettings settings, string version,
			bool withOverlapping = false)
		{
			Environment = environment;
			Settings = settings;
			WithOverlapping = withOverlapping;
			var currentService = GetLastWmiServiceInfo(settings.Name, $"{settings.Name}-{version}");

			var OldSettings = new WindowsHostSettings
			{
				Name = Convert.ToString(currentService["Name"].Value),
			};

			_stopOldHostBuilder = new StopBuilder(Environment, OldSettings);
			_startOldHostBuilder = new StartBuilder(_stopOldHostBuilder);
			_uninstallOldHostBuilder = new UninstallBuilder(Environment, OldSettings);
			_installAndStartNewHostBuilder = new InstallAndStartHostBuilder(Environment, Settings, version);
			_stopAndUninstallNewHostBuilder = new StopAndUninstallHostBuilder(Environment, Settings, version);
		}

		private static PropertyDataCollection GetLastWmiServiceInfo(string serviceNamePattern, string currentName)
		{
			var searcher =
				new ManagementObjectSearcher(
					$@"SELECT * FROM Win32_Service WHERE (Name='{serviceNamePattern}' or Name like '%{serviceNamePattern}[^0-9a-z]%' and startmode!='disabled') and Name != '{currentName}'");
			var collection = searcher.Get();
			if (collection.Count == 0)
			{
				return null;
			}

			var managementBaseObject = collection.Cast<ManagementBaseObject>().Last();
			return managementBaseObject.Properties;
		}

		public Host Build(ServiceBuilder serviceBuilder)
		{
			return new UpdateHost(_installAndStartNewHostBuilder.Build(serviceBuilder),
				_stopOldHostBuilder.Build(serviceBuilder),
				_uninstallOldHostBuilder.Build(serviceBuilder),
				_stopAndUninstallNewHostBuilder.Build(serviceBuilder),
				_startOldHostBuilder.Build(serviceBuilder), WithOverlapping);
		}

		public void Match<T>(Action<T> callback) where T : class, HostBuilder
		{
			_installAndStartNewHostBuilder.Match(callback);
			_stopOldHostBuilder.Match(callback);
			_uninstallOldHostBuilder.Match(callback);
			_stopAndUninstallNewHostBuilder.Match(callback);
			_startOldHostBuilder.Match(callback);
		}

		private sealed class UpdateHost : Host
		{
			private readonly Host _stopOldHost;
			private readonly Host _startOldHost;
			private readonly Host _uninstallOldHost;
			private readonly Host _installAndStartNewHost;
			private readonly Host _stopAndUninstallNewHost;

			private readonly bool _withOverlapping;

			public UpdateHost(Host installAndStartNewHost, Host stopOldHost, Host uninstallOldHost,
				Host stopAndUninstallNewHost, Host startOldHost, bool withOverlapping = false)
			{
				_installAndStartNewHost = installAndStartNewHost;
				_stopOldHost = stopOldHost;
				_uninstallOldHost = uninstallOldHost;
				_stopAndUninstallNewHost = stopAndUninstallNewHost;
				_startOldHost = startOldHost;
				_withOverlapping = withOverlapping;
			}

			public TopshelfExitCode Run()
			{
				Trace.TraceInformation("Update {0} Overlapping", _withOverlapping ? "with" : "without");
				var exitCode = TopshelfExitCode.Ok;
				if (!_withOverlapping)
				{
					exitCode = _stopOldHost.Run();
					Trace.TraceInformation("Service was self-stopped");
				}
				if (exitCode == TopshelfExitCode.Ok)
				{
					exitCode = _installAndStartNewHost.Run();
					if (exitCode == TopshelfExitCode.Ok)
					{
						Trace.TraceInformation("Started new version");
						if (_withOverlapping)
							_stopOldHost.Run();
						exitCode = _uninstallOldHost.Run();
						Trace.TraceInformation("The update has been successfully completed");
					}
					else
					{
						Trace.TraceInformation("Not started new version");
						if (!_withOverlapping)
							exitCode = _startOldHost.Run();
						exitCode = _stopAndUninstallNewHost.Run();
						Trace.TraceWarning("During the update failed and was rolled back.");
					}
				}

				return exitCode;
			}
		}
	}
}