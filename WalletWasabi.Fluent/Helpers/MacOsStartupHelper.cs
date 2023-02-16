using System.Collections.Generic;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.Helpers;

public static class MacOsStartupHelper
{
	private static readonly string AddCmd = $"osascript -e \' tell application \"System Events\" to make new login item at end with properties {{name:\"{Constants.AppName}\", path:\"/Applications/{Constants.AppName}.app\", hidden:true}} \'";
	private static readonly string DeleteCmd = $"osascript -e \' tell application \"System Events\" to delete login item \"{Constants.AppName}\" \'";
	private static readonly string ListCmd = $"osascript -e \' tell application \"System Events\" to get the name of every login item \'";

	public static async Task AddOrRemoveLoginItemAsync(bool runOnSystemStartup)
	{
		List<string> result = await EnvironmentHelpers.ShellExecAndGetResultAsync(ListCmd).ConfigureAwait(false);
		foreach (var line in result)
		{
			Logger.LogInfo(line);
		}
		if (!result.Contains(Constants.AppName) && runOnSystemStartup)
		{
			await EnvironmentHelpers.ShellExecAsync(AddCmd).ConfigureAwait(false);
		}
		else if (result.Contains(Constants.AppName) && !runOnSystemStartup)
		{
			await EnvironmentHelpers.ShellExecAsync(DeleteCmd).ConfigureAwait(false);
		}
	}
}
