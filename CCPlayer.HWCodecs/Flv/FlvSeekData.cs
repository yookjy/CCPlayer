using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaParsers.FlvParser
{
    public class FlvSeekData : SortedDictionary<long, long>
    {
        public event EventHandler ProgressStarted;

        public event ProgressChangedEventHandler ProgressChanged;

        public event EventHandler ProgressCompleted;

        public bool CanSave { get; private set; }

        public bool IsScriptLoad { get; private set; }

        public FlvSeekData()
            : base()
        {
        }

        public FlvSeekData(SortedDictionary<long, long> seekData)
            : base(seekData)
        {
        }

        public void Add(FlvTag videoTag)
        {
            if (videoTag.VideoData.FrameType == FrameType.Keyframe && !IsScriptLoad)
            {
                AddTag(videoTag.Timestamp, videoTag.Offset - 16);
            }
        }

        private bool AddTag(long timeCode, long offset)
        {
            if (this.ContainsKey(timeCode) && this[timeCode] == offset) return false;

            this[timeCode] = offset;
            CanSave = true;
            return true;
        }

        public void Make(Stream stream, long seekToTime)
        {
            var frame = FindSeekFrame(seekToTime);
            var time = frame.Key;
            var offset = frame.Value;

            var startTime = time;
            int previousProgressPercentage = 0;
            int status = 0;

            do
            {
                var ftp = new FlvTagPointer(stream, ref offset);
                if (ftp.IsKeyFrame)
                {
                    var added = AddTag(ftp.Timestamp, ftp.Offset);

                    if (added && status == 0 && ProgressStarted != null)
                    {
                        status = 1;
                        ProgressStarted(this, null);
                    }

                    int progressPercentage = (int)(((double)(ftp.Timestamp - startTime) / (seekToTime - startTime)) * 100);
                    if (ProgressChanged != null && previousProgressPercentage != progressPercentage)
                    {
                        ProgressChanged(this, new ProgressChangedEventArgs(progressPercentage, null));
                        previousProgressPercentage = progressPercentage;
                    }
                }

                time = ftp.Timestamp;

            } while (time <= seekToTime); //마지막까지 가버린 경우는???? 역방향 시크는?

            if (status == 1 && ProgressCompleted != null)
            {
                ProgressCompleted(this, null);
            }
        }

        public void Load(FlvTag scriptTag)
        {
            var value = scriptTag.ScriptData.Values[1].Value;
            var hasKeyFrame = (value as ScriptObject)["hasKeyframes"];

            if (hasKeyFrame != null && hasKeyFrame.ToString() == "1")
            {
                var keyframes = (value as ScriptObject)["keyframes"];
                var times = (keyframes as ScriptObject)["times"] as ScriptArray;
                var filepositions = (keyframes as ScriptObject)["filepositions"] as ScriptArray;

                if (times.Values.Count == filepositions.Values.Count)
                {
                    for (int i = 0; i < times.Values.Count; i++)
                    {
                        var timeCode = TimeSpan.FromSeconds((double)times.Values[i]).Ticks;
                        var offset = (long)(double)filepositions.Values[i];
                        //설정
                        this[timeCode] = offset;
                    }

                    IsScriptLoad = true;
                }
            }
        }

        public KeyValuePair<long, long> FindSeekFrame(long timeCode)
        {
            var val = this.LastOrDefault(x => x.Key <= timeCode);

            if (val.Key == 0 && val.Value == 0)
            {
                val = this.FirstOrDefault(x => x.Key > timeCode);
            }
            return val;
        }

        class FlvTagPointer
        {
            public long Timestamp { get; private set; }

            public long Offset { get; set; }

            public bool IsKeyFrame { get; set; }

            public FlvTagPointer(Stream stream, ref long offset)
            {
                //시작 위치 저장
                this.Offset = offset;

                var tagType = (TagType)stream.ReadUInt8(ref offset);
                var dataSize = stream.ReadUInt24(ref offset);

                var value = stream.ReadUInt24(ref offset);
                value |= stream.ReadUInt8(ref offset) << 24;
                //시간 저장
                this.Timestamp = TimeSpan.FromMilliseconds(value).Ticks;

                //this.StreamID = stream.ReadUInt24(ref offset);
                offset += 3;
                var mediaInfo = stream.ReadUInt8(ref offset);
                var count = dataSize - 1;

                if (tagType == TagType.Video)
                {
                    //byte[] bytes = stream.ReadBytes(ref offset, (int)count);
                    byte[] buffer = stream.ReadBytes(ref offset, 4);
                    offset -= 4;

                    //비디오 인경우만 키프레임을 셋팅함 (비디오 키프레임이 아니면 담을 필요가 없다.)
                    this.IsKeyFrame = (FrameType)(mediaInfo >> 4) == FrameType.Keyframe;

                    byte[] x = new byte[4];
                    x[1] = buffer[1];
                    x[2] = buffer[2];
                    x[3] = buffer[3];

                    if ((AVCPacketType)buffer[0] == AVCPacketType.AVCNALU)
                    {
                        //P / B 프레임의 경우 시간 추가
                        this.Timestamp += TimeSpan.FromMilliseconds((int)BitConverterBE.ToUInt32(x, 0)).Ticks;
                    }
                }
                //데이터를 읽은것을 간주
                offset += count;

                //this.TagSize = stream.ReadUInt32(ref offset);
                offset += 4;
            }
        }
    }
}
