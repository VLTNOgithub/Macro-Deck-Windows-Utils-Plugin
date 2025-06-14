﻿using SuchByte.MacroDeck.Logging;
using SuchByte.WindowsUtils.Utils;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace SuchByte.WindowsUtils.Services;

public class ApplicationLauncher
{
    [DllImport("kernel32.dll")]
    public static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

    [DllImport("psapi.dll")]
    static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, [Out] StringBuilder lpBaseName, [In][MarshalAs(UnmanagedType.U4)] int nSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool CloseHandle(IntPtr hObject);


    public static void StartApplication(string path, string arguments, bool asAdmin)
    {
        var p = new Process
        {
            StartInfo = new ProcessStartInfo(path)
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(path),
                Arguments = arguments,
                Verb = asAdmin ? "runas" : ""
            }
        };
        p.Start();
    }

    public static bool IsRunning(string path)
    {
        path = WindowsShortcut.GetShortcutTarget(path);
        bool isRunning = GetProcessByPath(path) != null;
        return isRunning;
    }

    public static void KillApplication(string path)
    {
        path = WindowsShortcut.GetShortcutTarget(path);
        if (!IsRunning(path)) return;
        var p = GetProcessByPath(path);
        if (p == null) return;
        Process.GetProcessesByName(p.ProcessName).ToList().ForEach(p =>
        {
            MacroDeckLogger.Trace(Main.Instance, $"Killing process: {p.ProcessName} PID: {p.Id}");
            p.Kill();
        });
    }

    public static void BringToForeground(string path)
    {
        path = WindowsShortcut.GetShortcutTarget(path);
        if (!IsRunning(path)) return;

        var processes = Process.GetProcesses()
            .Where(p => GetProcessFileName(p.Id).Equals(path, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Try to find a process with a valid main window handle
        foreach (var proc in processes.OrderByDescending(p => p.Id))
        {
            IntPtr handle = proc.MainWindowHandle;
            if (handle == IntPtr.Zero) continue;

            WindowActivator.ForceActivateWindow(handle);

            return;
        }
    }

    public static Process GetProcessByPath(string path)
    {
        path = WindowsShortcut.GetShortcutTarget(path);
        return Process.GetProcesses().ToArray().Where(p => GetProcessFileName(p.Id).Equals(path, StringComparison.OrdinalIgnoreCase)).OrderByDescending(p => p.Id).FirstOrDefault();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetProcessFileName(int pid)
    {
        var processHandle = OpenProcess(0x0400 | 0x0010, false, pid);

        if (processHandle == IntPtr.Zero)
        {
            return "";
        }

        const int lengthSb = 4000;

        var sb = new StringBuilder(lengthSb);

        string result = null;

        if (GetModuleFileNameEx(processHandle, IntPtr.Zero, sb, lengthSb) > 0)
        {
            result = sb.ToString();
        }

        CloseHandle(processHandle);

        return result;
    }
}
