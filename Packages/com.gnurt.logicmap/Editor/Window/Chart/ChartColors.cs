using UnityEditor;
using UnityEngine;

namespace Gnurt.LogicMap
{
    /// <summary>Màu cho phần vẽ bằng Painter2D (đường nối, ô thoi, lưới nền). Khớp bảng màu của GnurtWindow.uss.</summary>
    internal static class ChartColors
    {
        static bool Dark => EditorGUIUtility.isProSkin;

        public static Color Grid => Dark ? new Color(1, 1, 1, 0.06f) : new Color(0, 0, 0, 0.08f);
        public static Color Edge => Dark ? Hex(0x7a7a7a) : Hex(0x9a9a9a);
        public static Color EdgeActive => Dark ? Hex(0xb3a9ff) : Hex(0x5b4bd6);
        public static Color NodeFill => Dark ? Hex(0x303030) : Hex(0xffffff);
        public static Color NodeBorder => Dark ? Hex(0x3a3a3a) : Hex(0xcfcfcf);
        public static Color Script => Dark ? Hex(0x3fb6a8) : Hex(0x138a7e);
        public static Color Selected => Dark ? Hex(0xf2b84b) : Hex(0xc98a1a);
        public static Color Accent => Dark ? Hex(0x7c6cf2) : Hex(0x5b4bd6);

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f);
    }
}
