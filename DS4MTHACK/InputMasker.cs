using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Collections.Generic;

namespace DS4MTHACK
{
    public static class InputMasker
    {
        private static IntPtr hookId = IntPtr.Zero;
        public static bool ControllerConnected = false;

        // Lista de teclas e botões do mouse a bloquear — agora baseada no mapeamento Warzone
        public static List<Keys> BlockedKeys = new List<Keys> {
            Keys.Space, Keys.C, Keys.F, Keys.D1,           // Cross, Circle, Square, Triangle
            Keys.D3, Keys.D4, Keys.V, Keys.D5,             // DPad Up, Right, Down, Left
            Keys.Q, Keys.E,                                // Shoulder Left, Right
            Keys.ShiftKey, Keys.LButton,                   // Trigger Left, Right
            Keys.LControlKey, Keys.B,                      // Thumb Left, Right
            Keys.Tab, Keys.Escape, Keys.Home, Keys.M       // Special buttons
        };

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private static LowLevelKeyboardProc proc = HookCallback;

        public static void StartHook()
        {
            if (hookId == IntPtr.Zero)
            {
                hookId = SetHook(proc);
            }
        }

        public static void StopHook()
        {
            if (hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(hookId);
                hookId = IntPtr.Zero;
            }
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                int vkCode = Marshal.ReadInt32(lParam);
                Keys key = (Keys)vkCode;

                if (ControllerConnected && BlockedKeys.Contains(key))
                {
                    return (IntPtr)1; // BLOQUEIA
                }
            }

            return CallNextHookEx(hookId, nCode, wParam, lParam);
        }

        public static void AddBlockedKey(Keys key)
        {
            if (!BlockedKeys.Contains(key))
            {
                BlockedKeys.Add(key);
            }
        }

        public static void RemoveBlockedKey(Keys key)
        {
            if (BlockedKeys.Contains(key))
            {
                BlockedKeys.Remove(key);
            }
        }

        public static void ClearBlockedKeys()
        {
            BlockedKeys.Clear();
        }
    }
}
