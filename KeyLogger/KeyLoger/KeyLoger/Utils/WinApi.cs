using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace KeyLogger.Utils
{
    internal static class WinApi
    {
        // ────────────────────────────────────── DLL‑импорты ───────────────────────────────────────

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetKeyboardState(byte[] lpKeyState);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll")]
        public static extern IntPtr GetKeyboardLayout(uint idThread);

        [DllImport("user32.dll",
                   CharSet = CharSet.Unicode,
                   SetLastError = true)]
        public static extern int ToUnicodeEx(
            uint wVirtKey,
            uint wScanCode,
            byte[] lpKeyState,
            [Out] StringBuilder pwszBuff,
            int cchBuff,
            uint wFlags,
            IntPtr dwhkl);

        // ────────────────────────────────────── Константы для MapVirtualKey ───────────────────

        public const uint MAPVK_VK_TO_VSC = 0x00;   // виртуальный код → scancode
        public const uint MAPVK_VSC_TO_VK = 0x01;
        public const uint MAPVK_VK_TO_CHAR = 0x02;
    }
}
