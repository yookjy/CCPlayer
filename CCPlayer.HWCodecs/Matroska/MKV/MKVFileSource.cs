using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CCPlayer.HWCodecs.Media;
using CCPlayer.HWCodecs.Matroska.Codecs;
using CCPlayer.HWCodecs.Matroska.EBML;
using Windows.Media.Core;
using CCPlayer.HWCodecs.Matroska.Common;

namespace CCPlayer.HWCodecs.Matroska.MKV
{
    public class SkipErrorClusterEventArgs : EventArgs
    {
        public ulong Timecode { get; set; }
        public SkipErrorClusterEventArgs(ulong timecode)
        {
            this.Timecode = timecode;
        }
    }

    public class MKVFileSource
    {
        public bool IsReloading { get; set; }

        private Document _Document;
        public Document Document { get { return _Document; } }

        public string Title
        {
            get
            {
                return _Segment.Info[0].Title;
            }
        }

        public double Duration
        {
            get
            {
                return _Segment.Info[0].Duration;
            }
        }

        public ulong TimecodeScale
        {
            get
            {
                return _Segment.Info[0].TimecodeScale;
            }
        }

        private Segment _Segment { get; set; }
        public IEnumerable<KnownCodec> UsableCodecs { get; set; }
        public IEnumerable<KnownCodec> UsableMediaCodecs { get; set; }
        public IEnumerable<ICodec> UnusableMediaCodecs { get; private set; }
        public IEnumerable<KnownCodec> UsableSubtitleCodecs { get; set; }

        public MKVFileSource(Document document)
        {
            _Document = document;
            _Segment = document.Segments[0];
            //활성화된 트랙에서 필요한 코덱리스트를 생성
            var codecs = CodecFactory.Create(_Segment.Tracks[0].TrackEntry.Where(x => x.FlagEnabled == 1).ToList());
            //사용가능한 모든 코덱 추가
            UsableCodecs = codecs.Where(x => x.IsSupported).Cast<KnownCodec>();
            //사용가능한 미디어 코덱만 추가
            UsableMediaCodecs = UsableCodecs.Where(x => x.CodecType == TrackTypes.Video || x.CodecType == TrackTypes.Audio);
            //사용가능한 자막 코덱 리스트를 생성
            UsableSubtitleCodecs = UsableCodecs.Where(x => x.CodecType == TrackTypes.Subtitle);
            
            //디폴트 비디오 트랙의 코덱
            var videoCodec = UsableMediaCodecs.FirstOrDefault(x => x.CodecType == TrackTypes.Video && x.TrackEntry.FlagDefault == 1);
            if (videoCodec == null)
            {
                videoCodec = UsableMediaCodecs.FirstOrDefault(x => x.CodecType == TrackTypes.Video);
            }

            var audioCodec = UsableMediaCodecs.FirstOrDefault(x => x.CodecType == TrackTypes.Audio && x.TrackEntry.FlagDefault == 1);
            if (audioCodec == null)
            {
                audioCodec = UsableMediaCodecs.FirstOrDefault(x => x.CodecType == TrackTypes.Audio);
            }

            //비디오나 오디오코덱을 사용할 수 없는 경우만 사용 불가능 코덱을 저장
            if (videoCodec == null || audioCodec == null)
            {
                //사용 불가능한 미디어 코덱 저장
                UnusableMediaCodecs = codecs.Where(x => !x.IsSupported && (x.CodecType == TrackTypes.Video || x.CodecType == TrackTypes.Audio));
            }
            else
            {
                UnusableMediaCodecs = new List<ICodec>();
                //디폴트 비디오/오디오 트랙으로 미디어 디스크립터 생성
                CreateMediaStreamSource(audioCodec);
            }
        }

