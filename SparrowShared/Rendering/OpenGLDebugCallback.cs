﻿using System;
using System.Diagnostics;
using OpenGL;
using System.Runtime.InteropServices;

namespace Sparrow.Core
{
    
    class OpenGLDebugCallback
    {

        private static OpenGLDebugCallback _instance;
        private readonly Gl.DebugProc _pcCallbackInstance = PCCallbackHandler; // The callback delegate must be stored to avoid GC

        internal static void Init()
        {
            if (_instance == null)
            {
                _instance = new OpenGLDebugCallback();
            }
        }

        private OpenGLDebugCallback()
        {
            try
            {
                Gl.Enable((EnableCap)Gl.DEBUG_OUTPUT_SYNCHRONOUS);
                Gl.DebugMessageCallback(_pcCallbackInstance, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                // Google was lazy to implement this in Android 5 and 6, even though the drivers support it.
                Debug.WriteLine("WARNING: No support for OpenGL debug callback, likely its not implemented. " + ex);
            }
        }

        static void PCCallbackHandler(DebugSource source, DebugType type, uint id, DebugSeverity severity, int length, IntPtr message, IntPtr userParam)
        {
            string msg = Marshal.PtrToStringAnsi(message);
            if (severity == DebugSeverity.DebugSeverityHigh || severity == DebugSeverity.DebugSeverityMedium)
            {
                Debug.WriteLine("[GL] {0}; {1}; {2}; {3}; {4}", source, type, id, severity, msg);
            }
            else if (severity == DebugSeverity.DebugSeverityLow)
            {
                Debug.WriteLine("[GL] {0}; {1}; {2}; {3}; {4}", source, type, id, severity, msg);
            }
        }
    }

}
