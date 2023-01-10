///<summary>
///  Copyright (c) https://github.com/TheMostDifficultStep
///
///  Permission is hereby granted, free of charge, to any person obtaining
///  a copy of this software and associated documentation files (the
///  "Software"), to deal in the Software without restriction, including
///  without limitation the rights to use, copy, modify, merge, publish,
///  distribute, sublicense, and/or sell copies of the Software, and to
///  permit persons to whom the Software is furnished to do so, subject to
///  the following conditions:
///
///  The above copyright notice and this permission notice shall be
///  included in all copies or substantial portions of the Software.
///
///  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
///  EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
///  MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
///  NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS
///  BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN
///  ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
///  CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
///  SOFTWARE
///</summary>

// https://docs.microsoft.com/en-us/dotnet/framework/interop/marshaling-classes-structures-and-unions

// MSDN Audio Streaming Sample Code
// https://msdn.microsoft.com/en-us/library/windows/desktop/dd317593(v=vs.85).aspx

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Globalization;

namespace Play.Sound {
	unsafe public class WmmReader {
        [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Winapi)]
			public static extern MMSYSERROR waveInOpen(ref IntPtr phwo, int uDeviceID, ref WAVEFORMATEX oFmt, WaveOutProc dwCallback, IntPtr dwCallbackInstance, WaveOpenFlags dwFlags);
        [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Winapi)]
			public static extern MMSYSERROR waveInClose( IntPtr hHandle );
        [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Winapi)]
			public static extern MMSYSERROR waveInPrepareHeader(IntPtr hwo, IntPtr ipHeader, int cbwh);
        [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Winapi)]
			public static extern MMSYSERROR waveInUnprepareHeader(IntPtr hwo, IntPtr ipHeader, int cbwh);
        [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Winapi)]
			public static extern MMSYSERROR waveInRead(IntPtr hwo, IntPtr ipHeader, int cbwh);
        [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Winapi)]
			public static extern MMSYSERROR waveInReset(IntPtr hwo);


        public WmmReader( Specification oSpec, int iDeviceID ) {
        }
    }
}