using CCPlayer.UWP.Extensions;
using SQLitePCL;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage.AccessCache;

namespace CCPlayer.UWP.Models.DataAccess
{
    public class FileDAO : BaseDAO
    {
        #region 미디어 SQL 구문
        private const string DDL_CREATE_TABLE_MEDIA =
            @"CREATE TABLE IF NOT EXISTS
                MEDIA_FILE ( 
                    SEQ                     INTEGER PRIMARY KEY NOT NULL
                   ,PATH                    VARCHAR(255) UNIQUE NOT NULL
                   ,NAME                    VARCHAR(255) NOT NULL
                   ,FAL_TOKEN               VARCHAR(255) 
                )";

        private const string DML_INSERT_MEDIA =
            @"INSERT OR REPLACE INTO
                MEDIA_FILE (
                    PATH
                   ,NAME
                   ,FAL_TOKEN
                )
                VALUES (
                    @PATH
                   ,@NAME
                   ,@FAL_TOKEN
                )";

        private const string DML_SELECT_MEDIA =
            @"SELECT M.SEQ
                    ,M.PATH
                    ,M.NAME
                    ,M.FAL_TOKEN
                    ,CASE WHEN EXISTS (SELECT 1 FROM SUBTITLE_FILE S WHERE S.OWNER_SEQ = M.SEQ) THEN 'Y' ELSE 'N' END AS HAS_SUBTITLE
                FROM MEDIA_FILE M
               @WHERE
               ORDER BY M.NAME, M.PATH";

        private const string DML_DELETE_MEDIA_ALL =
            @"DELETE FROM MEDIA_FILE";

        #endregion

        //#region 자막 SQL 구문
        //private const string DDL_CREATE_TABLE_SUBTITLE =
        //    @"CREATE TABLE IF NOT EXISTS
        //        SUBTITLE_FILE ( 
        //            PATH                    VARCHAR(255) PRIMARY KEY NOT NULL
        //           ,OWNER_SEQ               INTEGER NOT NULL
        //           ,FAL_TOKEN               VARCHAR(255) 
        //        )";

        //private const string DML_INSERT_SUBTITLE =
        //    @"INSERT OR REPLACE INTO
        //        SUBTITLE_FILE (
        //            PATH
        //           ,OWNER_SEQ
        //           ,FAL_TOKEN
        //        )
        //        VALUES (
        //            @PATH
        //           ,@OWNER_SEQ
        //           ,@FAL_TOKEN
        //        )";

        //private const string DML_SELECT_SUBTITLE =
        //    @"SELECT PATH
        //            ,OWNER_SEQ
        //            ,FAL_TOKEN
        //        FROM SUBTITLE_FILE
        //       WHERE OWNER_SEQ = @OWNER_SEQ
        //       ORDER BY PATH";

        //private const string DML_DELETE_SUBTITLE =
        //    @"DELETE FROM SUBTITLE_FILE WHERE OWNER_SEQ = @OWNER_SEQ";

        //#endregion

        #region 재생목록 SQL 구문
        //private const string DDL_CREATE_TABLE_PLAYLIST =
        //    @"CREATE TABLE IF NOT EXISTS
        //        PLAY_LIST ( 
        //            PATH                    VARCHAR(255) PRIMARY KEY NOT NULL
        //           ,ADDED_DATETIME          DATETIME
        //           ,ORDER_NO                INTEGER
        //           ,RUNNING_TIME            INTEGER
        //           ,PAUSED_TIME             INTEGER
        //        )";

        //private const string DML_INSERT_PLAYLIST =
        //    @"INSERT OR REPLACE INTO
        //        PLAY_LIST (
        //            PATH
        //           ,ADDED_DATETIME
        //           ,ORDER_NO
        //           ,PAUSED_TIME
        //        )
        //        VALUES (
        //            @PATH
        //           ,STRFTIME('%Y-%m-%d %H:%M:%f', 'NOW', 'LOCALTIME')
        //           ,@ORDER_NO
        //           ,(SELECT PAUSED_TIME FROM PLAY_LIST WHERE PATH = @PATH)
        //        )";

        //private const string DML_UPDATE_PLAYLIST =
        //    @"UPDATE PLAY_LIST SET
        //            RUNNING_TIME = @RUNNING_TIME
        //           ,PAUSED_TIME = @PAUSED_TIME
        //       WHERE PATH = @PATH";

