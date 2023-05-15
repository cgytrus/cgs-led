using System;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CgsLedGui.Helpers;

public static class NavHelper {
    public static Type? GetNavigateTo(NavigationViewItem item) => (Type?)item.GetValue(navigateToProperty);
    public static void SetNavigateTo(NavigationViewItem item, Type value) => item.SetValue(navigateToProperty, value);
    public static readonly DependencyProperty navigateToProperty =
        DependencyProperty.RegisterAttached("navigateTo", typeof(Type), typeof(NavHelper), new PropertyMetadata(null));
}
