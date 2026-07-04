using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace KeyLogger.Utils
{
    internal static class KeyboardConverter
    {
        // ────────────────────────────────────── DLL‑импорты ───────────────────────────────────────

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetKeyboardState(byte[] lpKeyState);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll")]
        private static extern IntPtr GetKeyboardLayout(uint idThread);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int ToUnicodeEx(
            uint wVirtKey,
            uint wScanCode,
            byte[] lpKeyState,
            [Out] StringBuilder pwszBuff,
            int cchBuff,
            uint wFlags,
            IntPtr dwhkl);

        // ────────────────────────────────────── Константы ───────────────────────────────────────

        private const uint MAPVK_VK_TO_VSC = 0x00;   // виртуальный код → scancode
        private const uint MAPVK_VSC_TO_VK = 0x01;
        private const uint MAPVK_VK_TO_CHAR = 0x02;

        // ────────────────────────────────────── Публичный API ───────────────────────────────────

        /// <summary>
        /// Возвращает строку‑символ, который пользователь видит при нажатии клавиши.
        /// Если символ не получается (например, Shift+F5), возвращается пустая строка.
        /// </summary>
        public static string GetPrintableChar(int vkCode)
        {
            // 1. Получаем состояние всех клавиш
            byte[] keyState = new byte[256];
            if(!GetKeyboardState(keyState))
                return string.Empty;          // ошибка – ничего не делаем

            // 2. Переводим VK → scancode (необязательно, но безопасно)
            uint scanCode = MapVirtualKey((uint)vkCode, MAPVK_VK_TO_VSC);

            // 3. Получаем дескриптор раскладки для текущего потока
            IntPtr hkl = GetKeyboardLayout((uint)Thread.CurrentThread.ManagedThreadId);

            // 4. Конвертируем в Unicode‑символ(ы)
            StringBuilder sb = new StringBuilder(10);
            int result = ToUnicodeEx(
                (uint)vkCode,
                scanCode,
                keyState,
                sb,
                sb.Capacity,
                0,          // wFlags
                hkl);

            if(result > 0)
                return sb.ToString();   // один или несколько символов

            return string.Empty;        // ничего не получено
        }
    }
}
