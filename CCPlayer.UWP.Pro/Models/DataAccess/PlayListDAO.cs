using CCPlayer.UWP.Extensions;
using CCPlayer.UWP.Helpers;
using GalaSoft.MvvmLight.Threading;
using SQLitePCL;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage.AccessCache;
using Windows.UI.Xaml.Input;

namespace CCPlayer.UWP.Models.DataAccess
{
    public class PlayListDAO : BaseDAO
    {
        #region 재생목록 SQL 구문
        private const string DDL_CREATE_TABLE_PLAYLIST =
            @"CREATE TABLE IF NOT EXISTS
                PLAY_LIST ( 
                    SEQ                     INTEGER PRIMARY KEY NOT NULL
                   ,NAME                    VARCHAR(50) UNIQUE NOT NULL
                )";

        private const string DML_INSERT_PLAYLIST =
            @"INSERT INTO
                PLAY_LIST (
                   NAME
                )
                VALUES (
                   @NAME
                )";
        private const string DML_UPDATE_PLAYLIST =
            @"UPDATE PLAY_LIST SET
                    NAME = @NAME
               WHERE SEQ != 1 AND SEQ = @SEQ";
        
        private const string DML_DELETE_PLAYLIST =
            @"DELETE FROM PLAY_LIST WHERE SEQ = @SEQ";

        private const string DML_SELECT_PLAYLIST =
            @"SELECT P.SEQ
                    ,P.NAME
                FROM PLAY_LIST P
               WHERE P.SEQ != 1
            ORDER BY P.NAME";

        private const string DML_SELECT_PLAYLIST_BY_NAME =
            @"SELECT P.SEQ
                    ,P.NAME
                FROM PLAY_LIST P
               WHERE P.SEQ != 1 AND P.NAME = @NAME ";
        #endregion

        #region 재생목록 파일 SQL 구문
        private const string DDL_CREATE_TABLE_PLAYLIST_FILE =
            @"CREATE TABLE IF NOT EXISTS
                PLAY_LIST_FILE ( 
                    OWNER_SEQ               INTEGER NOT NULL
                   ,PATH                    VARCHAR(255) NOT NULL
                   ,FAL_TOKEN               VARCHAR(255) 
                   ,ADDED_DATETIME          DATETIME
                   ,PAUSED_TIME             INTEGER
                   ,ORDER_NO                INTEGER
                   ,PRIMARY KEY (OWNER_SEQ, PATH)
                )";

        private const string DML_INSERT_PLAYLIST_FILE =
            @"INSERT OR REPLACE INTO
                PLAY_LIST_FILE (
                    OWNER_SEQ
                   ,PATH 
                   ,FAL_TOKEN
                   ,ADDED_DATETIME
                   ,PAUSED_TIME
                   ,ORDER_NO
                )
                VALUES (
                    @OWNER_SEQ
                   ,@PATH
                   ,@FAL_TOKEN
                   ,STRFTIME('%Y-%m-%d %H:%M:%f', 'NOW', 'LOCALTIME')
                   ,IFNULL((SELECT PAUSED_TIME FROM PLAY_LIST_FILE WHERE OWNER_SEQ = @OWNER_SEQ AND PATH = @PATH), 0.0)
                   ,IFNULL((SELECT MAX(ORDER_NO) FROM PLAY_LIST_FILE WHERE OWNER_SEQ = @OWNER_SEQ), 0) + @ORDER_NO
                )";

        private const string DML_UPDATE_PLAYLIST_FILE =
            @"UPDATE PLAY_LIST_FILE SET
                    ORDER_NO = @ORDER_NO
                   ,PAUSED_TIME = @PAUSED_TIME
               WHERE OWNER_SEQ = @OWNER_SEQ 
                 AND PATH = @PATH";

        private const string DML_UPDATE_PAUSED_TIME_PLAYLIST_FILE =
            @"UPDATE PLAY_LIST_FILE SET
                    PAUSED_TIME = @PAUSED_TIME
               WHERE OWNER_SEQ = @OWNER_SEQ 
                 AND PATH = @PATH";

