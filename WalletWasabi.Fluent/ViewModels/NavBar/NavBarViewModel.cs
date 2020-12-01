using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Fluent.ViewModels.Wallets;

namespace WalletWasabi.Fluent.ViewModels.NavBar
{
	/// <summary>
	/// The ViewModel that represents the structure of the sidebar.
	/// </summary>
	public class NavBarViewModel : ViewModelBase
	{
		private ObservableCollection<NavBarItemViewModel> _topItems;
		private ObservableCollection<NavBarItemViewModel> _bottomItems;
		private NavBarItemViewModel? _selectedItem;
		private readonly WalletManagerViewModel _walletManager;
		private bool _isBackButtonVisible;
		private bool _isNavigating;
		private bool _isOpen;
		private Action? _toggleAction;
		private Action? _collapseOnClickAction;

		public NavBarViewModel(TargettedNavigationStack mainScreen, WalletManagerViewModel walletManager)
		{
			_walletManager = walletManager;
			_topItems = new ObservableCollection<NavBarItemViewModel>();
			_bottomItems = new ObservableCollection<NavBarItemViewModel>();

			mainScreen.WhenAnyValue(x => x.CurrentPage)
				.OfType<NavBarItemViewModel>()
				.Subscribe(x => CurrentPageChanged(x, walletManager));

			this.WhenAnyValue(x => x.SelectedItem)
				.OfType<NavBarItemViewModel>()
				.Subscribe(NavigateItem);

			this.WhenAnyValue(x => x.IsOpen)
				.Subscribe(x =>
				{
					if (SelectedItem is { })
					{
						SelectedItem.IsExpanded = x;
					}
				});
		}

		public ObservableCollection<NavBarItemViewModel> TopItems
		{
			get => _topItems;
			set => this.RaiseAndSetIfChanged(ref _topItems, value);
		}

		public ObservableCollection<WalletViewModelBase> Items => _walletManager.Items;

		public ObservableCollection<NavBarItemViewModel> BottomItems
		{
			get => _bottomItems;
			set => this.RaiseAndSetIfChanged(ref _bottomItems, value);
		}

		public NavBarItemViewModel? SelectedItem
		{
			get => _selectedItem;
			set => SetSelectedItem(value);
		}

		public Action? ToggleAction
		{
			get => _toggleAction;
			set => this.RaiseAndSetIfChanged(ref _toggleAction, value);
		}

		public Action? CollapseOnClickAction
		{
			get => _collapseOnClickAction;
			set => this.RaiseAndSetIfChanged(ref _collapseOnClickAction, value);
		}

		public bool IsBackButtonVisible
		{
			get => _isBackButtonVisible;
			set => this.RaiseAndSetIfChanged(ref _isBackButtonVisible, value);
		}

		public bool IsOpen
		{
			get => _isOpen;
			set => this.RaiseAndSetIfChanged(ref _isOpen, value);
		}

		public void RegisterTopItem(NavBarItemViewModel item, bool isSelected = false)
		{
			_topItems.Add(item);

			if (isSelected)
			{
				_selectedItem = item;
			}
		}

		public void RegisterBottomItem(NavBarItemViewModel item, bool isSelected = false)
		{
			_bottomItems.Add(item);

			if (isSelected)
			{
				_selectedItem = item;
			}
		}

		public void DoToggleAction()
		{
			ToggleAction?.Invoke();
		}

		private void RaiseAndChangeSelectedItem(NavBarItemViewModel? value)
		{
			_selectedItem = value;
			this.RaisePropertyChanged(nameof(SelectedItem));
		}

		private void Select(NavBarItemViewModel? value)
		{
			if (_selectedItem == value)
			{
				return;
			}

			if (_selectedItem is { })
			{
				_selectedItem.IsSelected = false;
				_selectedItem.IsExpanded = false;

				if (_selectedItem.Parent is { })
				{
					_selectedItem.Parent.IsSelected = false;
					_selectedItem.Parent.IsExpanded = false;
				}
			}

			RaiseAndChangeSelectedItem(null);
			RaiseAndChangeSelectedItem(value);

			if (_selectedItem is { })
			{
				_selectedItem.IsSelected = true;
				_selectedItem.IsExpanded = IsOpen;

				if (_selectedItem.Parent is { })
				{
					_selectedItem.Parent.IsSelected = true;
					_selectedItem.Parent.IsExpanded = true;
				}
			}
		}

		private void SetSelectedItem(NavBarItemViewModel? value)
		{
			if (value is null || value.SelectionMode == NavBarItemSelectionMode.Selected)
			{
				Select(value);
				return;
			}

			if (value.SelectionMode == NavBarItemSelectionMode.Button)
			{
				_isNavigating = true;
				var previous = _selectedItem;
				RaiseAndChangeSelectedItem(null);
				RaiseAndChangeSelectedItem(value);
				_isNavigating = false;
				NavigateItem(value);
				_isNavigating = true;
				RaiseAndChangeSelectedItem(null);
				RaiseAndChangeSelectedItem(previous);
				_isNavigating = false;
				return;
			}

			if (value.SelectionMode == NavBarItemSelectionMode.Toggle)
			{
				_isNavigating = true;
				var previous = _selectedItem;
				RaiseAndChangeSelectedItem(null);
				RaiseAndChangeSelectedItem(value);
				_isNavigating = false;
				value.Toggle();
				_isNavigating = true;
				RaiseAndChangeSelectedItem(null);
				RaiseAndChangeSelectedItem(previous);
				_isNavigating = false;
			}
		}

		private void CurrentPageChanged(NavBarItemViewModel x, WalletManagerViewModel walletManager)
		{
			if (walletManager.Items.Contains(x) || _topItems.Contains(x) || _bottomItems.Contains(x))
			{
				if (!_isNavigating && x.SelectionMode == NavBarItemSelectionMode.Selected)
				{
					_isNavigating = true;
					SetSelectedItem(x);
					_isNavigating = false;
				}
			}
		}

		private void NavigateItem(NavBarItemViewModel x)
		{
			if (!_isNavigating)
			{
				_isNavigating = true;
				if (x.OpenCommand.CanExecute(default))
				{
					x.OpenCommand.Execute(default);
				}

				CollapseOnClickAction?.Invoke();
				_isNavigating = false;
			}
		}
	}
}
