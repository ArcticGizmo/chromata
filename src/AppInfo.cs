namespace Chromata;

internal static class AppInfo
{
    /// <summary>
    /// GitHub repository that hosts Velopack releases. Update-checks read its Releases feed.
    /// Create the repo and push a tagged release (see scripts/publish.ps1) before updates work.
    /// </summary>
    public const string RepoUrl = "https://github.com/ArcticGizmo/chromata";
}