        private const string DML_DELETE_PLAYLIST_FILE =
            @"DELETE FROM PLAY_LIST_FILE WHERE OWNER_SEQ = @OWNER_SEQ 
                                           AND PATH = @PATH";
        
        private const string DML_DELETE_PLAYLIST_FILE_ALL =
            @"DELETE FROM PLAY_LIST_FILE WHERE OWNER_SEQ = @OWNER_SEQ";

        private const string DML_SELECT_PLAYLIST_FILE =
            @"SELECT F.OWNER_SEQ
                    ,F.PATH
                    ,F.FAL_TOKEN
                    ,F.ORDER_NO
                    ,F.ADDED_DATETIME
                    ,CAST(F.PAUSED_TIME AS FLOAT) AS PAUSED_TIME
                FROM PLAY_LIST P
                    ,PLAY_LIST_FILE F
               WHERE P.SEQ = F.OWNER_SEQ
                 AND F.OWNER_SEQ = @OWNER_SEQ
                 @CONDITION
            ORDER BY F.ORDER_NO";

        private const string DML_COUNT_PLAYLIST_FILE =
            @"SELECT COUNT(*) FROM PLAY_LIST_FILE WHERE OWNER_SEQ = @OWNER_SEQ";

        #endregion
        /// <summary>
        /// 테이블이 존재하지 않는 경우 생성한다. 
        /// </summary>
        protected override void CheckCreateTable()
        {
            using (var stmt = conn.Prepare(DDL_CREATE_TABLE_PLAYLIST))
            {
                if (stmt.Step() == SQLiteResult.DONE)
                {
                    InsertPlayList(new PlayList() { Name = "!___CCPlayer___Default___PlayList___!" });
                }
            }

            using (var stmt = conn.Prepare(DDL_CREATE_TABLE_PLAYLIST_FILE))
            {
                stmt.Step();
            }
        }

        /// <summary>
        /// 테이블 생성여부를 체크하여, 생성되지 않은 경우 생성한다.
        /// </summary>
        protected override void CheckAlterTable()
        {
        }

        /// <summary>
        /// 생성자
        /// </summary>
        /// <param name="conn">Db접속 객체</param>
        public PlayListDAO(SQLiteConnection conn) : base(conn) { }

        /// <summary>
        /// 재생목록을 로딩한다.
        /// </summary>
        /// <param name="playList">로딩될 리스트</param>
        public void LoadPlayList(ICollection<PlayList> playList, TappedEventHandler eventHandler)
        {
            using (var pstmt = conn.Prepare(DML_SELECT_PLAYLIST))
            {
                while (pstmt.Step() == SQLiteResult.ROW)
                {
                    PlayList fi = new PlayList()
                    {
                        Seq = pstmt.GetInteger("SEQ"),
                        Name = pstmt.GetText("NAME")
                    };

                    if (eventHandler != null)
                    {
                        fi.ItemTapped = eventHandler;
                    }

                    playList.Add(fi);
                }
            }
        }

        /// <summary>
        /// 이름으로 재생목록을 조회한다.
        /// </summary>
        /// <param name="playList">조회할 이름</param>
        public PlayList GetPlayList(string playListName)
        {
            PlayList playList = null;
            using (var pstmt = conn.Prepare(DML_SELECT_PLAYLIST_BY_NAME))
            {
                pstmt.Bind("@NAME", playListName);
                if (pstmt.Step() == SQLiteResult.ROW)
                {
                    playList = new PlayList()
                    {
                        Seq = pstmt.GetInteger("SEQ"),
                        Name = pstmt.GetText("NAME")
                    };
                }
            }
            return playList;
        }

        /// <summary>
        /// 재생목록을 등록한다.
        /// </summary>
        /// <param name="palyList">재생목록</param>
        /// <returns>DB 처리 결과</returns>
        public SQLiteResult InsertPlayList(PlayList playList)
        {
            SQLiteResult result = SQLiteResult.EMPTY;

            using (var pstmt = this.conn.Prepare(DML_INSERT_PLAYLIST))
            {
                pstmt.Bind("@NAME", playList.Name);
                result = pstmt.Step();
                
                if (result != SQLitePCL.SQLiteResult.DONE)
                {
                    return result;
                }
            }

            using (var pstmt = conn.Prepare("select last_insert_rowid()"))
            {
                if (pstmt.Step() == SQLiteResult.ROW)
                {
                    playList.Seq = pstmt.GetInteger(0);
                }
            }

            return result;
        }