        public MediaStreamSource CreateMediaStreamSource(ICodec videoCodec, ICodec audioCodec)
        {
            //디폴트 비디오 트랙의 코덱
            var currentVideoCodec = videoCodec as KnownCodec;
            //요청된 오디오 트랙의 코덱
            var currentAudioCodec = audioCodec as KnownCodec;

            //현재 비디오 트랙 번호
            VideoTrackID = currentVideoCodec.TrackNumber;
            //현재 오디오 트랙 번호
            AudioTrackID = currentAudioCodec.TrackNumber;

            //미디어 소스 생성
            var mss = new MediaStreamSource(currentVideoCodec.MediaStreamDescriptor, currentAudioCodec.MediaStreamDescriptor);
            //var mss = new MediaStreamSource(currentVideoCodec.MediaStreamDescriptor);
            //var mss = new MediaStreamSource(currentAudioCodec.MediaStreamDescriptor);
            MediaStreamSource = mss;
            //미디어 전체 길이 설정
            MediaStreamSource.Duration = TimeSpan.FromTicks((long)(this.Duration * Element.TIC_MILLISECONDS));
            //미지어 시크 여부 설정
            MediaStreamSource.CanSeek = _Segment.Cues.CuePoint.Sum(x => x.CueTrackPositions.Count) > 0;

            //비디오 제목 추가
            if (!string.IsNullOrEmpty(this.Title))
            {
                MediaStreamSource.VideoProperties.Title = this.Title;
            }

            return MediaStreamSource;
        }

        public MediaStreamSource CreateMediaStreamSource(ICodec audioCodec)
        {
            //디폴트 비디오 트랙의 코덱
            var currentVideoCodec = UsableMediaCodecs.FirstOrDefault(x => x.CodecType == TrackTypes.Video && x.TrackEntry.FlagDefault == 1);
            if (currentVideoCodec == null)
            {
                currentVideoCodec = UsableMediaCodecs.FirstOrDefault(x => x.CodecType == TrackTypes.Video);
            }
            
            MediaStreamSource = CreateMediaStreamSource(currentVideoCodec, audioCodec);
                        
            return MediaStreamSource;
        }

        public MediaStreamSource MediaStreamSource { get; private set; }

        ulong VideoTrackID = 0;
        ulong AudioTrackID = 0;
        public ulong SubtitleTrackID { get; set; }

        Queue<FrameBufferData> VideoFrameQueue = new Queue<FrameBufferData>();
        Queue<FrameBufferData> AudioFrameQueue = new Queue<FrameBufferData>();
        Queue<FrameBufferData> SubtitleFrameQueue = new Queue<FrameBufferData>();
        
        public IEnumerator<FrameBufferData> SubtitleFrames
        {
            get
            {
                //현재 추출할 수 있는 상황이 아닌데, 버퍼가 남아 있는 경우 버퍼 삭제
                if (SubtitleTrackID == 0)
                {
                    SubtitleFrameQueue.Clear();
                }
                
                //버퍼를 모두 추출하여 리턴
                List<FrameBufferData> list = new List<FrameBufferData>();
                while(SubtitleFrameQueue.Count > 0)
                {
                    list.Add(SubtitleFrameQueue.Dequeue());
                }
                return list.GetEnumerator();
            }
        }

        public delegate void SkipErrorClusterEventHandler(object sender, SkipErrorClusterEventArgs args);

        public event SkipErrorClusterEventHandler SkipErrorClusterStarted;

        public event SkipErrorClusterEventHandler SkipErrorClusterCompleted;
        
