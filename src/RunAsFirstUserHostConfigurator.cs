using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.DirectoryServices.AccountManagement;
using System.IO;
using System.Reflection;
using System.ServiceProcess;
using System.ServiceProcess.Design;
using Topshelf.Builders;
using Topshelf.Configurators;
using Topshelf.HostConfigurators;

namespace Topshelf.Squirrel.Windows
{
	public class RunAsFirstUserHostConfigurator : HostBuilderConfigurator
	{
		private const string creds = "Creds.txt";

		public string Password { get; private set; }

		public string Username { get; private set; }

		public HostBuilder Configure(HostBuilder builder)
		{
			if (builder == null)
				throw new ArgumentNullException("builder");
			builder.Match<InstallBuilder>((Action<InstallBuilder>) (x =>
			{
				bool valid = false;
				var path = new FileInfo(Assembly.GetExecutingAssembly().Location).Directory.Parent.FullName;
				var filename = Path.Combine(path, creds);
				if (File.Exists(filename))
				{
					try
					{
						var credlines = File.ReadAllLines(filename);
						Username = credlines[0];
						Password = credlines[1];
						valid = CheckCredentials(Username, Password);
					}
					catch (Exception ex)
					{
						Trace.TraceError("Reading error: {0}",ex);
					}
				}
				while (!valid)
				{
					using (ServiceInstallerDialog serviceInstallerDialog = new ServiceInstallerDialog())
					{
						serviceInstallerDialog.Username = Username;
						serviceInstallerDialog.ShowInTaskbar = true;
						serviceInstallerDialog.ShowDialog();
						switch (serviceInstallerDialog.Result)
						{
							case ServiceInstallerDialogResult.OK:
								Username = serviceInstallerDialog.Username;
								Password = serviceInstallerDialog.Password;
								valid = CheckCredentials(Username, Password);
								if (valid)
								{
									File.WriteAllLines(filename, new[] {Username, Password});
								}
								break;
							case ServiceInstallerDialogResult.Canceled:
								throw new InvalidOperationException("UserCanceledInstall");
						}
					}
				}
				x.RunAs(Username, Password, ServiceAccount.User);
			}));
			return builder;
		}

		private bool CheckCredentials(string username, string password)
		{
			try
			{
				if (username.StartsWith(@".\"))
				{
					using (PrincipalContext context = new PrincipalContext(ContextType.Machine))
					{
						return context.ValidateCredentials(username.Remove(0, 2), password);
					}
				}
				using (PrincipalContext context = new PrincipalContext(ContextType.Domain))
				{
					return context.ValidateCredentials(username, password);
				}
			}
			catch (Exception ex)
			{
				Trace.TraceError("Exception: {0}", ex);
				return false;
			}
		}

		public IEnumerable<ValidateResult> Validate()
		{
			yield return this.Success("All ok!");
		}
	}
}