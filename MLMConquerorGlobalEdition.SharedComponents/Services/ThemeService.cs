namespace MLMConquerorGlobalEdition.SharedComponents.Services;

public class ThemeService : IThemeService
{
    private bool _isDarkMode;

    public bool IsDarkMode => _isDarkMode;
    public event Action? OnThemeChanged;

    public void ToggleTheme()
    {
        _isDarkMode = !_isDarkMode;
        OnThemeChanged?.Invoke();
    }

    public void SetDarkMode(bool isDark)
    {
        _isDarkMode = isDark;
        OnThemeChanged?.Invoke();
    }
}
