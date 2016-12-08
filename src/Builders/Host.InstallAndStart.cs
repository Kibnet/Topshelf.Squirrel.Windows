using System;
using System.ServiceProcess;
using Topshelf.Builders;
using Topshelf.Runtime;
using Topshelf.Runtime.Windows;

namespace Topshelf.Squirrel.Windows.Builders
{
	public sealed class InstallAndStartHostBuilder : HostBuilder
	{
		private readonly InstallBuilder _installBuilder;
		private readonly StartBuilder _startBuilder;
		private string username;
		private string password;
		private ServiceAccount serviceAccount;

		public HostEnvironment Environment { get; }
		public HostSettings Settings { get; }

		public InstallAndStartHostBuilder(HostEnvironment environment, HostSettings settings, string version)
		{
			Environment = environment;
			Settings = new WindowsHostSettings
			{
				Name = $"{settings.Name}-{version}",
				DisplayName = $"{settings.DisplayName} ({version})",
				Description = settings.Description,
				InstanceName = settings.InstanceName,
				CanPauseAndContinue = settings.CanPauseAndContinue,
				CanSessionChanged = settings.CanSessionChanged,
				CanShutdown = settings.CanShutdown,
			};

			_installBuilder = new InstallBuilder(Environment, Settings);
			_installBuilder.Sudo();

			_startBuilder = new StartBuilder(_installBuilder);
		}

		public Topshelf.Host Build(ServiceBuilder serviceBuilder)
		{
			return _startBuilder.Build(serviceBuilder);
		}

		public void Match<T>(Action<T> callback) where T : class, HostBuilder
		{
			_installBuilder.Match(callback);
			_startBuilder.Match(callback);
		}
	}
}