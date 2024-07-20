using System;
using System.Globalization;
using System.Linq;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Splat;
using SS14.Launcher.Localization;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Utility;

namespace SS14.Launcher.ViewModels;

public sealed class LanguageSelectorViewModel : ObservableRecipient
{
    private readonly LocalizationManager _localization;
    private readonly DataManager _dataManager;
    private readonly LanguageSelectorLanguageViewModel _systemDefaultLanguage;

    private bool _isDropDownOpen;
    private bool _isChangeSelected;

    // The language currently set in the config.
    private LanguageSelectorLanguageViewModel? _configValue;

    public LanguageSelectorLanguageViewModel[] Languages { get; }

    public bool IsDropDownOpen
    {
        get => _isDropDownOpen;
        set
        {
            var wasChanged = SetProperty(ref _isDropDownOpen, value);
            if (wasChanged && value)
            {
                UpdateSelectedOnOpen();
            }
        }
    }

    public bool IsChangeSelected
    {
        get => _isChangeSelected;
        set => SetProperty(ref _isChangeSelected, value);
    }

    public LanguageSelectorViewModel()
    {
        _localization = LocalizationManager.Instance;
        _dataManager = Locator.Current.GetRequiredService<DataManager>();

        _systemDefaultLanguage = new LanguageSelectorLanguageViewModel(_localization, this, null);
        Languages =
        [
            // System default
            _systemDefaultLanguage,
            ..LocalizationManager.AvailableLanguages
                .OrderBy(x => x.Culture.EnglishName)
                .Select(x => new LanguageSelectorLanguageViewModel(_localization, this, x.Culture))
        ];
    }

    private void UpdateSelectedOnOpen()
    {
        var language = _dataManager.GetCVar(CVars.Language);
        if (language == "")
            language = null;

        _configValue = Languages.SingleOrDefault(l => l.Culture?.Name == language);
        if (_configValue == null)
            return;

        _configValue.IsChecked = true;
    }

    public void OnHelpTranslateButtonPressed()
    {
        Helpers.OpenUri(new Uri(ConfigConstants.TranslateUrl));
    }

    public void OnSaveButtonPressed()
    {
        IsDropDownOpen = false;

        var selected = Languages.SingleOrDefault(x => x.IsChecked) ?? _systemDefaultLanguage;
        _dataManager.SetCVar(CVars.Language, selected.Culture?.Name);
        _dataManager.CommitConfig();
        _localization.SwitchToLanguage(selected.Culture);
    }

    public void OnCancelButtonPressed()
    {
        IsDropDownOpen = false;
    }

    public void UpdateLanguageChecked()
    {
        IsChangeSelected = !(_configValue?.IsChecked ?? false);
    }
}

public sealed class LanguageSelectorLanguageViewModel(
    LocalizationManager loc,
    LanguageSelectorViewModel parent,
    CultureInfo? culture) : ObservableObject
{
    private bool _isChecked;
    public CultureInfo? Culture { get; } = culture;

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            SetProperty(ref _isChecked, value);
            parent.UpdateLanguageChecked();
        }
    }

    public string Text
    {
        get
        {
            if (Culture == null)
            {
                return loc.GetString("language-selector-system-language",
                    ("languageName", loc.SystemCulture.NativeName));
            }

            return loc.GetString(
                "language-selector-language",
                ("languageName", Culture.NativeName),
                ("englishName", Culture.EnglishName));
        }
    }
}
