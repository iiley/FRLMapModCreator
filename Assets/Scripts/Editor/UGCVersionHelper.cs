using UnityEngine;

namespace FRLMapMod.Editor
{
    public class UGCVersionHelper
    {
        

        internal static bool TryParseVersion(string version, out int major, out int minor)
        {
            major = 0;
            minor = 0;

            if (string.IsNullOrEmpty(version) || version == "N/A")
                return false;

            var parts = version.Split('.');
            if (parts.Length != 2)
                return false;

            if (parts[0].Length == 0 || parts[0].Length > 3)
                return false;
            if (parts[1].Length == 0 || parts[1].Length > 3)
                return false;

            if (!int.TryParse(parts[0], out major))
                return false;
            if (!int.TryParse(parts[1], out minor))
                return false;

            if (major < 0 || minor < 0)
                return false;

            return true;
        }
        
        internal static string GetNextVersionString(string current)
        {
            if (!TryParseVersion(current, out var major, out var minor))
            {
                return "0.1";
            }
            if (minor >= 999)
            {
                major++;
            }
            else
            {
                minor++;
            }
            return $"{major}.{minor}";
        }
        
        internal static bool CanEditMetadata(UGCItemData item)
        {
            if (item == null)
                return false;

            if (item.Status != UGCItemStatus.Draft) return false;
            
            if (!item.HasPublished)
                return true; // only draft, never published

            if (!item.HasDraft)
                return false;

            if (!TryParseVersion(item.DraftVersion, out var dMajor, out var dMinor))
                return false;
            if (!TryParseVersion(item.PublishedVersion, out var pMajor, out var pMinor))
                return false;

            // Allow editing only when Draft > Published
            return IsNewVersionGreater(pMajor, pMinor, dMajor, dMinor);
        }

        internal static bool IsNewVersionGreater(int oldMajor, int oldMinor, int newMajor, int newMinor)
        {
            if (newMajor > oldMajor)
                return true;
            if (newMajor < oldMajor)
                return false;

            // major equal, compare minor
            return newMinor > oldMinor;
        }

        internal static string ColorString(string s, string color)
        {
            return string.IsNullOrEmpty(color) ? s : $"<color={color}>{s}</color>";
        }
        
        internal static string RedString(string s, bool red)
        {
            return red ? $"<color=red>{s}</color>" : s;
        }
    }
}