        /// <summary>
        /// 재생목록을 갱신한다.
        /// </summary>
        /// <param name="palyList">재생목록</param>
        /// <returns>DB 처리 결과</returns>
        public SQLiteResult UpdatePlayList(PlayList playList)
        {
            SQLiteResult result = SQLiteResult.EMPTY;

            using (var pstmt = this.conn.Prepare(DML_UPDATE_PLAYLIST))
            {
                pstmt.Bind("@NAME", playList.Name);
                pstmt.Bind("@SEQ", playList.Seq);
                result = pstmt.Step();

                if (result != SQLitePCL.SQLiteResult.DONE)
                {
                    return result;
                }
            }

            return result;
        }
       
        /// <summary>
        /// 재생 목록을 삭제한다.
        /// </summary>
        /// <returns>DB 처리 결과</returns>
        public SQLiteResult DeletePlayList(PlayList playList)
        {
            SQLiteResult result = SQLiteResult.EMPTY;
            //재생목록 파일 삭제
            using (var pstmt = conn.Prepare(DML_DELETE_PLAYLIST_FILE_ALL))
            {
                pstmt.Bind("@OWNER_SEQ", playList.Seq);
                result = pstmt.Step();
            }

            if (result == SQLitePCL.SQLiteResult.DONE)
            {
                //재생 목록도 삭제
                using (var pstmt = conn.Prepare(DML_DELETE_PLAYLIST))
                {
                    pstmt.Bind("@SEQ", playList.Seq);
                    result = pstmt.Step();
                }
            }

            return result;
        }

        /// <summary>
        /// 재생목록파일을 로딩한다.
        /// </summary>
        /// <param name="playList">로딩할 재생목록</param>
        /// <param name="orderNo"></param>
        /// <param name="action"></param>
        public void LoadPlayListFiles(PlayList playList, int orderNo, Action<PlayListFile> action)
        {
            if (action == null)
            {
                throw new ArgumentNullException("Action<PlayListFile> argument can not be null");
            }

            using (var pstmt = conn.Prepare(DML_SELECT_PLAYLIST_FILE.Replace("@CONDITION", " AND F.ORDER_NO > @ORDER_NO")))
            {
                pstmt.Bind("@OWNER_SEQ", playList.Seq);
                pstmt.Bind("@ORDER_NO", orderNo);

                List<string> subtitlePathList = new List<string>();
                List<PlayListFile> playListFileList = new List<PlayListFile>();

                while (pstmt.Step() == SQLiteResult.ROW)
                {
                    string path = pstmt.GetText("PATH");
                    if (Xaml.Controls.MediaFileSuffixes.CLOSED_CAPTION_SUFFIX.Contains(Path.GetExtension(path).ToUpper()))
                    {
                        subtitlePathList.Add(path);
                    }
                    else
                    {
                        PlayListFile pl = new PlayListFile
                        {
                            Seq = pstmt.GetInteger("OWNER_SEQ"),
                            Path = pstmt.GetText("PATH"),
                            FalToken = pstmt.GetText2("FAL_TOKEN"),
                            PausedTime = TimeSpan.FromSeconds(pstmt.GetFloat2("PAUSED_TIME")),
                            OrderNo = pstmt.GetInteger2("ORDER_NO"),
                        };

                        //재생목록에 추가된 시간 파싱
                        DateTime addedDateTime;
                        if (DateTime.TryParse(pstmt.GetText2("ADDED_DATETIME"), out addedDateTime))
                        {
                            pl.AddedDateTime = addedDateTime;
                        }

                        playListFileList.Add(pl);
                    }
                    
                }

                foreach (var plf in playListFileList)
                {
                    var subPathList = subtitlePathList.Where(x => x.ToUpper().Contains(PathHelper.GetFullPathWithoutExtension(plf.Path).ToUpper())).ToList();

                    foreach (var subPath in subPathList)
                    {
                        if (plf.SubtitleList == null)
                            plf.SubtitleList = new List<string>();

                        plf.SubtitleList.Add(subPath);
                        subtitlePathList.Remove(subPath);
                    }
                    action.Invoke(plf);
                }
            }
        }

