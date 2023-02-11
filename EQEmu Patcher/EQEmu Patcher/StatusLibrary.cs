using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EQEmu_Patcher
{
    internal class StatusLibrary
    {
        readonly static Mutex mux = new Mutex();
        
        public delegate void ProgressHandler(int value);
        static event ProgressHandler progressChange;
        static int progressValue;

        public delegate void LogAddHandler(string message);
        static event LogAddHandler logAddChange;

        public delegate void PatchStateHandler(bool isPatching);
        static event PatchStateHandler patchStateChange;

        public static int Progress()
        {
            mux.WaitOne();
            int value = progressValue;
            mux.ReleaseMutex();
            return value;
        }

        public static void SetProgress(int value)
        {
            mux.WaitOne();
            progressValue = value;
            progressChange?.BeginInvoke(value, null, null);
            mux.ReleaseMutex();
        }

        public static void SubscribeProgress(ProgressHandler f)
        {
            mux.WaitOne();
            progressChange += f;
            mux.ReleaseMutex();
        }

        public static void Log(string message)
        {
            mux.WaitOne();
            logAddChange?.BeginInvoke(message, null, null);
            mux.ReleaseMutex();
        }

        public static void SubscribeLogAdd(LogAddHandler f)
        {
            mux.WaitOne();
            logAddChange += f;
            mux.ReleaseMutex();
        }

        public static void SetPatchState(bool isPatching)
        {
            mux.WaitOne();
            patchStateChange?.BeginInvoke(isPatching, null, null);
            mux.ReleaseMutex();
        }

        public static void SubscribePatchState(PatchStateHandler f)
        {
            mux.WaitOne();
            patchStateChange += f;
            mux.ReleaseMutex();
        }
    }
}
