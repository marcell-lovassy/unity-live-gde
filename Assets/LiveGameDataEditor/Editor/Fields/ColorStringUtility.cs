using UnityEngine;

namespace LiveGameDataEditor.Editor
{
    public static class ColorStringUtility
    {
        public static bool TryParseHtmlColor(string value, out Color color)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                color = Color.white;
                return false;
            }

            return ColorUtility.TryParseHtmlString(value, out color);
        }

        public static string ToHex(Color color, bool includeAlpha)
        {
            return includeAlpha
                ? "#" + ColorUtility.ToHtmlStringRGBA(color)
                : "#" + ColorUtility.ToHtmlStringRGB(color);
        }
    }
}