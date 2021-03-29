using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using DynamicData;
using DynamicData.Aggregation;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Exceptions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Model;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send
{
	[NavigationMetaData(Title = "Privacy Control")]
	public partial class PrivacyControlViewModel : RoutableViewModel
	{
		private readonly Wallet _wallet;
		private readonly SourceList<PocketViewModel> _pocketSource;
		private readonly ReadOnlyObservableCollection<PocketViewModel> _pockets;

		[AutoNotify] private decimal _stillNeeded;
		[AutoNotify] private bool _enoughSelected;

		public PrivacyControlViewModel(Wallet wallet, TransactionInfo transactionInfo, TransactionBroadcaster broadcaster)
		{
			_wallet = wallet;

			_pocketSource = new SourceList<PocketViewModel>();

			_pocketSource.Connect()
				.Bind(out _pockets)
				.Subscribe();

			var selected = _pocketSource.Connect()
				.AutoRefresh()
				.Filter(x => x.IsSelected);

			var selectedList = selected.AsObservableList();

			selected.Sum(x => x.TotalBtc)
				.Subscribe(x =>
				{
					StillNeeded = transactionInfo.Amount.ToDecimal(MoneyUnit.BTC) - x;
					EnoughSelected = StillNeeded <= 0;
				});

			StillNeeded = transactionInfo.Amount.ToDecimal(MoneyUnit.BTC);

			EnableCancel = true;

			EnableBack = true;

			NextCommand = ReactiveCommand.CreateFromTask(
				async () => await OnNext(wallet, transactionInfo, broadcaster, selectedList),
				this.WhenAnyValue(x => x.EnoughSelected));

			EnableAutoBusyOn(NextCommand);
		}

		public ReadOnlyObservableCollection<PocketViewModel> Pockets => _pockets;

		private async Task OnNext(Wallet wallet, TransactionInfo transactionInfo, TransactionBroadcaster broadcaster, IObservableList<PocketViewModel> selectedList)
		{
			transactionInfo.Coins = selectedList.Items.SelectMany(x => x.Coins).ToArray();

			try
			{
				if (transactionInfo.PayJoinClient is { })
				{
					await BuildTransactionAsPayJoinAsync(wallet, transactionInfo, broadcaster);
				}
				else
				{
					await BuildNormalTransactionAsync(wallet, transactionInfo, broadcaster);
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
				await ShowErrorAsync("Transaction Building", ex.ToUserFriendlyString(), "Wasabi was unable to create your transaction.");
				Navigate().BackTo<SendViewModel>();
			}
		}

		private async Task BuildNormalTransactionAsync(Wallet wallet, TransactionInfo transactionInfo, TransactionBroadcaster broadcaster)
		{
			try
			{
				var transactionResult = await Task.Run(() => TransactionHelpers.BuildTransaction(_wallet, transactionInfo));
				Navigate().To(new TransactionPreviewViewModel(wallet, transactionInfo, broadcaster, transactionResult));
			}
			catch (InsufficientBalanceException)
			{
				var transactionResult = await Task.Run(() => TransactionHelpers.BuildTransaction(_wallet, transactionInfo, subtractFee: true));
				var dialog = new InsufficientBalanceDialogViewModel(BalanceType.Pocket, transactionResult, wallet.Synchronizer.UsdExchangeRate);
				var result = await NavigateDialog(dialog, NavigationTarget.DialogScreen);

				if (result.Result)
				{
					Navigate().To(new TransactionPreviewViewModel(wallet, transactionInfo, broadcaster, transactionResult));
				}
				else
				{
					Navigate().BackTo<SendViewModel>();
				}
			}
		}

		private async Task BuildTransactionAsPayJoinAsync(Wallet wallet, TransactionInfo transactionInfo, TransactionBroadcaster broadcaster)
		{
			try
			{
				var transactionResult = await Task.Run(() => TransactionHelpers.BuildTransaction(_wallet, transactionInfo));
				Navigate().To(new TransactionPreviewViewModel(wallet, transactionInfo, broadcaster, transactionResult));
			}
			catch (InsufficientBalanceException)
			{
				await ShowErrorAsync("Transaction Building", "There are not enough funds selected to cover the transaction fee.", "Wasabi was unable to create your transaction.");
			}
		}

		protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
		{
			base.OnNavigatedTo(isInHistory, disposables);

			if (!isInHistory)
			{
				var pockets = _wallet.Coins.GetPockets(_wallet.ServiceConfiguration.GetMixUntilAnonymitySetValue());

				foreach (var pocket in pockets)
				{
					_pocketSource.Add(new PocketViewModel(pocket));
				}

				if (_pocketSource.Count == 1)
				{
					_pocketSource.Items.First().IsSelected = true;
				}
			}
		}
	}
}