        void ReadBlock(MediaTypes msType)
        {
            Block block = null;
            Cluster cluster = _Segment.Cluster;
            MediaTypes blockMediaType = MediaTypes.None;

            ulong lastClusterPosition = _Segment.Cues.CuePoint.Max(cp => cp.CueTrackPositions.Max(ctp => ctp.CueClusterPosition));
            bool skippingErrorCluster = false;

            while(true)
            {
                while (cluster.Block.MoveNext())
                {
                    long timecode = 0;
                    //블록 데이터 처리
                    block = cluster.Block.Current;
                    blockMediaType = EnqueueFrameData(cluster, block, ref timecode);

                    //읽어야 하는 블록이 발견되었으면 종료
                    if (blockMediaType == msType)
                    {
                        if (skippingErrorCluster && SkipErrorClusterCompleted != null)
                        {
                            SkipErrorClusterCompleted(this, new SkipErrorClusterEventArgs((ulong)timecode));
                            skippingErrorCluster = false;
                        }
                        break;
                    }
                }

                //읽어야 하는 블록이 아직 발견되지 않은 경우
                if (blockMediaType != msType)
                {
                    if (cluster.MoveNext())
                    {
                        //다음 클러스터 읽기 성공
                        cluster = cluster.Current;
                    }
                    else
                    {
                        //다음 클러스트 읽기 실패
                        //실패한 포지션을 현재 포지션으로 설정
                        ulong currentClusterPosition = (ulong)cluster.Offset + cluster.Size - (ulong)cluster.Parent.Offset; //세그먼트이 offset 차감
                        ulong nextClusterPosition = 0;
                        ulong nextClusterTimecode = 0;

                        do
                        {
                            foreach (var cuePoint in _Segment.Cues.CuePoint)
                            {
                                //현재시간 바로 직후의 포지션 검색
                                var cueTrackPosition = cuePoint.CueTrackPositions.FirstOrDefault(ctp => ctp.CueClusterPosition > currentClusterPosition);
                                if (cueTrackPosition != null)
                                {
                                    //이동할 포지션이 검색되면 포지션 저장 및 이동할 시간 저장
                                    nextClusterPosition = cueTrackPosition.CueClusterPosition;
                                    nextClusterTimecode = cuePoint.CueTime;
                                    break;
                                }
                            }

                            if (nextClusterPosition > 0)
                            {
                                //이동할 포지션이 있고, 이벤트가 발생이 되지 않았다면 이벤트 발생
                                if (!skippingErrorCluster && SkipErrorClusterStarted != null)
                                {
                                    skippingErrorCluster = true;
                                    SkipErrorClusterStarted(this, new SkipErrorClusterEventArgs(nextClusterTimecode * (ulong)Element.TIC_MILLISECONDS));
                                }

                                //클러스터 이동
                                _Segment.LoadCluster((long)nextClusterPosition);
                                cluster = _Segment.Cluster;
                                //현재 포지션 변경
                                currentClusterPosition = nextClusterPosition;
                            }
                            else
                            {
                                //클러스터 포지션의 범위를 벗어났다. 종료 처리
                                //오류 이벤트가 발생했으면 종료 이벤트 호출을 위해 시간 설정
                                if (skippingErrorCluster && SkipErrorClusterCompleted != null)
                                {
                                    SkipErrorClusterCompleted(this, new SkipErrorClusterEventArgs((ulong)(this.Duration * Element.TIC_MILLISECONDS)));
                                    skippingErrorCluster = false;
                                }
                                
                                //마지막 종료 데이터 입력
                                if (msType == MediaTypes.Audio)
                                {
                                    AudioFrameQueue.Enqueue(FrameBufferData.Empty);
                                }
                                else if (msType == MediaTypes.Video)
                                {
                                    VideoFrameQueue.Enqueue(FrameBufferData.Empty);
                                }
                                //마지막까지 도달했으므로 루프 탈출
                                return;
                            }
                        } while (cluster == null && currentClusterPosition <= lastClusterPosition);
                    }
                }
                else
                {
                    //블록을 읽었으므로 루프 탈출
                    break;
                }
            }
            
        }