        //private const string DML_SELECT_PLAYLIST =
        //    @"SELECT M.PATH
        //            ,M.NAME
        //            ,M.FAL_TOKEN
        //            ,P.ADDED_DATETIME
        //            ,P.RUNNING_TIME
        //            ,P.PAUSED_TIME
        //            ,CASE WHEN EXISTS (SELECT 1 FROM SUBTITLE_FILE S WHERE S.OWNER_PATH = M.PATH) THEN 'Y' ELSE 'N' END AS HAS_SUBTITLE
        //        FROM ${INLINE_VIEW} P
        //            ,MEDIA_FILE M
        //       WHERE P.PATH = M.PATH";

        //private const string DML_DELETE_PLAYLIST =
        //    @"DELETE FROM PLAY_LIST WHERE PATH = @PATH";

        //private const string DML_CLEAN_PALYLIST =
        //    @"DELETE FROM PLAY_LIST 
        //       WHERE PATH NOT IN (SELECT PATH FROM MEDIA_FILE)";

        #endregion 

        #region 미디어 SQL 구문
        //private const string DDL_CREATE_TABLE_SEEKING =
        //    @"CREATE TABLE IF NOT EXISTS
        //        MEDIA_SEEKING ( 
        //            PATH                 VARCHAR(255) NOT NULL
        //           ,TIMECODE             INTEGER NOT NULL
        //           ,OFFSET               INTEGER NOT NULL
        //           ,PRIMARY KEY (PATH, TIMECODE)
        //        )";

        //private const string DML_INSERT_SEEKING =
        //    @"INSERT OR REPLACE INTO
        //        MEDIA_SEEKING (
        //            PATH
        //           ,TIMECODE
        //           ,OFFSET
        //        )
        //        VALUES (
        //            @PATH
        //           ,@TIMECODE
        //           ,@OFFSET
        //        )";

        //private const string DML_SELECT_SEEKING =
        //    @"SELECT S.PATH
        //            ,S.TIMECODE
        //            ,S.OFFSET
        //        FROM MEDIA_SEEKING S
        //       WHERE S.PATH = @PATH
        //       ORDER BY S.TIMECODE";

        //private const string DML_DELETE_SEEKING =
        //    @"DELETE FROM MEDIA_SEEKING
        //       WHERE PATH NOT IN (SELECT PATH FROM MEDIA_FILE)";

