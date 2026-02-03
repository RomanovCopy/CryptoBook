using CryptoBook.DTO;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IFileLauncherService
    {
        // 1) "Двойной клик": файл/папка/URL по умолчанию
        LaunchResult Open(string target);

        // 2) То же, но с явным verb: open, edit, print, runas, properties, ...
        LaunchResult Open(string target, string verb);

        // 3) Открыть/выполнить через Shell с параметрами
        LaunchResult ShellExecute(ShellLaunchOptions options);

        // 4) Запуск через конкретную программу (например notepad.exe file.txt)
        LaunchResult OpenWith(string applicationPath, string target, string? arguments = null, string? workingDirectory = null);

        // 5) Запустить EXE (без Shell), с контролем окна/учётки/переменных окружения и т.п.
        LaunchResult StartProcess(ProcessLaunchOptions options);

        // 6) Показать в Проводнике (выделить файл) или открыть папку
        LaunchResult RevealInExplorer(string path, bool select = true);

        // 7) Удобные шорткаты
        LaunchResult Print(string path);   // Shell verb "print"
        LaunchResult Edit(string path);    // Shell verb "edit"
        LaunchResult RunAsAdmin(string path, string? arguments = null); // verb "runas"

        // 8) Консольные команды (cmd/powershell)
        LaunchResult RunCmd(string command, string? workingDirectory = null, bool runAsAdmin = false);
        LaunchResult RunPowerShell(string command, string? workingDirectory = null, bool runAsAdmin = false);
    }
}
