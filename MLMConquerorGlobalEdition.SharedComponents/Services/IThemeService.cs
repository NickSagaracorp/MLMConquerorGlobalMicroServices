namespace MLMConquerorGlobalEdition.SharedComponents.Services;

public interface IThemeService
{
    bool IsDarkMode { get; }
    event Action? OnThemeChanged;
    void ToggleTheme();
    void SetDarkMode(bool isDark);
}
