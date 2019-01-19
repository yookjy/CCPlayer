using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Threading;

namespace MediaParsers.FlvParser
{
    public class FlvFileBody
    {
        private Queue<FlvTag> audioTagQueue = new Queue<FlvTag>();
        public Queue<FlvTag> AudioTagQueue
        {
            get
            {
                return audioTagQueue;
            }
        }

        private Queue<FlvTag> videoTagQueue = new Queue<FlvTag>();
        public Queue<FlvTag> VideoTagQueue
        {
            get
            {
                return videoTagQueue;
            }
        }

        public List<FlvTag> ScriptTageList
        {
            get
            {
                return scriptTageList;
            }
        }

        private FlvTag audioInfoflvTag;
        public FlvTag AudioInfoFlvTag
        {
            get
            {
                return audioInfoflvTag;
            }
        }

        private FlvTag videoInfoTag;
        public FlvTag VideoInfoFlvTag
        {
            get
            {
                return videoInfoTag;
            }
        }

        private List<FlvTag> scriptTageList = new List<FlvTag>();

        private Stream Stream;

        long offset;

        public FlvFileBody(Stream stream, ref long offset)
        {
            //uint previousTagSize = stream.ReadUInt32(ref offset);
            offset = stream.Position += 4;
            this.Stream = stream;
            SeekData = new FlvSeekData();

            do
            {
                FlvTag ft = new FlvTag(stream, ref offset);
                if (ft.TagType == TagType.Video)
                {
                    if (ft.VideoData.CodecID == CodecID.AVC)
                    {
                        //H264 코덱
                        if (ft.VideoData.AVCVideoPacket.AVCPacketType == AVCPacketType.AVCSequenceHeader)
                        {
                            videoInfoTag = ft;
                        }
                        else
                        {
                            videoTagQueue.Enqueue(ft);
                            //seek 데이터 추가
                            SeekData.Add(ft);
                        }
                    }
                    else
                    {
                        //가상 infoTag
                        videoInfoTag = ft;
                        videoTagQueue.Enqueue(ft);
                        //seek 데이터 추가
                        SeekData.Add(ft);
                    }
                }
                else if (ft.TagType == TagType.Audio)
                {
                    if (ft.AudioData.SoundFormat == SoundFormat.AAC)
                    {
                        if ((ft.AudioData.SoundData as AACAudioData).AACPacketType == AACPacketType.AACSequenceHeader)
                        {
                            audioInfoflvTag = ft;
                        }
                        else
                        {
                            audioTagQueue.Enqueue(ft);
                        }
                    }
                    else
                    {
                        //가상 infotag
                        audioInfoflvTag = ft;
                        //실제 데이터 
                        audioTagQueue.Enqueue(ft);
                    }
                }
                else
                {
                    scriptTageList.Add(ft);
                    SeekData.Load(ft);
                }
            } while (scriptTageList.Count == 0
                || audioTagQueue.Count == 0
                || videoTagQueue.Count == 0);

            this.offset = offset;
        }

        public FlvTag CurrentVideoTag
        {
            get
            {
                if (videoTagQueue.Count == 0)
                {
                    Load(TagType.Video);
                }

                return videoTagQueue.Dequeue();
            }
        }

        public FlvTag CurrentAudioTag
        {
            get
            {
                if (audioTagQueue.Count == 0)
                {
                    Load(TagType.Audio);
                }
                return audioTagQueue.Dequeue();
            }
        }

        public void Load(TagType type)
        {
            FlvTag ft = null;
            TagType loadType = TagType.None;
            do
            {
                ft = new FlvTag(this.Stream, ref offset);
                if (ft.TagType == TagType.Video)
                {
                    if (ft.VideoData.CodecID != CodecID.AVC || ft.VideoData.AVCVideoPacket.AVCPacketType != AVCPacketType.AVCSequenceHeader)
                    {
                        videoTagQueue.Enqueue(ft);
                        loadType = ft.TagType;
                        //seek 데이터 추가
                        SeekData.Add(ft);
                    }
                }
                else if (ft.TagType == TagType.Audio)
                {
                    if (ft.AudioData.SoundFormat != SoundFormat.AAC
                        || (ft.AudioData.SoundData as AACAudioData).AACPacketType != AACPacketType.AACSequenceHeader)
                    {
                        audioTagQueue.Enqueue(ft);
                        loadType = ft.TagType;
                    }
                }
            } while (loadType != type);
            //System.Diagnostics.Debug.WriteLine("a : {0} , v : {1}", audioTagQueue.Count, videoTagQueue.Count);
        }

        public FlvSeekData SeekData { get; set; }

        public long SeekQueue(long seekToTime)
        {
            var frame = SeekData.FindSeekFrame(seekToTime);
            lock (videoTagQueue)
            {
                videoTagQueue.Clear();
            }
            lock (audioTagQueue)
            {
                audioTagQueue.Clear();
            }
            seekToTime = frame.Key;
            offset = frame.Value;

            return seekToTime;
        }
    }
}