        public void LoadPlayListFiles(PlayList playList, List<PlayListFile> playListFileList)
        {
            using (var pstmt = conn.Prepare(DML_SELECT_PLAYLIST_FILE.Replace("@CONDITION", string.Empty)))
            {
                pstmt.Bind("@OWNER_SEQ", playList.Seq);

                List<string> subtitlePathList = new List<string>();
                while (pstmt.Step() == SQLiteResult.ROW)
                {
                    string path = pstmt.GetText("PATH");
                    if (Xaml.Controls.MediaFileSuffixes.CLOSED_CAPTION_SUFFIX.Contains(Path.GetExtension(path).ToUpper()))
                    {
                        subtitlePathList.Add(path);
                    }
                    else
                    {
                        PlayListFile pl = new PlayListFile
                        {
                            Seq = pstmt.GetInteger("OWNER_SEQ"),
                            Path = path,
                            FalToken = pstmt.GetText2("FAL_TOKEN"),
                            PausedTime = TimeSpan.FromSeconds(pstmt.GetFloat2("PAUSED_TIME")),
                            OrderNo = pstmt.GetInteger2("ORDER_NO"),
                        };

                        //재생목록에 추가된 시간 파싱
                        DateTime AddedDateTime;
                        if (DateTime.TryParse(pstmt.GetText2("ADDED_DATETIME"), out AddedDateTime))
                        {
                            pl.AddedDateTime = AddedDateTime;
                        }

                        playListFileList.Add(pl);
                    }
                }

                foreach(var plf in playListFileList)
                {
                    var subPathList = subtitlePathList.Where(x => x.ToUpper().Contains(PathHelper.GetFullPathWithoutExtension(plf.Path).ToUpper())).ToList();

                    foreach (var subPath in subPathList)
                    {
                        if (plf.SubtitleList == null)
                            plf.SubtitleList = new List<string>();

                        plf.SubtitleList.Add(subPath);
                        subtitlePathList.Remove(subPath);
                    }
                }
            }
        }
        
        public long CountPlayListFiles(PlayList playList)
        {
            long count = 0;
            using (var pstmt = this.conn.Prepare(DML_COUNT_PLAYLIST_FILE))
            {
                pstmt.Bind("@OWNER_SEQ", playList.Seq);
                if (pstmt.Step() == SQLiteResult.ROW)
                {
                    count = pstmt.GetInteger(0);
                }
            }
            return count;   
        }

        /// <summary>
        /// 재생목록 파일을 일괄 등록한다.
        /// </summary>
        /// <param name="playListFile">재생목록 파일 리스트</param>
        /// <returns>DB 처리 결과</returns>
        public SQLiteResult InsertPlayListFiles(PlayList ownerPlayList, IEnumerable<StorageItemInfo> storageItemInfoFiles)
        {
            return Transactional(() =>
            {
                SQLiteResult result = SQLiteResult.EMPTY;
                long orderSeq = CountPlayListFiles(ownerPlayList);

                using (var pstmt = this.conn.Prepare(DML_INSERT_PLAYLIST_FILE))
                {
                    foreach (var file in storageItemInfoFiles)
                    {
                        pstmt.Bind("@OWNER_SEQ", ownerPlayList.Seq);
                        pstmt.Bind("@PATH", file.Path);
                        pstmt.Bind("@FAL_TOKEN", file.FalToken);
                        pstmt.Bind("@ORDER_NO", ++orderSeq);

                        result = pstmt.Step();

                        if (result != SQLitePCL.SQLiteResult.DONE)
                        {
                            return result;
                        }

                        // Resets the statement, to that it can be used again (with different parameters).
                        pstmt.Reset();
                        pstmt.ClearBindings();
                    }
                }

                return result;
            });
        }

