//-----------------------------------------------------------------------
// <copyright file="NativeMethods.cs" company="(none)">
//  Copyright © 2010 John Gietzen
//
//  Permission is hereby granted, free of charge, to any person obtaining
//  a copy of this software and associated documentation files (the
//  "Software"), to deal in the Software without restriction, including
//  without limitation the rights to use, copy, modify, merge, publish,
//  distribute, sublicense, and/or sell copies of the Software, and to
//  permit persons to whom the Software is furnished to do so, subject to
//  the following conditions:
//
//  The above copyright notice and this permission notice shall be
//  included in all copies or substantial portions of the Software.
//
//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
//  EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
//  MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
//  NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS
//  BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN
//  ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
//  CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//  SOFTWARE
// </copyright>
// <author>John Gietzen</author>
//-----------------------------------------------------------------------

/// <summary>
/// Copyright 2018 https://github.com/TheMostDifficultStep
/// I lifted these enumerations from John Gietzen's WinMM.Net source. That's a lot of
/// work and there's no use repeating that. But I stripped out the function declarations
/// and all the joy stick stuff which I'd want in a separate file anyway.
/// </summary>

namespace Play.Sound {
    using System;
    using System.Runtime.InteropServices;

	public enum ErrorSource {
		WaveIn,
		WaveOut,
	}

    /// <summary>
    /// MMSystemException is thrown as a generic exception for errors in WinMM.
    /// </summary>
    [global::System.Serializable]
    public class MMSystemException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the MMSystemException class.
        /// </summary>
        public MMSystemException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the MMSystemException class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public MMSystemException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the MMSystemException class with a specified error
        /// message and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="inner">The exception that is the cause of the current exception, or a null reference (Nothing in Visual Basic) if no inner exception is specified.</param>
        public MMSystemException(string message, Exception inner)
            : base(message, inner)
        {
        }

