using System;
using System.Linq;
using System.Windows.Input;

using CgsLedGui.Helpers;
using CgsLedGui.Services;

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace CgsLedGui.ViewModels;

public class ShellViewModel : Observable {
    private bool _isBackEnabled;
    private NavigationView? _navigationView;
    private NavigationViewItem? _selected;

    private ICommand? _itemInvokedCommand;

    public bool isBackEnabled {
        get => _isBackEnabled;
        set => Set(ref _isBackEnabled, value);
    }

    public NavigationViewItem? selected {
        get => _selected;
        set => Set(ref _selected, value);
    }

    public ICommand itemInvokedCommand =>
        _itemInvokedCommand ??= new RelayCommand<NavigationViewItemInvokedEventArgs>(OnItemInvoked);

    public void Initialize(Frame frame, NavigationView navigationView) {
        _navigationView = navigationView;
        NavigationService.frame = frame;
        NavigationService.navigationFailed += Frame_NavigationFailed;
        NavigationService.navigated += Frame_Navigated;
        _navigationView.BackRequested += OnBackRequested;
    }

    private void OnItemInvoked(NavigationViewItemInvokedEventArgs? args) {
        NavigationViewItem? item = _navigationView?.MenuItems.OfType<NavigationViewItem>()
            .First(menuItem => (string?)menuItem.Content == (string?)args?.InvokedItem);
        if(item?.GetValue(NavHelper.navigateToProperty) is Type pageType)
            NavigationService.Navigate(pageType);
    }

    private static void OnBackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args) =>
        NavigationService.GoBack();

    private static void Frame_NavigationFailed(object sender, NavigationFailedEventArgs e) => throw e.Exception;

    private void Frame_Navigated(object sender, NavigationEventArgs e) {
        isBackEnabled = NavigationService.canGoBack;
        selected = _navigationView?.MenuItems.OfType<NavigationViewItem>()
            .FirstOrDefault(menuItem => IsMenuItemForPageType(menuItem, e.SourcePageType));
    }

    // ReSharper disable once SuggestBaseTypeForParameter
    private static bool IsMenuItemForPageType(NavigationViewItem menuItem, Type sourcePageType) {
        Type? pageType = menuItem.GetValue(NavHelper.navigateToProperty) as Type;
        return pageType == sourcePageType;
    }
}
