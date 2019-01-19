using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace MediaParsers.FlvParser
{
    public class FlvTag
    {
        public FlvTag(Stream stream, ref long offset)
        {
            this.TagType = (TagType)stream.ReadUInt8(ref offset);
            this.DataSize = stream.ReadUInt24(ref offset);

            var value = stream.ReadUInt24(ref offset);
            value |= stream.ReadUInt8(ref offset) << 24;
            this.Timestamp = TimeSpan.FromMilliseconds(value).Ticks;

            this.StreamID = stream.ReadUInt24(ref offset);
            var mediaInfo = stream.ReadUInt8(ref offset);
            this.Count = this.DataSize - 1;
            
            if (this.TagType == TagType.Video)
            {
                this.Offset = offset + 4;
                byte[] bytes = stream.ReadBytes(ref offset, (int)this.Count);
                this.VideoData = new VideoData(mediaInfo, bytes);

                if (this.VideoData.CodecID == CodecID.AVC && this.VideoData.AVCVideoPacket.AVCPacketType == AVCPacketType.AVCNALU)
                {
                    this.Timestamp += TimeSpan.FromMilliseconds(this.VideoData.AVCVideoPacket.CompositionTime).Ticks;
                }
            }
            else if (this.TagType == TagType.Audio)
            {
                this.Offset = offset + 1;
                byte[] bytes = stream.ReadBytes(ref offset, (int)this.Count);
                this.AudioData = new AudioData(mediaInfo, bytes);
            }
            else if (this.TagType == TagType.ScriptData)
            {
                long position = stream.Position;
                stream.Position += this.Count;
                offset = stream.Position;
                this.ScriptData = new ScriptData(stream, position, this.DataSize);
            }

            this.TagSize = stream.ReadUInt32(ref offset);
        }

        public TagType TagType
        {
            get;
            private set;
        }

        public uint DataSize
        {
            get;
            private set;
        }

        public long Timestamp
        {
            get;
            private set;
        }

        public uint StreamID
        {
            get;
            private set;
        }

        public ScriptData ScriptData
        {
            get;
            private set;
        }

        public AudioData AudioData
        {
            get;
            private set;
        }

        public VideoData VideoData
        {
            get;
            private set;
        }

        public uint TagSize
        {
            get;
            private set;
        }

        public long Offset
        {
            get;
            private set;
        }

        public uint Count
        {
            get;
            private set;
        }
    }
}
