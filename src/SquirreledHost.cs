using System;
using System.Reflection;
using Topshelf.HostConfigurators;
using Topshelf.Squirrel.Windows.Builders;
using Topshelf.Squirrel.Windows.Interfaces;

namespace Topshelf.Squirrel.Windows
{
	public class SquirreledHost
	{
		private readonly string serviceName;
		private readonly string serviceDisplayName;
		private readonly bool withOverlapping;
		private readonly bool promtCredsWhileInstall;
		private readonly ISelfUpdatableService selfUpdatableService;
		private readonly IUpdater updater;

		public SquirreledHost(
			ISelfUpdatableService selfUpdatableService, 
			string serviceName = null,
			string serviceDisplayName = null, IUpdater updater = null, bool withOverlapping = false, bool promtCredsWhileInstall = false)
		{
			var assemblyName = Assembly.GetEntryAssembly().GetName().Name;

			this.serviceName = serviceName ?? assemblyName;
			this.serviceDisplayName = serviceDisplayName ?? assemblyName;
			this.selfUpdatableService = selfUpdatableService;
			this.withOverlapping = withOverlapping;
			this.promtCredsWhileInstall = promtCredsWhileInstall;
			this.updater = updater;
		}

		public void ConfigureAndRun(ConfigureExt configureExt = null)
		{
			HostFactory.Run(configurator => { Configure(configurator); configureExt?.Invoke(configurator); });
		}

		public delegate void ConfigureExt(HostConfigurator config);

		private void Configure(HostConfigurator config)
		{
			config.Service<ISelfUpdatableService>(service =>
			{
				service.ConstructUsing(settings => selfUpdatableService);

				service.WhenStarted((s, hostControl) =>
				{
					s.Start();
					return true;
				});

				service.AfterStartingService(() => { updater?.Start(); });

				service.WhenStopped(s => { s.Stop(); });
			});

			config.SetServiceName(serviceName);
			config.SetDisplayName(serviceDisplayName);
			config.StartAutomatically();
			config.EnableShutdown();

			if (promtCredsWhileInstall)
			{
				config.RunAsFirstPrompt();
			}
			else
			{
				config.RunAsLocalSystem();
			}

			config.AddCommandLineSwitch("squirrel", _ => { });
			config.AddCommandLineDefinition("firstrun", _ => Environment.Exit(0));
			config.AddCommandLineDefinition("obsolete", _ => Environment.Exit(0));
			config.AddCommandLineDefinition("updated", version => { config.UseHostBuilder((env, settings) => new UpdateHostBuilder(env, settings, version, withOverlapping)); });
			config.AddCommandLineDefinition("install", version => { config.UseHostBuilder((env, settings) => new InstallAndStartHostBuilder(env, settings, version)); });
			config.AddCommandLineDefinition("uninstall", _ => { config.UseHostBuilder((env, settings) => new StopAndUninstallHostBuilder(env, settings)); });
		}
	}
}