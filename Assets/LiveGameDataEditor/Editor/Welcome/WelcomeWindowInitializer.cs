using UnityEditor;

namespace LiveGameDataEditor.Editor
{
    /// <summary>
    ///     Triggers <see cref="WelcomeWindow" /> automatically on the first editor launch
    ///     after the package is installed.
    ///     Uses the <c>[InitializeOnLoad]</c> pattern: the static constructor runs every
    ///     time the editor starts or scripts recompile, but the window is only shown once
    ///     (guarded by the <c>LiveGameDataEditor.WelcomeShown</c> EditorPrefs key).
    /// </summary>
    [InitializeOnLoad]
    internal static class WelcomeWindowInitializer
    {
        private const string ShownPref = "LiveGameDataEditor.WelcomeShown";

        static WelcomeWindowInitializer()
        {
            if (EditorPrefs.GetBool(ShownPref, false)) return;

            // Delay the show call so it runs after the editor has fully initialised
            // (Unity does not allow opening windows in static constructors directly).
            EditorApplication.delayCall += () =>
            {
                EditorPrefs.SetBool(ShownPref, true);
                WelcomeWindow.ShowOnStartup();
            };
        }
    }
}