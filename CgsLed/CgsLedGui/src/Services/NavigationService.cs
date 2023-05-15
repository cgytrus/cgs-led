// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

namespace CgsLedGui.Services;

public static class NavigationService {
    public static event NavigatedEventHandler? navigated;
    public static event NavigationFailedEventHandler? navigationFailed;

    private static Frame? _frame;
    private static object? _lastParamUsed;

    public static Frame? frame {
        get {
            if(_frame != null)
                return _frame;
            _frame = Window.Current.Content as Frame;
            RegisterFrameEvents();
            return _frame;
        }
        set {
            UnregisterFrameEvents();
            _frame = value;
            RegisterFrameEvents();
        }
    }

    public static bool canGoBack => frame?.CanGoBack ?? false;

    public static bool canGoForward => frame?.CanGoForward ?? false;

    public static bool GoBack() {
        if(!canGoBack)
            return false;
        frame?.GoBack();
        return true;
    }

    public static void GoForward() => frame?.GoForward();

    public static bool Navigate(Type pageType, object? parameter = null, NavigationTransitionInfo? infoOverride = null) {
        // Don't open the same page multiple times
        if(frame?.Content?.GetType() == pageType && (parameter == null || parameter.Equals(_lastParamUsed)))
            return false;
        bool navigationResult = frame?.Navigate(pageType, parameter, infoOverride) ?? false;
        if(navigationResult)
            _lastParamUsed = parameter;
        return navigationResult;
    }

    public static bool Navigate<T>(object? parameter = null, NavigationTransitionInfo? infoOverride = null)
        where T : Page
        => Navigate(typeof(T), parameter, infoOverride);

    private static void RegisterFrameEvents() {
        if(_frame == null)
            return;
        _frame.Navigated += Frame_Navigated;
        _frame.NavigationFailed += Frame_NavigationFailed;
    }

    private static void UnregisterFrameEvents() {
        if(_frame == null)
            return;
        _frame.Navigated -= Frame_Navigated;
        _frame.NavigationFailed -= Frame_NavigationFailed;
    }

    private static void Frame_NavigationFailed(object sender, NavigationFailedEventArgs e) =>
        navigationFailed?.Invoke(sender, e);

    private static void Frame_Navigated(object sender, NavigationEventArgs e) => navigated?.Invoke(sender, e);
}