        /// <summary>
        /// Initializes a new instance of the MMSystemException class with serialized data.
        /// </summary>
        /// <param name="info">The System.Runtime.Serialization.SerializationInfo that holds the serialized object data about the exception being thrown.</param>
        /// <param name="context">The System.Runtime.Serialization.StreamingContext that contains contextual information about the source or destination.</param>
        /// <exception cref="System.ArgumentNullException">The info parameter is null.</exception>
        /// <exception cref="System.Runtime.Serialization.SerializationException">The class name is null or System.Exception.HResult is zero (0).</exception>
        protected MMSystemException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
            : base(info, context)
        {
        }
    }
	
	/// <summary>
    /// Thrown when a WinMM error of type MMSYSERR_BADDEVICEID is returned.
    /// </summary>
    [global::System.Serializable]
    public class BadDeviceIdException : MMSystemException
    {
        /// <summary>
        /// Initializes a new instance of the BadDeviceIdException class.
        /// </summary>
        public BadDeviceIdException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the BadDeviceIdException class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public BadDeviceIdException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the BadDeviceIdException class with a specified error
        /// message and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="inner">The exception that is the cause of the current exception, or a null reference (Nothing in Visual Basic) if no inner exception is specified.</param>
        public BadDeviceIdException(string message, Exception inner)
            : base(message, inner)
        {
        }

        /// <summary>
        /// Initializes a new instance of the BadDeviceIdException class with serialized data.
        /// </summary>
        /// <param name="info">The System.Runtime.Serialization.SerializationInfo that holds the serialized object data about the exception being thrown.</param>
        /// <param name="context">The System.Runtime.Serialization.StreamingContext that contains contextual information about the source or destination.</param>
        /// <exception cref="System.ArgumentNullException">The info parameter is null.</exception>
        /// <exception cref="System.Runtime.Serialization.SerializationException">The class name is null or System.Exception.HResult is zero (0).</exception>
        protected BadDeviceIdException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
            : base(info, context)
        {
        }
    }


    /// <summary>
    /// Thrown when a WinMM error of type MMSYSERR_INVALHANDLE is returned.
    /// </summary>
    [global::System.Serializable]
    public class InvalidHandleException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the InvalidHandleException class.
        /// </summary>
        public InvalidHandleException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the InvalidHandleException class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public InvalidHandleException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the InvalidHandleException class with a specified error
        /// message and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="inner">The exception that is the cause of the current exception, or a null reference (Nothing in Visual Basic) if no inner exception is specified.</param>
        public InvalidHandleException(string message, Exception inner)
            : base(message, inner)
        {
        }

        /// <summary>
        /// Initializes a new instance of the InvalidHandleException class with serialized data.
        /// </summary>
        /// <param name="info">The System.Runtime.Serialization.SerializationInfo that holds the serialized object data about the exception being thrown.</param>
        /// <param name="context">The System.Runtime.Serialization.StreamingContext that contains contextual information about the source or destination.</param>
        /// <exception cref="System.ArgumentNullException">The info parameter is null.</exception>
        /// <exception cref="System.Runtime.Serialization.SerializationException">The class name is null or System.Exception.HResult is zero (0).</exception>
        protected InvalidHandleException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
            : base(info, context)
        {
        }
    }

    /// <summary>
    /// The waveOutProc function is the callback function used with the waveform-audio output device. The waveOutProc
    /// function is a placeholder for the application-defined function name. The address of this function can be specified
    /// in the callback-address parameter of the waveOutOpen function.
    /// </summary>
    /// <param name="hwo">Handle to the waveform-audio device associated with the callback.</param>
    /// <param name="uMsg">
    /// Waveform-audio output message. It can be one of the following values.
    /// <list type="Messages">
    /// <item>WOM_CLOSE (Sent when the device is closed using the waveOutClose function.)</item>
    /// <item>WOM_DONE (Sent when the device driver is finished with a data block sent using the waveOutWrite function.)</item>
    /// <item>WOM_OPEN (Sent when the device is opened using the waveOutOpen function.)</item>
    /// </list>
    /// </param>
    /// <param name="dwInstance">User-instance data specified with waveOutOpen.</param>
    /// <param name="dwParam1">Message parameter one.</param>
    /// <param name="dwParam2">Message parameter two.</param>
    /// <remarks>
    /// Applications should not call any system-defined functions from inside a callback function, except for
    /// <list type="Acceptable Calls">
    /// <item>EnterCriticalSection</item>
    /// <item>LeaveCriticalSection</item>
    /// <item>midiOutLongMsg</item>
    /// <item>midiOutShortMsg</item>
    /// <item>OutputDebugString</item>
    /// <item>PostMessage</item>
    /// <item>PostThreadMessage</item>
    /// <item>SetEvent</item>
    /// <item>timeGetSystemTime</item>
    /// <item>timeGetTime</item>
    /// <item>timeKillEvent</item>
    /// <item>timeSetEvent</item>
    /// </list>
    /// Calling other wave functions will cause deadlock.
    /// </remarks>
    public delegate void WaveOutProc(IntPtr hwo, WaveOutMessage uMsg, IntPtr dwInstance, IntPtr dwParam1, IntPtr dwParam2);

    /// <summary>
    /// The waveInProc function is the callback function used with the waveform-audio input device. This function is a placeholder for the application-defined function name. The address of this function can be specified in the callback-address parameter of the waveInOpen function.
    /// </summary>
    /// <param name="hwi">Handle to the waveform-audio device associated with the callback function.</param>
    /// <param name="uMsg">Waveform-audio input message.</param>
    /// <param name="dwInstance">User instance data specified with waveInOpen.</param>
    /// <param name="dwParam1">Message parameter one.</param>
    /// <param name="dwParam2">Message parameter two.</param>
    public delegate void WaveInProc(IntPtr hwi, WaveInMessage uMsg, IntPtr dwInstance, IntPtr dwParam1, IntPtr dwParam2);

    /// <summary>
    /// The WAVEOUTCAPS structure describes the capabilities of a waveform-audio output device.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct WAVEOUTCAPS
    {
        /// <summary>
        /// Specifies the manufacturer id of the device.
        /// </summary>
        public short wMid;

        /// <summary>
        /// Specifies the product id of the device.
        /// </summary>
        public short wPid;

        /// <summary>
        /// Specifies the version of the device's driver.
        /// </summary>
        public int vDriverVersion;

        /// <summary>
        /// Specifies the name of the device.
        /// </summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szPname;

        /// <summary>
        /// Specifies the WAVE formats the device supports.
        /// </summary>
        public WaveFormats dwFormats;

        /// <summary>
        /// Specifies the number of channels the device supports.
        /// </summary>
        public short wChannels;

        /// <summary>
        /// Unused.  Padding.
        /// </summary>
        public short wReserved1;

        /// <summary>
        /// Specifies the features that the device supports.
        /// </summary>
        public WAVECAPS dwSupport;
    }

    /// <summary>
    /// The WAVEINCAPS structure describes the capabilities of a waveform-audio input device.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct WAVEINCAPS
    {
        /// <summary>
        /// The ManufacturerID.
        /// </summary>
        public short wMid;

        /// <summary>
        /// The ProductID.
        /// </summary>
        public short wPid;

        /// <summary>
        /// The device's driver version.
        /// </summary>
        public int vDriverVersion;

        /// <summary>
        /// The name of the device.
        /// </summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szPname;

        /// <summary>
        /// The formats the device supports.
        /// </summary>
        public int dwFormats;

        /// <summary>
        /// The number of channels the device supports.
        /// </summary>
        public short wChannels;

        /// <summary>
        /// Reserved for internal use.
        /// </summary>
        public short wReserved1;
    }

    /// <summary>
    /// The WAVEHDR structure defines the header used to identify a waveform-audio buffer.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct WAVEHDR
    {
        /// <summary>
        /// Pointer to the waveform buffer.
        /// </summary>
        public IntPtr lpData;

        /// <summary>
        /// Length, in bytes, of the buffer.
        /// </summary>
        public int dwBufferLength;

        /// <summary>
        /// When the header is used in input, this member specifies how much data is in the buffer.
        /// </summary>
        public int dwBytesRecorded;

        /// <summary>
        /// User data.
        /// </summary>
        public IntPtr dwUser;

        /// <summary>
        /// Flags supplying information about the buffer. The following values are defined:
        /// </summary>
        public int dwFlags;

        /// <summary>
        /// Number of times to play the loop. This member is used only with output buffers.
        /// </summary>
        public int dwLoops;

        /// <summary>
        /// Reserved for internal use.
        /// </summary>
        public IntPtr lpNext;

        /// <summary>
        /// Reserved for internal use.
        /// </summary>
        public int reserved;
    }

    /// <summary>
    /// Describes the full format of a wave formatted stream.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct WAVEFORMATEX
    {
        /// <summary>
        /// The wave format of the stream.
        /// </summary>
        public short wFormatTag;

        /// <summary>
        /// The number of channels.
        /// </summary>
        public short nChannels;

        /// <summary>
        /// The number of samples per second.
        /// </summary>
        public int nSamplesPerSec;

        /// <summary>
        /// The average bytes per second.
        /// </summary>
        public int nAvgBytesPerSec;

        /// <summary>
        /// The smallest atomic data size.
        /// </summary>
        public short nBlockAlign;

        /// <summary>
        /// The number of bits per sample.
        /// </summary>
        public short wBitsPerSample;

        /// <summary>
        /// The remaining header size. (Must be zero in this struct format.)
        /// </summary>
        public short cbSize;
    }

    /// <summary>
    /// Describes the full format of a wave formatted stream.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct WAVEFORMATEXTENSIBLE
    {
        /// <summary>
        /// The wave format of the stream.
        /// </summary>
        public short wFormatTag;

        /// <summary>
        /// The number of channels.
        /// </summary>
        public short nChannels;

        /// <summary>
        /// The number of samples per second.
        /// </summary>
        public int nSamplesPerSec;

        /// <summary>
        /// The average bytes per second.
        /// </summary>
        public int nAvgBytesPerSec;

        /// <summary>
        /// The smallest atomic data size.
        /// </summary>
        public short nBlockAlign;

        /// <summary>
        /// The number of bits per sample.
        /// </summary>
        public short wBitsPerSample;

        /// <summary>
        /// The remaining header size.
        /// </summary>
        public short cbSize;

        /// <summary>
        /// The number of valid bits per sample.
        /// </summary>
        public short wValidBitsPerSample;

        /// <summary>
        /// The channel mask.
        /// </summary>
        public int dwChannelMask;

        /// <summary>
        /// The sub format identifier.
        /// </summary>
        public Guid SubFormat;
    }

    /// <summary>
    /// The MMTIME structure contains timing information for different types of multimedia data.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct MMTIME
    {
        /// <summary>
        /// Time format.
        /// </summary>
        public int wType;

        /// <summary>
        /// The first part of the data.
        /// </summary>
        public int wData1;

        /// <summary>
        /// The second part of the data.
        /// </summary>
        public int wData2;
    }

    /// <summary>
    /// Used with the <see cref="NativeMethods.waveOutOpen"/> command.
    /// Probably from winmm.h
    /// </summary>
    [Flags]
    public enum WaveOpenFlags
    {
        /// <summary>
        /// No callback mechanism. This is the default setting.
        /// </summary>
        CALLBACK_NULL = 0x00000,

        /// <summary>
        /// Indicates the dwCallback parameter is a window handle.
        /// </summary>
        CALLBACK_WINDOW = 0x10000,

        /// <summary>
        /// The dwCallback parameter is a thread identifier.
        /// </summary>
        CALLBACK_THREAD = 0x20000,

        /// <summary>
        /// The dwCallback parameter is a thread identifier.
        /// </summary>
        [Obsolete]
        CALLBACK_TASK = 0x20000,

        /// <summary>
        /// The dwCallback parameter is a callback procedure address.
        /// </summary>
        CALLBACK_FUNCTION = 0x30000,

        CALLBACK_EVENT = 0x50000,

        /// <summary>
        /// If this flag is specified, <see cref="NativeMethods.waveOutOpen"/> queries the device to determine if it supports the given format, but the device is not actually opened.
        /// </summary>
        WAVE_FORMAT_QUERY = 0x00001,

        /// <summary>
        /// If this flag is specified, a synchronous waveform-audio device can be opened. If this flag is not specified while opening a synchronous driver, the device will fail to open.
        /// </summary>
        WAVE_ALLOWSYNC = 0x00002,

        /// <summary>
        /// If this flag is specified, the uDeviceID parameter specifies a waveform-audio device to be mapped to by the wave mapper.
        /// </summary>
        WAVE_MAPPED = 0x00004,

        /// <summary>
        /// If this flag is specified, the ACM driver does not perform conversions on the audio data.
        /// </summary>
        WAVE_FORMAT_DIRECT = 0x00008
    }

    /// <summary>
    /// Flags supplying information about the buffer. The following values are defined:
    /// </summary>
    [Flags]
    public enum WaveHeaderFlags
    {
        /// <summary>
        /// This buffer is the first buffer in a loop.  This flag is used only with output buffers.
        /// </summary>
        BeginLoop = 0x00000004,

        /// <summary>
        /// Set by the device driver to indicate that it is finished with the buffer and is returning it to the application.
        /// </summary>
        Done = 0x00000001,

        /// <summary>
        /// This buffer is the last buffer in a loop.  This flag is used only with output buffers.
        /// </summary>
        EndLoop = 0x00000008,

        /// <summary>
        /// Set by Windows to indicate that the buffer is queued for playback.
        /// </summary>
        InQueue = 0x00000010,

        /// <summary>
        /// Set by Windows to indicate that the buffer has been prepared with the waveInPrepareHeader or waveOutPrepareHeader function.
        /// </summary>
        Prepared = 0x00000002
    }

 
    /// <summary>
    /// Indicates a WaveOut message.
    /// </summary>
    public enum WaveOutMessage
    {
        /// <summary>
        /// Not Used.  Indicates that there is no message.
        /// </summary>
        None = 0x000,

        /// <summary>
        /// Indicates that the device has been opened.
        /// </summary>
        DeviceOpened = 0x3BB,

        /// <summary>
        /// Indicates that the device has been closed.
        /// </summary>
        DeviceClosed = 0x3BC,

        /// <summary>
        /// Indicates that playback of a write operation has been completed.
        /// </summary>
        WriteDone = 0x3BD
    }

    /// <summary>
    /// Indicates a WaveIn message.
    /// </summary>
    public enum WaveInMessage
    {
        /// <summary>
        /// Not Used.  Indicates that there is no message.
        /// </summary>
        None = 0x000,

        /// <summary>
        /// Indicates that the device has been opened.
        /// </summary>
        DeviceOpened = 0x3BE,

        /// <summary>
        /// Indicates that the device has been closed.
        /// </summary>
        DeviceClosed = 0x3BF,

        /// <summary>
        /// Indicates that playback of a write operation has been completed.
        /// </summary>
        DataReady = 0x3C0
    }

    /// <summary>
    /// Indicates a wave data sample format.
    /// </summary>
    public enum WaveFormatTag
    {
        /// <summary>
        /// Indicates an invalid sample format.
        /// </summary>
        Invalid = 0x00,

        /// <summary>
        /// Indicates raw Pulse Code Modulation data.
        /// </summary>
        Pcm = 0x01,

        /// <summary>
        /// Indicates Adaptive Differential Pulse Code Modulation data.
        /// </summary>
        Adpcm = 0x02,

        /// <summary>
        /// Indicates IEEE-Float data.
        /// </summary>
        Float = 0x03,

        /// <summary>
        /// Indicates a-law companded data.
        /// </summary>
        ALaw = 0x06,

        /// <summary>
        /// Indicates μ-law  companded data.
        /// </summary>
        MuLaw = 0x07,
    }

    /// <summary>
    /// Describes a variety of channels, frequencies, and bit-depths by which a wave signal may be expressed.
    /// </summary>
    [Flags]
    public enum WaveFormats
    {
        /// <summary>
        /// Monaural, 8-bit, 11025 Hz
        /// </summary>
        Mono8Bit11Khz = 1,

        /// <summary>
        /// Stereo, 8-bit, 11025 Hz
        /// </summary>
        Stereo8Bit11Khz = 2,

        /// <summary>
        /// Monaural, 16-bit, 11025 Hz
        /// </summary>
        Mono16Bit11Khz = 4,

        /// <summary>
        /// Stereo, 16-bit, 11025 Hz
        /// </summary>
        Stereo16Bit11Khz = 8,

        /// <summary>
        /// Monaural, 8-bit, 22050 Hz
        /// </summary>
        Mono8Bit22Khz = 16,

        /// <summary>
        /// Stereo, 8-bit, 22050 Hz
        /// </summary>
        Stereo8Bit22Khz = 32,

        /// <summary>
        /// Monaural, 16-bit, 22050 Hz
        /// </summary>
        Mono16Bit22Khz = 64,

        /// <summary>
        /// Stereo, 16-bit, 22050 Hz
        /// </summary>
        Stereo16Bit22Khz = 128,

        /// <summary>
        /// Monaural, 8-bit, 44100 Hz
        /// </summary>
        Mono8Bit44Khz = 256,

        /// <summary>
        /// Stereo, 8-bit, 44100 Hz
        /// </summary>
        Stereo8Bit44Khz = 512,

        /// <summary>
        /// Monaural, 16-bit, 44100 Hz
        /// </summary>
        Mono16Bit44Khz = 1024,

        /// <summary>
        /// Stereo, 16-bit, 44100 Hz
        /// </summary>
        Stereo16Bit44Khz = 2048,

        /// <summary>
        /// Monaural, 8-bit, 48000 Hz
        /// </summary>
        Mono8Bit48Khz = 4096,

        /// <summary>
        /// Stereo, 8-bit, 48000 Hz
        /// </summary>
        Stereo8Bit48Khz = 8192,

        /// <summary>
        /// Monaural, 16-bit, 48000 Hz
        /// </summary>
        Mono16Bit48Khz = 16384,

        /// <summary>
        /// Stereo, 16-bit, 48000 Hz
        /// </summary>
        Stereo16Bit48Khz = 32768,

        /// <summary>
        /// Monaural, 8-bit, 96000 Hz
        /// </summary>
        Mono8Bit96Khz = 65536,

        /// <summary>
        /// Stereo, 8-bit, 96000 Hz
        /// </summary>
        Stereo8Bit96Khz = 131072,

        /// <summary>
        /// Monaural, 16-bit, 96000 Hz
        /// </summary>
        Mono16Bit96Khz = 262144,

        /// <summary>
        /// Stereo, 16-bit, 96000 Hz
        /// </summary>
        Stereo16Bit96Khz = 524288,
    }
	/// <summary>
    /// Used as a return result from many of the WinMM calls.
    /// </summary>
    public enum MMSYSERROR
    {
        /// <summary>
        /// No Error. (Success)
        /// </summary>
        MMSYSERR_NOERROR = 0,

        /// <summary>
        /// Unspecified Error.
        /// </summary>
        MMSYSERR_ERROR = 1,

        /// <summary>
        /// Device ID out of range.
        /// </summary>
        MMSYSERR_BADDEVICEID = 2,

        /// <summary>
        /// Driver failed enable.
        /// </summary>
        MMSYSERR_NOTENABLED = 3,

        /// <summary>
        /// Device is already allocated.
        /// </summary>
        MMSYSERR_ALLOCATED = 4,

        /// <summary>
        /// Device handle is invalid.
        /// </summary>
        MMSYSERR_INVALHANDLE = 5,

        /// <summary>
        /// No device driver is present.
        /// </summary>
        MMSYSERR_NODRIVER = 6,

        /// <summary>
        /// In sufficient memory, or memory allocation error.
        /// </summary>
        MMSYSERR_NOMEM = 7,

        /// <summary>
        /// Unsupported function.
        /// </summary>
        MMSYSERR_NOTSUPPORTED = 8,

        /// <summary>
        /// Error value out of range.
        /// </summary>
        MMSYSERR_BADERRNUM = 9,

        /// <summary>
        /// Invalid flag passed.
        /// </summary>
        MMSYSERR_INVALFLAG = 10,

        /// <summary>
        /// Invalid parameter passed.
        /// </summary>
        MMSYSERR_INVALPARAM = 11,

        /// <summary>
        /// Handle being used simultaneously on another thread.
        /// </summary>
        MMSYSERR_HANDLEBUSY = 12,

        /// <summary>
        /// Specified alias not found.
        /// </summary>
        MMSYSERR_INVALIDALIAS = 13,

        /// <summary>
        /// Bad registry database.
        /// </summary>
        MMSYSERR_BADDB = 14,

        /// <summary>
        /// Registry key not found.
        /// </summary>
        MMSYSERR_KEYNOTFOUND = 15,

        /// <summary>
        /// Registry read error.
        /// </summary>
        MMSYSERR_READERROR = 16,

        /// <summary>
        /// Registry write error.
        /// </summary>
        MMSYSERR_WRITEERROR = 17,

        /// <summary>
        /// Registry delete error.
        /// </summary>
        MMSYSERR_DELETEERROR = 18,

        /// <summary>
        /// Registry value not found.
        /// </summary>
        MMSYSERR_VALNOTFOUND = 19,

        /// <summary>
        /// Driver does not call DriverCallback.
        /// </summary>
        MMSYSERR_NODRIVERCB = 20,

        /// <summary>
        /// More data to be returned.
        /// </summary>
        MMSYSERR_MOREDATA = 21,

        /// <summary>
        /// Unsupported wave format.
        /// </summary>
        WAVERR_BADFORMAT = 32,

        /// <summary>
        /// Still something playing.
        /// </summary>
        WAVERR_STILLPLAYING = 33,

        /// <summary>
        /// Header not prepared.
        /// </summary>
        WAVERR_UNPREPARED = 34,

        /// <summary>
        /// Device is syncronus.
        /// </summary>
        WAVERR_SYNC = 35,

        /// <summary>
        /// Header not prepared.
        /// </summary>
        MIDIERR_UNPREPARED = 64,

        /// <summary>
        /// Still something playing.
        /// </summary>
        MIDIERR_STILLPLAYING = 65,

        /// <summary>
        /// No configured instruments.
        /// </summary>
        MIDIERR_NOMAP = 66,

        /// <summary>
        /// Hardware is still busy.
        /// </summary>
        MIDIERR_NOTREADY = 67,

        /// <summary>
        /// Port no longer connected
        /// </summary>
        MIDIERR_NODEVICE = 68,

        /// <summary>
        /// Invalid MIF
        /// </summary>
        MIDIERR_INVALIDSETUP = 69,

        /// <summary>
        /// Operation unsupported with open mode.
        /// </summary>
        MIDIERR_BADOPENMODE = 70,

        /// <summary>
        /// Thru device 'eating' a message
        /// </summary>
        MIDIERR_DONT_CONTINUE = 71,

        /// <summary>
        /// The resolution specified in uPeriod is out of range.
        /// </summary>
        TIMERR_NOCANDO = 96 + 1,

        /// <summary>
        /// Time struct size
        /// </summary>
        TIMERR_STRUCT = 96 + 33,

        /// <summary>
        /// Bad parameters
        /// </summary>
        JOYERR_PARMS = 160 + 5,

        /// <summary>
        /// Request not completed
        /// </summary>
        JOYERR_NOCANDO = 160 + 6,

        /// <summary>
        /// Joystick is unplugged
        /// </summary>
        JOYERR_UNPLUGGED = 160 + 7,

        /// <summary>
        /// Invalid device ID
        /// </summary>
        MCIERR_INVALID_DEVICE_ID = 256 + 1,

        /// <summary>
        /// Unrecognized keyword.
        /// </summary>
        MCIERR_UNRECOGNIZED_KEYWORD = 256 + 3,

        /// <summary>
        /// Unrecognized command
        /// </summary>
        MCIERR_UNRECOGNIZED_COMMAND = 256 + 5,

        /// <summary>
        /// Hardware error
        /// </summary>
        MCIERR_HARDWARE = 256 + 6,

        /// <summary>
        /// Invalid device name
        /// </summary>
        MCIERR_INVALID_DEVICE_NAME = 256 + 7,

        /// <summary>
        /// Out of memory
        /// </summary>
        MCIERR_OUT_OF_MEMORY = 256 + 8,

        MCIERR_DEVICE_OPEN = 256 + 9,

        MCIERR_CANNOT_LOAD_DRIVER = 256 + 10,

        MCIERR_MISSING_COMMAND_STRING = 256 + 11,

        MCIERR_PARAM_OVERFLOW = 256 + 12,

        MCIERR_MISSING_STRING_ARGUMENT = 256 + 13,

        MCIERR_BAD_INTEGER = 256 + 14,

        MCIERR_PARSER_INTERNAL = 256 + 15,

        MCIERR_DRIVER_INTERNAL = 256 + 16,

        MCIERR_MISSING_PARAMETER = 256 + 17,

        MCIERR_UNSUPPORTED_FUNCTION = 256 + 18,

        MCIERR_FILE_NOT_FOUND = 256 + 19,

        MCIERR_DEVICE_NOT_READY = 256 + 20,

        MCIERR_INTERNAL = 256 + 21,

        MCIERR_DRIVER = 256 + 22,

        MCIERR_CANNOT_USE_ALL = 256 + 23,

        MCIERR_MULTIPLE = 256 + 24,

        MCIERR_EXTENSION_NOT_FOUND = 256 + 25,

        MCIERR_OUTOFRANGE = 256 + 26,

        MCIERR_FLAGS_NOT_COMPATIBLE = 256 + 28,

        MCIERR_FILE_NOT_SAVED = 256 + 30,

        MCIERR_DEVICE_TYPE_REQUIRED = 256 + 31,

        MCIERR_DEVICE_LOCKED = 256 + 32,

        MCIERR_DUPLICATE_ALIAS = 256 + 33,

        MCIERR_BAD_CONSTANT = 256 + 34,

        MCIERR_MUST_USE_SHAREABLE = 256 + 35,

        MCIERR_MISSING_DEVICE_NAME = 256 + 36,

        MCIERR_BAD_TIME_FORMAT = 256 + 37,

        MCIERR_NO_CLOSING_QUOTE = 256 + 38,

        MCIERR_DUPLICATE_FLAGS = 256 + 39,

        MCIERR_INVALID_FILE = 256 + 40,

        MCIERR_NULL_PARAMETER_BLOCK = 256 + 41,

        MCIERR_UNNAMED_RESOURCE = 256 + 42,

        MCIERR_NEW_REQUIRES_ALIAS = 256 + 43,

        MCIERR_NOTIFY_ON_AUTO_OPEN = 256 + 44,

        MCIERR_NO_ELEMENT_ALLOWED = 256 + 45,

        MCIERR_NONAPPLICABLE_FUNCTION = 256 + 46,

        MCIERR_ILLEGAL_FOR_AUTO_OPEN = 256 + 47,

        MCIERR_FILENAME_REQUIRED = 256 + 48,

        MCIERR_EXTRA_CHARACTERS = 256 + 49,

        MCIERR_DEVICE_NOT_INSTALLED = 256 + 50,

        MCIERR_GET_CD = 256 + 51,

        MCIERR_SET_CD = 256 + 52,

        MCIERR_SET_DRIVE = 256 + 53,

        MCIERR_DEVICE_LENGTH = 256 + 54,

        MCIERR_DEVICE_ORD_LENGTH = 256 + 55,

        MCIERR_NO_INTEGER = 256 + 56,

        MCIERR_WAVE_OUTPUTSINUSE = 256 + 64,

        MCIERR_WAVE_SETOUTPUTINUSE = 256 + 65,

        MCIERR_WAVE_INPUTSINUSE = 256 + 66,

        MCIERR_WAVE_SETINPUTINUSE = 256 + 67,

        MCIERR_WAVE_OUTPUTUNSPECIFIED = 256 + 68,

        MCIERR_WAVE_INPUTUNSPECIFIED = 256 + 69,

        MCIERR_WAVE_OUTPUTSUNSUITABLE = 256 + 70,

        MCIERR_WAVE_SETOUTPUTUNSUITABLE = 256 + 71,

        MCIERR_WAVE_INPUTSUNSUITABLE = 256 + 72,

        MCIERR_WAVE_SETINPUTUNSUITABLE = 256 + 73,

        MCIERR_SEQ_DIV_INCOMPATIBLE = 256 + 80,

        MCIERR_SEQ_PORT_INUSE = 256 + 81,

        MCIERR_SEQ_PORT_NONEXISTENT = 256 + 82,

        MCIERR_SEQ_PORT_MAPNODEVICE = 256 + 83,

        MCIERR_SEQ_PORT_MISCERROR = 256 + 84,

        MCIERR_SEQ_TIMER = 256 + 85,

        MCIERR_SEQ_PORTUNSPECIFIED = 256 + 86,

        MCIERR_SEQ_NOMIDIPRESENT = 256 + 87,

        MCIERR_NO_WINDOW = 256 + 90,

        MCIERR_CREATEWINDOW = 256 + 91,

        MCIERR_FILE_READ = 256 + 92,

        MCIERR_FILE_WRITE = 256 + 93,

        MCIERR_NO_IDENTITY = 256 + 94,

        MIXERR_INVALLINE = 1024 + 0,

        MIXERR_INVALCONTROL = 1024 + 1,

        MIXERR_INVALVALUE = 1024 + 2,

        MIXERR_LASTERROR = 1024 + 2,
    }

    /// <summary>
    /// Specifies capabilities of a waveOut device.
    /// </summary>
    [Flags]
    public enum WAVECAPS
    {
        /// <summary>
        /// The device can change playback pitch.
        /// </summary>
        WAVECAPS_PITCH = 0x01,

        /// <summary>
        /// The device can change the playback rate.
        /// </summary>
        WAVECAPS_PLAYBACKRATE = 0x02,

        /// <summary>
        /// The device can change the volume.
        /// </summary>
        WAVECAPS_VOLUME = 0x04,

        /// <summary>
        /// The device can change the stereo volume.
        /// </summary>
        WAVECAPS_LRVOLUME = 0x08,

        /// <summary>
        /// The device is synchronus.
        /// </summary>
        WAVECAPS_SYNC = 0x10,

        /// <summary>
        /// The device supports sample accurate.
        /// </summary>
        WAVECAPS_SAMPLEACCURATE = 0x20,

        /// <summary>
        /// The device supports direct sound writing.
        /// </summary>
        WAVECAPS_DIRECTSOUND = 0x40,
    }

}