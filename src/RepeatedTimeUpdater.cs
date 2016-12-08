using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using NuGet;
using Squirrel;
using Topshelf.Squirrel.Windows.Interfaces;

namespace Topshelf.Squirrel.Windows
{
	public class RepeatedTimeUpdater : IUpdater
	{
		private TimeSpan checkUpdatePeriod = TimeSpan.FromSeconds(30);
		private readonly IUpdateManager updateManager;
		private string curversion;

		/// <summary>
		/// Задать время между проверками доступности обновлений. По умолчанию 30 секунд.
		/// </summary>
		/// <param name="checkSpan"></param>
		/// <returns></returns>
		public RepeatedTimeUpdater SetCheckUpdatePeriod(TimeSpan checkSpan)
		{
			checkUpdatePeriod = checkSpan;
			return this;
		}

		public RepeatedTimeUpdater(IUpdateManager updateManager)
		{
			if (!Environment.UserInteractive)
			{
				if (updateManager == null)
					throw new Exception("Update manager can not be null");
			}
			curversion = Assembly.GetEntryAssembly().GetName().Version.ToString();
			this.updateManager = updateManager;
		}
		
		/// <summary>
		/// Метод который проверяет обновления
		/// </summary>
		public void Start()
		{
			if (!Environment.UserInteractive)
			{
				Task.Run(Update).ConfigureAwait(false);
			}
		}

		private async Task Update()
		{
			if (updateManager == null)
				throw new Exception("Update manager can not be null");
			Trace.TraceInformation("Automatic-renewal was launched ({0})", curversion);

			{
				while (true)
				{
					await Task.Delay(checkUpdatePeriod);
					try
					{
						//Проверяем наличие новой версии
						var update = await updateManager.CheckForUpdate();
						try
						{
							var oldVersion = update.CurrentlyInstalledVersion?.Version ?? new SemanticVersion(0, 0, 0, 0);
							var newVersion = update.FutureReleaseEntry.Version;
							if (oldVersion < newVersion)
							{
								Trace.TraceInformation("Found a new version: {0}", newVersion);

								//Скачиваем новую версию
								await updateManager.DownloadReleases(update.ReleasesToApply);

								//Распаковываем новую версию
								await updateManager.ApplyReleases(update);
							}
						}
						catch (Exception ex)
						{
							Trace.TraceError("Error on update ({0}): {1}", curversion, ex);
						}
					}
					catch (Exception ex)
					{
						Trace.TraceError("Error on check for update ({0}): {1}", curversion, ex);
					}
				}
			}
		}
	}
}