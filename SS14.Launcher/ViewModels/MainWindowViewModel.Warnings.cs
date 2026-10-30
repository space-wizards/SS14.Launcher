using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using Avalonia.Threading;
using SS14.Launcher.Models.Data;

namespace SS14.Launcher.ViewModels;

public sealed partial class MainWindowViewModel
{
    private sealed class LauncherWarning
    {
        /// <summary>
        /// Saved in the settings once the user answers, so it isn't shown again. Don't rename it, that would
        /// show it again to everyone who already answered.
        /// </summary>
        public required string Id { get; init; }

        // Keys of the strings in text.ftl.
        public required string TitleKey { get; init; }
        public required string DescriptionKey { get; init; }
        public required string AcceptKey { get; init; }

        /// <summary>Key of the text of a second button. If there is none, there is only the accept button.</summary>
        public string? SecondaryKey { get; init; }

        /// <summary>Whether the popup applies right now. Checked every time the popups are looked at again.</summary>
        public Func<bool> ShouldShow { get; init; } = static () => true;

        /// <summary>Runs when the accept button is pressed.</summary>
        public Action? OnAccept { get; init; }

        /// <summary>Runs when the second button is pressed, if there is one.</summary>
        public Action? OnSecondary { get; init; }

        /// <summary>Seconds the accept button stays disabled after the popup appears, to make sure it's read.</summary>
        public int CountdownSeconds { get; init; }

        /// <summary>Whether the title is red. Yes, that's all it does.</summary>
        public bool IsDanger { get; init; }

        /// <summary>
        /// Whether answering is saved (as <see cref="Id"/>), so it never shows again.
        /// If false, it's up to <see cref="ShouldShow"/> to stop it, and it may come back next launch.
        /// </summary>
        public bool Remember { get; init; } = true;
    }

    // The order here is the order they're shown in.
    private List<LauncherWarning> BuildWarnings() =>
    [
        new()
        {
            Id = "AuthOverride",
            TitleKey = "main-window-auth-override-title",
            DescriptionKey = "main-window-auth-override-desc",
            AcceptKey = "main-window-auth-override-acknowledge",
            ShouldShow = () => ShouldShowAuthOverrideWarning,
            OnAccept = () => ShouldShowAuthOverrideWarning = false,
            CountdownSeconds = 5,
            IsDanger = true,
            Remember = false
        },
        new()
        {
            Id = "EarlyAccess",
            TitleKey = "main-window-early-access-title",
            DescriptionKey = "main-window-early-access-desc",
            AcceptKey = "main-window-early-access-accept"
        },
        new()
        {
            Id = "Epilepsy",
            TitleKey = "main-window-epilepsy-title",
            DescriptionKey = "main-window-epilepsy-desc",
            AcceptKey = "main-window-epilepsy-accept"
        },
        new()
        {
            Id = "IntelDegradation",
            TitleKey = "main-window-intel-degrade-title",
            DescriptionKey = "main-window-intel-degrade-desc",
            AcceptKey = "main-window-intel-degrade-accept",
            ShouldShow = IsVulnerableToIntelDegradation,
            CountdownSeconds = 5
        },
        new()
        {
            Id = "Rosetta",
            TitleKey = "main-window-rosetta-title",
            DescriptionKey = "main-window-rosetta-desc",
            AcceptKey = "main-window-rosetta-accept",
            ShouldShow = IsAppleSiliconInRosetta
        },
        // A question rather than a warning, so it comes after the ones the user has to read.
        new()
        {
            Id = "BackgroundFetch",
            TitleKey = "main-window-background-fetch-title",
            DescriptionKey = "main-window-background-fetch-desc",
            AcceptKey = "main-window-background-fetch-enable",
            SecondaryKey = "main-window-background-fetch-decline",
            ShouldShow = () => !Cfg.GetCVar(CVars.PrefetchFavoriteServers) && Cfg.FavoriteServers.Count > 0,
            // Through the options tab so its checkbox follows.
            OnAccept = () => OptionsTab.PrefetchFavoriteServers = true
        }
    ];

    private List<LauncherWarning> _warnings = [];
    private int _warningCountdown;