        #endregion
        /// <summary>
        /// 테이블이 존재하지 않는 경우 생성한다. 
        /// </summary>
        protected override void CheckCreateTable()
        {
            using (var stmt = conn.Prepare(DDL_CREATE_TABLE_MEDIA))
            {
                stmt.Step();
            }

            //using (var stmt = conn.Prepare(DDL_CREATE_TABLE_SUBTITLE))
            //{
            //    stmt.Step();
            //}

            //using (var stmt = conn.Prepare(DDL_CREATE_TABLE_PLAYLIST))
            //{
            //    stmt.Step();
            //}

            //using (var stmt = conn.Prepare(DDL_CREATE_TABLE_SEEKING))
            //{
            //    stmt.Step();
            //}
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
        public FileDAO(SQLiteConnection conn) : base(conn) { }

        /// <summary>
        /// 보호되는 폴더 목록을 얻어온다.
        /// </summary>
        /// <returns>보호되는 폴더 목록</returns>
        //public List<FolderInfo> GetProtectedFolderList()
        //{
        //    List<FolderInfo> pFiList = new List<FolderInfo>();
        //    using (var stmt = conn.Prepare(FolderDAO.DML_SELECT_PROTECTED_FOLDER))
        //    {
        //        stmt.Bind("@FOLDER_TYPE", (int)FolderType.Root);
        //        while (stmt.Step() == SQLiteResult.ROW)
        //        {
        //            pFiList.Add(new FolderInfo()
        //            {
        //                Path = stmt.GetText2("PATH"),
        //                Passcode = stmt.GetText2("PASSCODE")
        //            });
        //        }
        //    }
        //    return pFiList;
        //}

        /// <summary>
        /// 모든 비디오 목록을 로딩한다.
        /// </summary>
        /// <param name="allVideoList">로딩될 리스트</param>
        //public void LoadAllVideoList(ICollection<MediaInfo> allVideoList, ICollection<MediaInfo> playList)
        //{
        //    //보호되는 폴더 목록
        //    List<FolderInfo> pFiList = GetProtectedFolderList();

        //    using (var stmt = conn.Prepare(DML_SELECT_MEDIA.Replace("@WHERE", string.Empty)))
        //    {
        //        string prevItemName = string.Empty;

        //        while (stmt.Step() == SQLiteResult.ROW)
        //        {
        //            MediaInfo fi = new MediaInfo()
        //            {
        //                Path = stmt.GetText("PATH"),
        //                Name = stmt.GetText("NAME"),
        //                FalToken = stmt.GetText2("FAL_TOKEN"),
        //                IsAddedPlaylist = playList.Any(x => x.Path == stmt.GetText("PATH"))
        //            };

        //            if (!pFiList.Any(x => fi.Path.Contains(x.Path)))
        //            {
        //                if (stmt.GetText("HAS_SUBTITLE") == "Y")
        //                {
        //                    using (var stmt2 = conn.Prepare(DML_SELECT_SUBTITLE))
        //                    {
        //                        stmt2.Bind("@OWNER_PATH", fi.Path);
        //                        while (stmt2.Step() == SQLiteResult.ROW)
        //                        {
        //                            fi.AddSubtitle(new SubtitleInfo
        //                            {
        //                                Path = stmt2.GetText("PATH"),
        //                                Owner = stmt2.GetText("OWNER_PATH"),
        //                                FalToken = stmt2.GetText2("FAL_TOKEN")
        //                            });
        //                        }
        //                    }
        //                }

        //                //이름이 중복되는 경우 루트 경로를 표시
        //                if (prevItemName == fi.Name)
        //                {
        //                    fi.Name = string.Format("{0} ({1})", fi.Name, Path.GetPathRoot(fi.Path));
        //                }

        //                allVideoList.Add(fi);
        //                prevItemName = fi.Name;
        //            }
        //        }
        //    }
        //}

        /// <summary>
        /// 모든 비디오을 검색한다.
        /// </summary>
        /// <param name="allVideoList">로딩될 리스트</param>
        //public void SearchAllVideoList(ICollection<MediaInfo> allVideoList, string searchWord)
        //{
        //    //보호되는 폴더 목록
        //    List<FolderInfo> pFiList = GetProtectedFolderList();

        //    using (var stmt = conn.Prepare(DML_SELECT_MEDIA.Replace("@WHERE", "WHERE LOWER(NAME) LIKE '%' || @NAME || '%'")))
        //    {
        //        stmt.Bind("@NAME", searchWord);
        //        string prevItemName = string.Empty;

        //        while (stmt.Step() == SQLiteResult.ROW)
        //        {
        //            MediaInfo fi = new MediaInfo()
        //            {
        //                Path = stmt.GetText("PATH"),
        //                Name = stmt.GetText("NAME"),
        //                FalToken = stmt.GetText2("FAL_TOKEN"),
        //            };

        //            if (!pFiList.Any(x => fi.Path.Contains(x.Path)))
        //            {
        //                if (stmt.GetText("HAS_SUBTITLE") == "Y")
        //                {
        //                    using (var stmt2 = conn.Prepare(DML_SELECT_SUBTITLE))
        //                    {
        //                        stmt2.Bind("@OWNER_PATH", fi.Path);
        //                        while (stmt2.Step() == SQLiteResult.ROW)
        //                        {
        //                            fi.AddSubtitle(new SubtitleInfo
        //                            {
        //                                Path = stmt2.GetText("PATH"),
        //                                Owner = stmt2.GetText("OWNER_PATH"),
        //                                FalToken = stmt2.GetText2("FAL_TOKEN")
        //                            });
        //                        }
        //                    }
        //                }

        //                //이름이 중복되는 경우 루트 경로를 표시
        //                if (prevItemName == fi.Name)
        //                {
        //                    fi.Name = string.Format("{0} ({1})", fi.Name, Path.GetPathRoot(fi.Path));
        //                }

        //                allVideoList.Add(fi);
        //                prevItemName = fi.Name;
        //            }
        //        }
        //    }
        //}

        //// <summary>
        // 모든비디오 목록을 삭제한다.
        /// </summary>
        /// <returns>DB 처리 결과</returns>
        public SQLiteResult DeleteAllVideos()
        {
            //모든 미디어 FAL 삭제 (특별한 경우를 제외하고는 수행되지 않음 - 상위 폴더가 FAL에 등록되어 있으면 하위 파일은 접근 권한을 갖기 때문)
            foreach (var entry in StorageApplicationPermissions.FutureAccessList.Entries)
            {
                if (entry.Metadata == typeof(MediaInfo).ToString())
                {
                    StorageApplicationPermissions.FutureAccessList.Remove(entry.Token);
                }
            }

            SQLiteResult result = SQLiteResult.EMPTY;
            using (var stmt = conn.Prepare(DML_DELETE_MEDIA_ALL))
            {
                result = stmt.Step();
            }

            //탐색 목록도 삭제
            //using (var stmt = conn.Prepare(DML_DELETE_SEEKING))
            //{
            //    result = stmt.Step();
            //}

            return result;
        }

        /// <summary>
        /// 미디어 리스트를 일괄 등록한다.
        /// </summary>
        /// <param name="storageItemList">미디어 정보 리스트</param>
        /// <returns>DB 처리 결과</returns>
        public SQLiteResult InsertMedia(IEnumerable<StorageItemInfo> storageItemList)
        {
            return Transactional(() =>
            {
                SQLiteResult result = SQLiteResult.EMPTY;

                using (var custstmt = this.conn.Prepare(DML_INSERT_MEDIA))
                {
                    foreach (var item in storageItemList)
                    {
                        custstmt.Bind("@PATH", item.Path);
                        custstmt.Bind("@NAME", item.Name);
                        custstmt.Bind("@FAL_TOKEN", item.FalToken);
                        result = custstmt.Step();

                        if (result != SQLitePCL.SQLiteResult.DONE)
                        {
                            return result;
                        }

                        // Resets the statement, to that it can be used again (with different parameters).
                        custstmt.Reset();
                        custstmt.ClearBindings();

                        using (var pkPstmt = conn.Prepare("select last_insert_rowid()"))
                        {
                            if (pkPstmt.Step() == SQLiteResult.ROW)
                            {
                                //playList.Seq = pkPstmt.GetInteger(0);
                            }
                        }
                    }
                }

                return result;
            });
        }

        /// <summary>
        /// 자막 리스트를 일괄 등록한다.
        /// </summary>
        /// <param name="mediaInfoList">미디어정보 리스트</param>
        /// <returns>DB 처리 결과</returns>
        //public SQLiteResult InsertSubtitles(MediaInfo mediaInfo)
        //{
        //    if (mediaInfo.SubtitleFileList != null)
        //    {
        //        return Transactional(() =>
        //        {
        //            SQLiteResult result = SQLiteResult.EMPTY;

        //            using (var custstmt = this.conn.Prepare(DML_INSERT_SUBTITLE))
        //            {
        //                foreach (var subtitle in mediaInfo.SubtitleFileList)
        //                {
        //                    custstmt.Bind("@PATH", subtitle.Path);
        //                    custstmt.Bind("@OWNER_SEQ", mediaInfo.Path);
        //                    custstmt.Bind("@FAL_TOKEN", subtitle.FalToken);
        //                    result = custstmt.Step();

        //                    if (result != SQLitePCL.SQLiteResult.DONE)
        //                    {
        //                        return result;
        //                    }

        //                    // Resets the statement, to that it can be used again (with different parameters).
        //                    custstmt.Reset();
        //                    custstmt.ClearBindings();
        //                }
        //            }

        //            return result;
        //        });
        //    }
        //    else
        //    {
        //        return SQLiteResult.DONE;
        //    }
        //}

        /// <summary>
        /// 모든 자막 목록을 삭제한다.
        /// </summary>
        /// <returns>DB 처리 결과</returns>
        //public SQLiteResult DeleteSubtitle(SubtitleInfo sif)
        //{
        //    //모든 자막 FAL 삭제 (특별한 경우를 제외하고는 수행되지 않음 - 상위 폴더가 FAL에 등록되어 있으면 하위 파일은 접근 권한을 갖기 때문)
        //    foreach (var entry in StorageApplicationPermissions.FutureAccessList.Entries)
        //    {
        //        if (entry.Metadata == typeof(SubtitleInfo).ToString())
        //        {
        //            StorageApplicationPermissions.FutureAccessList.Remove(entry.Token);
        //        }
        //    }

        //    using (var pstmt = conn.Prepare(DML_DELETE_SUBTITLE))
        //    {
        //        pstmt.Bind("@OWNER_SEQ", sif.Path) ;
        //        return pstmt.Step();
        //    }
        //}

        /// <summary>
        /// 재생목록 로우 펫치
        /// </summary>
        /// <param name="stmt"></param>
        /// <returns></returns>
        private MediaInfo GetRowDataForPlayList(ISQLiteStatement stmt)
        {
            var mi = new MediaInfo()
            {
                Path = stmt.GetText("PATH"),
                Name = stmt.GetText("NAME"),
                FalToken = stmt.GetText2("FAL_TOKEN"),
                RunningTime = stmt.GetInteger2("RUNNING_TIME"),
                PausedTime = stmt.GetInteger2("PAUSED_TIME"),
            };
            //재생목록에 추가된 시간 파싱
            DateTime AddedDateTime;
            if (DateTime.TryParse(stmt.GetText2("ADDED_DATETIME"), out AddedDateTime))
            {
                mi.AddedDateTime = AddedDateTime;
            }
            return mi;
        }

        /// <summary>
        /// 재생목록 중 해당경로에 따른 단건을 조회한다.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private MediaInfo GetPlayList(string query, string path)
        {
            MediaInfo mi = null;
            using (var stmt = conn.Prepare(query))
            {
                stmt.Bind("@PATH", path);
                if (stmt.Step() == SQLiteResult.ROW)
                {
                    mi = GetRowDataForPlayList(stmt);

                    //if (stmt.GetText("HAS_SUBTITLE") == "Y")
                    //{
                    //    using (var stmt2 = conn.Prepare(DML_SELECT_SUBTITLE))
                    //    {
                    //        stmt2.Bind("@OWNER_PATH", mi.Path);
                    //        while (stmt2.Step() == SQLiteResult.ROW)
                    //        {
                    //            mi.AddSubtitle(new SubtitleInfo
                    //            {
                    //                Path = stmt2.GetText("PATH"),
                    //                Owner = stmt2.GetText("OWNER_PATH"),
                    //                FalToken = stmt2.GetText2("FAL_TOKEN")
                    //            });
                    //        }
                    //    }
                    //}
                }
            }
            return mi;
        }

        /// <summary>
        /// 재생목록 중 해당경로 다음 미디어 정보를 조회한다.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        //public MediaInfo GetPrevMediaInfo(string path)
        //{
        //    var query = @"(SELECT *
        //                    FROM PLAY_LIST PL
        //                   WHERE STRFTIME('%Y%m%d%H%M%f', PL.ADDED_DATETIME) || PL.ORDER_NO >= (SELECT STRFTIME('%Y%m%d%H%M%f', L.ADDED_DATETIME) || L.ORDER_NO FROM PLAY_LIST L WHERE L.PATH = @PATH)
        //                     AND PL.PATH <> @PATH
        //                   ORDER BY PL.ADDED_DATETIME, PL.ORDER_NO
        //                   LIMIT 1)";

        //    return GetPlayList(DML_SELECT_PLAYLIST.Replace("${INLINE_VIEW}", query), path);
        //}

        /// <summary>
        /// 재생목록 중 해당경로에 따른 단건을 조회한다.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        //public MediaInfo GetPlayList(string path)
        //{
        //    var query = new StringBuilder(DML_SELECT_PLAYLIST)
        //    .Replace("${INLINE_VIEW}", "PLAY_LIST")
        //    .AppendLine()
        //    .AppendLine("AND P.PATH = @PATH");

        //    return GetPlayList(query.ToString(), path);
        //}

        /// <summary>
        /// 재생목록 중 해당경로 다음 미디어 정보를 조회한다.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        //public MediaInfo GetNextMediaInfo(string path)
        //{
        //    var query = @"(SELECT *
        //                    FROM PLAY_LIST PL
        //                   WHERE STRFTIME('%Y%m%d%H%M%f', PL.ADDED_DATETIME) || PL.ORDER_NO <= (SELECT STRFTIME('%Y%m%d%H%M%f', L.ADDED_DATETIME) || L.ORDER_NO FROM PLAY_LIST L WHERE L.PATH = @PATH)
        //                     AND PL.PATH <> @PATH
        //                   ORDER BY PL.ADDED_DATETIME DESC, PL.ORDER_NO DESC
        //                   LIMIT 1)";

        //    return GetPlayList(DML_SELECT_PLAYLIST.Replace("${INLINE_VIEW}", query), path);
        //}

        /// <summary>
        /// 재생 목록을 로딩한다.
        /// </summary>
        /// <param name="allVideoList">로딩될 리스트</param>
        //public void LoadPlayList(ICollection<MediaInfo> playList, int count, int skip, bool loadSubtitle)
        //{
        //    var query = new StringBuilder(DML_SELECT_PLAYLIST)
        //    .Replace("${INLINE_VIEW}", "PLAY_LIST")
        //    .AppendLine()
        //    .AppendLine("ORDER BY P.ADDED_DATETIME DESC, P.ORDER_NO DESC, M.NAME")
        //    .AppendLine("LIMIT @COUNT OFFSET @SKIP");

        //    using (var stmt = conn.Prepare(query.ToString()))
        //    {
        //        string prevItemName = string.Empty;
        //        stmt.Bind("@COUNT", count);
        //        stmt.Bind("@SKIP", skip);

        //        while (stmt.Step() == SQLiteResult.ROW)
        //        {
        //            MediaInfo mi = GetRowDataForPlayList(stmt);

        //            if (loadSubtitle && stmt.GetText("HAS_SUBTITLE") == "Y")
        //            {
        //                using (var stmt2 = conn.Prepare(DML_SELECT_SUBTITLE))
        //                {
        //                    stmt2.Bind("@OWNER_PATH", mi.Path);
        //                    while (stmt2.Step() == SQLiteResult.ROW)
        //                    {
        //                        mi.AddSubtitle(new SubtitleInfo
        //                        {
        //                            Path = stmt2.GetText("PATH"),
        //                            Owner = stmt2.GetText("OWNER_PATH"),
        //                            FalToken = stmt2.GetText2("FAL_TOKEN")
        //                        });
        //                    }
        //                }
        //            }

        //            //이름이 중복되는 경우 루트 경로를 표시
        //            if (prevItemName == mi.Name)
        //            {
        //                mi.Name = string.Format("{0} ({1})", mi.Name, Path.GetPathRoot(mi.Path));
        //            }

        //            playList.Add(mi);
        //            prevItemName = mi.Name;
        //        }
        //    }
        //}

        /// <summary>
        /// 재생 목록을 일괄 등록한다.
        /// </summary>
        /// <param name="subtitleList">재생 목록</param>
        /// <returns>DB 처리 결과</returns>
        //public SQLiteResult InsertPlayList(IEnumerable<MediaInfo> playList)
        //{
        //    return Transactional(() =>
        //    {
        //        SQLiteResult result = SQLiteResult.EMPTY;

        //        using (var custstmt = this.conn.Prepare(DML_INSERT_PLAYLIST))
        //        {
        //            int orderNo = 1;
        //            foreach (var playItem in playList)
        //            {
        //                custstmt.Bind("@PATH", playItem.Path);
        //                custstmt.Bind("@ORDER_NO", orderNo++);
        //                result = custstmt.Step();

        //                if (result != SQLitePCL.SQLiteResult.DONE)
        //                {
        //                    return result;
        //                }

        //                // Resets the statement, to that it can be used again (with different parameters).
        //                custstmt.Reset();
        //                custstmt.ClearBindings();
        //            }
        //        }

        //        return result;
        //    });
        //}

        /// <summary>
        /// 재생목록을 업데이트 한다.
        /// </summary>
        /// <param name="playListItem"></param>
        /// <returns></returns>
        //public SQLiteResult UpdatePlayList(IEnumerable<object> playList)
        //{
        //    return Transactional(() =>
        //    {
        //        SQLiteResult result = SQLiteResult.EMPTY;
        //        using (var custstmt = this.conn.Prepare(DML_UPDATE_PLAYLIST))
        //        {
        //            foreach (MediaInfo playItem in playList)
        //            {
        //                custstmt.Bind("@RUNNING_TIME", playItem.RunningTime);
        //                custstmt.Bind("@PAUSED_TIME", playItem.PausedTime);
        //                custstmt.Bind("@PATH", playItem.Path);
        //                result = custstmt.Step();
        //                if (result != SQLitePCL.SQLiteResult.DONE)
        //                {
        //                    return result;
        //                }

        //                // Resets the statement, to that it can be used again (with different parameters).
        //                custstmt.Reset();
        //                custstmt.ClearBindings();
        //            }
        //        }

        //        return result;
        //    });
        //}

        /// <summary>
        /// 모든 재생 목록을 삭제한다.
        /// </summary>
        /// <returns>DB 처리 결과</returns>
        //public SQLiteResult DeletePlayList(IEnumerable<MediaInfo> playList)
        //{
        //    return Transactional(() =>
        //    {
        //        SQLiteResult result = SQLiteResult.EMPTY;

        //        using (var custstmt = this.conn.Prepare(DML_DELETE_PLAYLIST))
        //        {
        //            foreach (var playItem in playList)
        //            {
        //                custstmt.Bind("@PATH", playItem.Path);
        //                result = custstmt.Step();

        //                if (result != SQLitePCL.SQLiteResult.DONE)
        //                {
        //                    return result;
        //                }

        //                // Resets the statement, to that it can be used again (with different parameters).
        //                custstmt.Reset();
        //                custstmt.ClearBindings();
        //            }
        //        }

        //        return result;
        //    });
        //}

        /// <summary>
        /// 재생목록과 파일 목록을 비교하여 삭제된 파일들에 대해 재생목록에서 삭제한다.
        /// </summary>
        /// <returns>DB 처리 결과</returns>
        //public SQLiteResult CleanPlayList()
        //{
        //    SQLiteResult result = SQLiteResult.EMPTY;

        //    using (var custstmt = this.conn.Prepare(DML_CLEAN_PALYLIST))
        //    {
        //        result = custstmt.Step();
        //    }
        //    return result;
        //}

        /// <summary>
        /// 탐색 데이터 목록을 로딩한다.
        /// </summary>
        /// <param name="allVideoList">로딩될 리스트</param>
        //public SortedDictionary<long, long> GetSeekingList(string path)
        //{
        //    SortedDictionary<long, long> seekData = null;
        //    using (var stmt = conn.Prepare(DML_SELECT_SEEKING))
        //    {
        //        stmt.Bind("@PATH", path);

        //        while (stmt.Step() == SQLiteResult.ROW)
        //        {
        //            if (seekData == null)
        //            {
        //                seekData = new SortedDictionary<long, long>();
        //            }

        //            seekData[stmt.GetInteger2("TIMECODE")] = stmt.GetInteger2("OFFSET");
        //        }
        //    }
        //    return seekData;
        //}

        /// <summary>
        /// 탐색 데이터 목록을 삭제한다.
        /// </summary>
        /// <returns>DB 처리 결과</returns>
        //public SQLiteResult DeleteSeekingData()
        //{
        //    using (var stmt = conn.Prepare(DML_DELETE_SEEKING))
        //    {
        //        return stmt.Step();
        //    }
        //}

        /// <summary>
        /// 탐색 데이터 목록을 일괄 등록한다.
        /// </summary>
        /// <param name="mediaList">미디어 정보 리스트</param>
        /// <returns>DB 처리 결과</returns>
        //public SQLiteResult InsertSeekingData(string path, SortedDictionary<long, long> seekData)
        //{
        //    return Transactional(() =>
        //    {
        //        SQLiteResult result = SQLiteResult.EMPTY;

        //        using (var custstmt = this.conn.Prepare(DML_INSERT_SEEKING))
        //        {
        //            foreach (var data in seekData)
        //            {
        //                custstmt.Bind("@PATH", path);
        //                custstmt.Bind("@TIMECODE", data.Key);
        //                custstmt.Bind("@OFFSET", data.Value);
        //                result = custstmt.Step();

        //                if (result != SQLitePCL.SQLiteResult.DONE)
        //                {
        //                    return result;
        //                }

        //                // Resets the statement, to that it can be used again (with different parameters).
        //                custstmt.Reset();
        //                custstmt.ClearBindings();
        //            }
        //        }

        //        return result;
        //    });
        //}
    }
}
