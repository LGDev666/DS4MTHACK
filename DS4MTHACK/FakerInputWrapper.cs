using System;
using System.Runtime.InteropServices;

namespace DS4MTHACK
{
    public class FakerInputWrapper : IDisposable
    {
        private IntPtr _deviceHandle = IntPtr.Zero;
        private bool _disposed = false;

        // DLL Imports para FakerInput - usando nomes obfuscados
        [DllImport("wininput_helper64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "Helper_CreateDevice")]
        private static extern IntPtr CreateDevice(ushort vendorId, ushort productId, ushort versionNumber, 
            ushort usagePage, ushort usageId, string serialNumber, string devicePath);

        [DllImport("wininput_helper64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "Helper_DestroyDevice")]
        private static extern bool DestroyDevice(IntPtr deviceHandle);

        [DllImport("wininput_helper64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "Helper_UpdateDevice")]
        private static extern bool UpdateDevice(IntPtr deviceHandle);

        [DllImport("wininput_helper64.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "Helper_SetDeviceData")]
        private static extern bool SetDeviceData(IntPtr deviceHandle, byte[] buffer, int length);

        public bool IsInitialized => _deviceHandle != IntPtr.Zero;

        public bool CreateDS4HidDevice()
        {
            if (IsInitialized)
                return true;

            // Parâmetros para o DS4
            ushort vendorId = 0x054C;    // Sony
            ushort productId = 0x09CC;   // DualShock 4
            ushort versionNumber = 0x0100;
            ushort usagePage = 0x01;     // Generic Desktop Controls
            ushort usageId = 0x05;       // Game Pad
            string serialNumber = "FA:KE:DS:4H:ID";
            string devicePath = "FakerDS4Path";

            _deviceHandle = CreateDevice(vendorId, productId, versionNumber, 
                usagePage, usageId, serialNumber, devicePath);

            return IsInitialized;
        }

        public bool UpdateHidDeviceState(byte[] reportData)
        {
            if (!IsInitialized)
                return false;

            bool result = SetDeviceData(_deviceHandle, reportData, reportData.Length);
            if (result)
                result = UpdateDevice(_deviceHandle);

            return result;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (IsInitialized)
                {
                    DestroyDevice(_deviceHandle);
                    _deviceHandle = IntPtr.Zero;
                }
                _disposed = true;
            }
        }

        ~FakerInputWrapper()
        {
            Dispose(false);
        }
    }
}