    private LauncherWarning? _currentWarning;

    private LauncherWarning? CurrentWarning
    {
        get => _currentWarning;
        set
        {
            if (!SetProperty(ref _currentWarning, value))
                return;

            RestartWarningCountdown(value?.CountdownSeconds ?? 0);

            OnPropertyChanged(nameof(ShowWarning));
            OnPropertyChanged(nameof(WarningTitle));
            OnPropertyChanged(nameof(WarningDescription));
            OnPropertyChanged(nameof(WarningAcceptText));
            OnPropertyChanged(nameof(WarningSecondaryText));
            OnPropertyChanged(nameof(WarningTitleBrush));
        }
    }

    private LauncherWarning? FindCurrentWarning()
    {
        return _warnings.FirstOrDefault(w => (!w.Remember || !IsDismissed(w.Id)) && w.ShouldShow());
    }

    private bool IsDismissed(string id)
    {
        return Cfg.GetCVar(CVars.DismissedWarnings).Split(',').Contains(id);
    }

    private void Dismiss(string id)
    {
        var dismissed = Cfg.GetCVar(CVars.DismissedWarnings).Split(',', StringSplitOptions.RemoveEmptyEntries);
        Cfg.SetCVar(CVars.DismissedWarnings, string.Join(',', dismissed.Append(id).Distinct()));
        Cfg.CommitConfig();
    }

    public bool ShowWarning => _currentWarning != null;
    public string WarningTitle => _currentWarning is { } w ? _loc.GetString(w.TitleKey) : "";
    public string WarningDescription => _currentWarning is { } w ? _loc.GetString(w.DescriptionKey) : "";
    public string? WarningSecondaryText => _currentWarning?.SecondaryKey is { } key ? _loc.GetString(key) : null;
    public IBrush? WarningTitleBrush => _currentWarning?.IsDanger == true ? Brushes.Red : null;

    public string WarningAcceptText
    {
        get
        {
            if (_currentWarning is not { } w)
                return "";

            var text = _loc.GetString(w.AcceptKey);
            if (WarningCountdown <= 0)
                return text;

            return _loc.GetString("main-window-warning-accept-countdown",
                ("text", text), ("seconds", WarningCountdown));
        }
    }

    /// <summary>
    /// Seconds left before the current warning can be acknowledged. 0 if it can be acknowledged right away.
    /// </summary>
    public int WarningCountdown
    {
        get => _warningCountdown;
        private set
        {
            if (!SetProperty(ref _warningCountdown, value))
                return;

            OnPropertyChanged(nameof(IsWarningButtonEnabled));
            OnPropertyChanged(nameof(WarningAcceptText));
        }
    }

    public bool IsWarningButtonEnabled => WarningCountdown <= 0;

    public void WarningAcceptPressed()
    {
        if (_currentWarning is not { } warning)
            return;

        warning.OnAccept?.Invoke();
        Answered(warning);
    }

    public void WarningSecondaryPressed()
    {
        if (_currentWarning is not { } warning)
            return;

        warning.OnSecondary?.Invoke();
        Answered(warning);
    }

    private void Answered(LauncherWarning warning)
    {
        if (warning.Remember)
            Dismiss(warning.Id);

        WarningsChanged();
    }

    /// <summary>
    /// Looks again at which popup should be up. Call it after something that <see cref="LauncherWarning.ShouldShow"/>
    /// depends on changes.
    /// </summary>
    private void WarningsChanged()
    {
        CurrentWarning = FindCurrentWarning();
    }

    public bool ShouldShowAuthOverrideWarning
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                WarningsChanged();
        }
    }

    private void RestartWarningCountdown(int seconds)
    {
        _warningCountdownTimer?.Dispose();
        _warningCountdownTimer = null;

        WarningCountdown = seconds;
        if (WarningCountdown <= 0)
            return;

        _warningCountdownTimer = DispatcherTimer.Run(() =>
        {
            WarningCountdown--;
            return WarningCountdown > 0;
        }, TimeSpan.FromSeconds(1), DispatcherPriority.Normal);
    }
}