        /// <summary>
        /// 재생목록 파일을 업데이트 한다.
        /// </summary>
        /// <param name="playListFile">재생목록 파일 리스트</param>
        /// <returns></returns>
        public SQLiteResult UpdatePlayListFiles(PlayList ownerPlayList, IEnumerable<PlayListFile> playListFiles)
        {
            return Transactional(() =>
            {
                SQLiteResult result = SQLiteResult.EMPTY;
                using (var pstmt = this.conn.Prepare(DML_UPDATE_PLAYLIST_FILE))
                {
                    int orderNo = 0;
                    foreach (PlayListFile playListFile in playListFiles)
                    {
                        pstmt.Bind("@ORDER_NO", orderNo++);
                        pstmt.Bind("@PAUSED_TIME", playListFile.PausedTime.TotalSeconds);
                        pstmt.Bind("@OWNER_SEQ", ownerPlayList.Seq);
                        pstmt.Bind("@PATH", playListFile.Path);

                        result = pstmt.Step();
                        if (result != SQLitePCL.SQLiteResult.DONE)
                        {
                            return result;
                        }

                        // Resets the statement, to that it can be used again (with different parameters).
                        pstmt.Reset();
                        pstmt.ClearBindings();
                    }
                }
                return result;
            });
        }

        /// <summary>
        /// 재생목록 재생 정지 시간을 업데이트 한다.
        /// </summary>
        /// <param name="playListFile">재생목록 파일</param>
        /// <returns></returns>
        public SQLiteResult UpdatePausedTime(PlayListFile playListFile)
        {
            SQLiteResult result = SQLiteResult.EMPTY;
            using (var pstmt = this.conn.Prepare(DML_UPDATE_PAUSED_TIME_PLAYLIST_FILE))
            {
                pstmt.Bind("@PAUSED_TIME", playListFile.PausedTime.TotalSeconds);
                pstmt.Bind("@OWNER_SEQ", playListFile.Seq);
                pstmt.Bind("@PATH", playListFile.Path);

                result = pstmt.Step();
            }
            return result;
        }

        /// <summary>
        /// 재생목록내 파일을 삭제한다.
        /// </summary>
        /// <returns>DB 처리 결과</returns>
        public SQLiteResult DeletePlayListFiles(PlayList ownerPlayList, IEnumerable<PlayListFile> playListFiles)
        {
            return Transactional(() =>
            {
                SQLiteResult result = SQLiteResult.EMPTY;

                using (var pstmt = this.conn.Prepare(DML_DELETE_PLAYLIST_FILE))
                {
                    List<string> subtitleList = new List<string>();

                    foreach (PlayListFile playListFile in playListFiles)
                    {
                        if (playListFile.SubtitleList != null && playListFile.SubtitleList.Count > 0)
                        {
                            subtitleList.AddRange(playListFile.SubtitleList);
                        }

                        pstmt.Bind("@OWNER_SEQ", ownerPlayList.Seq);
                        pstmt.Bind("@PATH", playListFile.Path);
                        result = pstmt.Step();

                        if (result != SQLitePCL.SQLiteResult.DONE)
                        {
                            return result;
                        }

                        // Resets the statement, to that it can be used again (with different parameters).
                        pstmt.Reset();
                        pstmt.ClearBindings();
                    }

                    foreach (string path in subtitleList)
                    {
                        pstmt.Bind("@OWNER_SEQ", ownerPlayList.Seq);
                        pstmt.Bind("@PATH", path);
                        pstmt.Step();

                        // Resets the statement, to that it can be used again (with different parameters).
                        pstmt.Reset();
                        pstmt.ClearBindings();
                    }
                }

                return result;
            });
        }

        /// <summary>
        /// 재생 목록내의 모든 파일을 삭제한다.
        /// </summary>
        /// <returns>DB 처리 결과</returns>
        public SQLiteResult DeletePlayListFiles(PlayList playList)
        {
            SQLiteResult result = SQLiteResult.EMPTY;
            //재생목록 파일 삭제
            using (var pstmt = conn.Prepare(DML_DELETE_PLAYLIST_FILE_ALL))
            {
                pstmt.Bind("@OWNER_SEQ", playList.Seq);
                result = pstmt.Step();
            }
            
            return result;
        }
    }
}
