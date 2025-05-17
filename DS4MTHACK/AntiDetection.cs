using System;
using System.IO;
using System.Runtime.InteropServices;

namespace DS4MTHACK
{
    public static class AntiDetection
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll")]
        private static extern bool FreeLibrary(IntPtr hModule);

        private delegate int InitializeDelegate();
        private delegate bool InstallDriverDelegate(string driverPath);
        private delegate bool HookApiDelegate(string targetModule, string targetFunction, string hookModule, string hookFunction);
        private delegate bool UnhookApiDelegate(string targetModule, string targetFunction);

        private static IntPtr _dllHandle = IntPtr.Zero;
        private static InitializeDelegate _initialize;
        private static InstallDriverDelegate _installDriver;
        private static HookApiDelegate _hookApi;
        private static UnhookApiDelegate _unhookApi;

        public static bool InitializeAntiDetection(string driverPath, string dllPath)
        {
            try
            {
                // Verificar se os arquivos existem
                if (!File.Exists(driverPath) || !File.Exists(dllPath))
                {
                    Console.WriteLine("Arquivos de anti-detecção não encontrados.");
                    return false;
                }

                // Carregar a DLL
                _dllHandle = LoadLibrary(dllPath);
                if (_dllHandle == IntPtr.Zero)
                {
                    Console.WriteLine($"Falha ao carregar a DLL: {Marshal.GetLastWin32Error()}");
                    return false;
                }

                // Obter os endereços das funções
                IntPtr initializePtr = GetProcAddress(_dllHandle, "WinHelper_Init");
                IntPtr installDriverPtr = GetProcAddress(_dllHandle, "WinHelper_InstallDriver");
                IntPtr hookApiPtr = GetProcAddress(_dllHandle, "WinHelper_HookApi");
                IntPtr unhookApiPtr = GetProcAddress(_dllHandle, "WinHelper_UnhookApi");

                if (initializePtr == IntPtr.Zero || installDriverPtr == IntPtr.Zero ||
                    hookApiPtr == IntPtr.Zero || unhookApiPtr == IntPtr.Zero)
                {
                    Console.WriteLine("Falha ao obter os endereços das funções.");
                    FreeLibrary(_dllHandle);
                    _dllHandle = IntPtr.Zero;
                    return false;
                }

                // Criar os delegados
                _initialize = Marshal.GetDelegateForFunctionPointer<InitializeDelegate>(initializePtr);
                _installDriver = Marshal.GetDelegateForFunctionPointer<InstallDriverDelegate>(installDriverPtr);
                _hookApi = Marshal.GetDelegateForFunctionPointer<HookApiDelegate>(hookApiPtr);
                _unhookApi = Marshal.GetDelegateForFunctionPointer<UnhookApiDelegate>(unhookApiPtr);

                // Inicializar a DLL
                int result = _initialize();
                if (result != 0)
                {
                    Console.WriteLine($"Falha ao inicializar a DLL: {result}");
                    FreeLibrary(_dllHandle);
                    _dllHandle = IntPtr.Zero;
                    return false;
                }

                // Instalar o driver
                if (!_installDriver(driverPath))
                {
                    Console.WriteLine("Falha ao instalar o driver.");
                    FreeLibrary(_dllHandle);
                    _dllHandle = IntPtr.Zero;
                    return false;
                }

                // Instalar os hooks de API
                InstallApiHooks();

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao inicializar anti-detecção: {ex.Message}");
                return false;
            }
        }

        private static void InstallApiHooks()
        {
            if (_hookApi == null) return;

            // Hook para NtQuerySystemInformation
            _hookApi("ntdll.dll", "NtQuerySystemInformation", "wininput_helper64.dll", "HookedNtQuerySystemInformation");

            // Hook para CreateFileA/W
            _hookApi("kernel32.dll", "CreateFileA", "wininput_helper64.dll", "HookedCreateFileA");
            _hookApi("kernel32.dll", "CreateFileW", "wininput_helper64.dll", "HookedCreateFileW");

            // Hook para DeviceIoControl
            _hookApi("kernel32.dll", "DeviceIoControl", "wininput_helper64.dll", "HookedDeviceIoControl");

            // Hook para funções de detecção de debugger
            _hookApi("kernel32.dll", "IsDebuggerPresent", "wininput_helper64.dll", "HookedIsDebuggerPresent");
            _hookApi("kernel32.dll", "CheckRemoteDebuggerPresent", "wininput_helper64.dll", "HookedCheckRemoteDebuggerPresent");
        }

        public static void RemoveApiHooks()
        {
            if (_unhookApi == null || _dllHandle == IntPtr.Zero) return;

            // Remover hooks
            _unhookApi("ntdll.dll", "NtQuerySystemInformation");
            _unhookApi("kernel32.dll", "CreateFileA");
            _unhookApi("kernel32.dll", "CreateFileW");
            _unhookApi("kernel32.dll", "DeviceIoControl");
            _unhookApi("kernel32.dll", "IsDebuggerPresent");
            _unhookApi("kernel32.dll", "CheckRemoteDebuggerPresent");

            // Liberar a DLL
            FreeLibrary(_dllHandle);
            _dllHandle = IntPtr.Zero;
        }
    }
}