        private MediaTypes EnqueueFrameData(Cluster cluster, Block block, ref long timecode)
        {
            MediaTypes currBlockType = MediaTypes.None;
            Block currBlock = null;
            BlockGroup blockGroup = null;
            Queue<FrameBufferData> frameQueue = null;
            
            //블록 자식 레벨 엘리먼트 로드
            currBlock = block.GetBlock(out blockGroup);
            
            //큐 선택
            if (currBlock.TrackNumber == VideoTrackID)
            {
                currBlockType = MediaTypes.Video;
                frameQueue = VideoFrameQueue;
            }
            else if (currBlock.TrackNumber == AudioTrackID)
            {
                currBlockType = MediaTypes.Audio;
                frameQueue = AudioFrameQueue;
            }
            else if (currBlock.TrackNumber == SubtitleTrackID)
            {
                currBlockType = MediaTypes.Subtitle;
                frameQueue = SubtitleFrameQueue;
            }
            else 
            {
                return MediaTypes.None;
            }

            var codec = UsableCodecs.FirstOrDefault(x => x.TrackNumber == currBlock.TrackNumber);
            var track = codec.TrackEntry;
            byte[] CompressHeader = codec.CompressHeader;

            timecode = ((long)cluster.Timecode + currBlock.Timecode) * Element.TIC_MILLISECONDS;
            long timecodeOffset = 0, duration = (long)(track.DefaultDuration / 100); //100나노 초가 1틱이므로 100으로 나눈다.

            if (blockGroup != null && blockGroup.BlockDuration > 0)
            {
                duration = (long)blockGroup.BlockDuration * Element.TIC_MILLISECONDS;
            }
            
            var dts = currBlock.GetBlockData(CompressHeader);
            foreach (var item in dts)
            {
                try
                {
                    var frame = codec.GetFrameBufferData(
                        item,
                        (long)timecodeOffset + (long)timecode,
                        duration,
                        currBlock.IsKeyFrame,
                        currBlock.Discardable, 
                        currBlock.Invisible);
                    
                    frameQueue.Enqueue(frame);
                    timecodeOffset += duration;
                }
                catch (Exception)
                {
                    throw;
                }
            }
            //리턴값 (마지막 시간)
            timecode += timecodeOffset;
            //블록 타입
            return currBlockType;
        }

        public FrameBufferData GetFrameData(IMediaStreamDescriptor descriptor)
        {
            if (descriptor is VideoStreamDescriptor)
            {
                if (VideoFrameQueue.Count == 0)
                {
                    ReadBlock(MediaTypes.Video);
                }
                return VideoFrameQueue.Dequeue();
            }
            else if (descriptor is AudioStreamDescriptor)
            {
                if (AudioFrameQueue.Count == 0)
                {
                    ReadBlock(MediaTypes.Audio);
                }
                return AudioFrameQueue.Dequeue();
            }
            return FrameBufferData.Empty;
        }

        public TimeSpan Seek(ulong timeCode)
        {
            //var cuePoint = _Segment.Cues.CuePoint.FirstOrDefault(cp => cp.CueTime >= timeCode);
            var cuePoint = _Segment.Cues.CuePoint.LastOrDefault(cp => cp.CueTime <= timeCode);
            
            if (cuePoint == null)
            {
                cuePoint = _Segment.Cues.CuePoint.FirstOrDefault();        
            }
            else if (_Segment.Cues.CuePoint.LastOrDefault().CueTime < timeCode)
            {
                //종료 데이터 생성
                VideoFrameQueue.Clear();
                AudioFrameQueue.Clear();

                VideoFrameQueue.Enqueue(FrameBufferData.Empty);
                AudioFrameQueue.Enqueue(FrameBufferData.Empty);

                return TimeSpan.FromTicks((long)Duration / 100);
            }

            var cueTrackPosition = cuePoint.CueTrackPositions.FirstOrDefault();

            var currentClusterPosition = cueTrackPosition.CueClusterPosition;
            var currentBlockIndex = (int)cueTrackPosition.CueBlockNumber - 1;

            //클러스터 이동
            _Segment.LoadCluster((long)currentClusterPosition);
            if (_Segment.Cluster != null)
            {
                //블록 이동
                _Segment.Cluster.MoveBlockPointer(currentBlockIndex);
            }

            //큐 초기화
            VideoFrameQueue.Clear();
            AudioFrameQueue.Clear();

            return TimeSpan.FromTicks((long)cuePoint.CueTime * Element.TIC_MILLISECONDS);
        }
    }
}
