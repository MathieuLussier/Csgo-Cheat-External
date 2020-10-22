using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace CsgoHackPlayground
{
    internal class Memory
    {
        public static Process m_Process;
        public static IntPtr m_pProcessHandle;

        public static int m_iNumberOfBytesRead = 0;
        public static int m_iNumberOfBytesWritten = 0;

        public static void Initialize(string ProcessName)
        {
            try
            {
                m_Process = Process.GetProcessesByName(ProcessName)[0];
                m_pProcessHandle = OpenProcess(PROCESS_ALL_ACCESS, false, m_Process.Id);
            }
            catch (Exception ex)
            {
                Console.WriteLine("PLEASE OPEN CSGO FIRST !");
                Console.WriteLine($"Debug: {ex.Message}");
                Console.ReadLine();
            }
        }

        public static T ReadMemory<T>(int Adress) where T : struct
        {
            int ByteSize = Marshal.SizeOf(typeof(T));
            byte[] buffer = new byte[ByteSize];
            ReadProcessMemory((int)m_pProcessHandle, Adress, buffer, buffer.Length, ref m_iNumberOfBytesRead);
            return ByteArrayToStructure<T>(buffer);
        }

        public static float[] ReadMatrix<T>(int Adress, int MatrixSize) where T : struct
        {
            int ByteSize = Marshal.SizeOf(typeof(T));
            byte[] buffer = new byte[ByteSize * MatrixSize];
            ReadProcessMemory((int)m_pProcessHandle, Adress, buffer, buffer.Length, ref m_iNumberOfBytesRead);
            return ConvertToFloatArray(buffer);
        }

        public static void WriteMemory<T>(int Adress, object Value) where T : struct
        {
            byte[] buffer = StructureToByteArray(Value);
            WriteProcessMemory((int)m_pProcessHandle, Adress, buffer, buffer.Length, out m_iNumberOfBytesWritten);
        }

        public static void WriteFloat(int address, float[] value)
        {
            byte[] dataBuffer = ConvertFloatToByteArray(value);
            WriteProcessMemory((int)m_pProcessHandle, address, dataBuffer, dataBuffer.Length, out m_iNumberOfBytesWritten);
        }

        #region Transformation

        static byte[] ConvertFloatToByteArray(float[] floats)
        {
            var byteArray = new byte[floats.Length * 4];
            Buffer.BlockCopy(floats, 0, byteArray, 0, byteArray.Length);
            return byteArray;
        }

        public static float[] ConvertToFloatArray(byte[] bytes)
        {
            if (bytes.Length % 4 != 0)
            {
                throw new ArgumentException();
            }

            float[] floats = new float[bytes.Length / 4];
            for (int i = 0; i < floats.Length; i++)
            {
                floats[i] = BitConverter.ToSingle(bytes, i * 4);
            }
            return floats;
        }

        private static T ByteArrayToStructure<T>(byte[] bytes) where T : struct
        {
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                return (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            }
            finally
            {
                handle.Free();
            }
        }

        private static byte[] StructureToByteArray(object obj)
        {
            int length = Marshal.SizeOf(obj);
            byte[] array = new byte[length];
            IntPtr pointer = Marshal.AllocHGlobal(length);
            Marshal.StructureToPtr(obj, pointer, true);
            Marshal.Copy(pointer, array, 0, length);
            Marshal.FreeHGlobal(pointer);
            return array;
        }
        #endregion

        #region DllImports

        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll")]
        private static extern bool ReadProcessMemory(int hProcess, int lpBaseAddress, byte[] buffer, int size, ref int lpNumberOfBytesRead);

        [DllImport("kernel32.dll")]
        private static extern bool WriteProcessMemory(int hProcess, int lpBaseAddress, byte[] buffer, int size, out int lpNumberOfBytesWritten);
        #endregion

        #region Constants

        const int PROCESS_VM_OPERATION = 0x0008;
        const int PROCESS_VM_READ = 0x0010;
        const int PROCESS_VM_WRITE = 0x0020;
        const int PROCESS_ALL_ACCESS = 0x1F0FFF;

        #endregion
    }
}
