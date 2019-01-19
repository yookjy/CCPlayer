using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace CCPlayer.HWCodecs.Matroska.Common
{
    public class FrameBufferData
    {
        public static FrameBufferData Empty 
        {
            get
            {
                return new FrameBufferData();
            }
        }

        private FrameBufferData() { }
        public FrameBufferData(byte[] data, long timeCode, long duration, bool keyFrame, bool discaradable, bool invisible)
        {
            BinaryData = data;
            Data = data.AsBuffer();
            TimeCode = TimeSpan.FromTicks(timeCode);
            Duration = TimeSpan.FromTicks(duration);
            KeyFrame = keyFrame;
            Discardable = discaradable;
            Invisible = invisible;
        }

        public FrameBufferData(IBuffer data, long timeCode, long duration, bool keyFrame, bool discaradable, bool invisible)
        {
            Data = data;
            TimeCode = TimeSpan.FromTicks(timeCode);
            Duration = TimeSpan.FromTicks(duration);
            KeyFrame = keyFrame;
            Discardable = discaradable;
            Invisible = invisible;
        }

        public TimeSpan TimeCode { get; private set; }
        public IBuffer Data { get; private set; }
        public byte[] BinaryData { get; private set; }
        public TimeSpan Duration { get; private set; }
        public bool KeyFrame { get; private set; }
        public bool Discardable { get; private set; }
        public bool Invisible { get; private set; }
    }
}
