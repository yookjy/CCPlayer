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
    public class ThumbnailDAO : BaseDAO
    {
        #region 썸네일 SQL 구문
        private const string DDL_CREATE_TABLE_THUMBNAIL =
            @"CREATE TABLE IF NOT EXISTS
                THUMBNAIL_FILE ( 
                    NAME                    VARCHAR(255) NOT NULL
                   ,PARENT_PATH             VARCHAR(255) NOT NULL
                   ,SIZE                    INTEGER NOT NULL
                   ,RUNNING_TIME            NUMERIC NOT NULL
                   ,CREATED_DATETIME        DATETIME NOT NULL
                   ,ADDED_DATETIME          DATETIME NOT NULL
                   ,THUMBNAIL               BLOB NOT NULL
                   ,PRIMARY KEY (NAME, PARENT_PATH, SIZE, CREATED_DATETIME)
                )";

        private const string DML_INSERT_THUMBNAIL =
            @"INSERT OR REPLACE INTO
                THUMBNAIL_FILE (
                    NAME
                   ,PARENT_PATH
                   ,SIZE
                   ,RUNNING_TIME
                   ,CREATED_DATETIME
                   ,ADDED_DATETIME
                   ,THUMBNAIL
                )
                VALUES (
                    @NAME
                   ,@PARENT_PATH
                   ,@SIZE
                   ,@RUNNING_TIME
                   ,@CREATED_DATETIME
                   ,STRFTIME('%Y-%m-%d %H:%M:%f', 'NOW', 'LOCALTIME')
                   ,@THUMBNAIL
                )";

        private const string DML_SELECT_THUMBNAIL =
            @"SELECT M.NAME
                    ,M.PARENT_PATH
                    ,M.SIZE
                    ,M.RUNNING_TIME
                    ,M.CREATED_DATETIME
                    ,M.ADDED_DATETIME
                    @COLUMN
                FROM THUMBNAIL_FILE M
               WHERE M.PARENT_PATH = @PARENT_PATH
                    @ADDITIONAL_CONDITION
               ORDER BY M.PARENT_PATH, M.NAME";

        private const string DML_DELETE_THUMBNAIL =
            @"DELETE FROM THUMBNAIL_FILE @WHERE";

        private const string DML_SUM_THUMBNAIL_SIZE =
            @"SELECT SUM(LENGTH(HEX(THUMBNAIL))/2) AS SIZE FROM THUMBNAIL_FILE";
        #endregion

        /// <summary>
        /// 테이블이 존재하지 않는 경우 생성한다. 
        /// </summary>
        protected override void CheckCreateTable()
        {
            using (var stmt = conn.Prepare(DDL_CREATE_TABLE_THUMBNAIL))
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
        public ThumbnailDAO(SQLiteConnection conn) : base(conn) { }

        /// <summary>
        /// 썸네일 리스트를 조회한다. 실제 이미지 데이터는 제외된다.
        /// </summary>
        /// <param name="folderPath">조회할 폴더의 경로</param>
        /// <param name="thumbnailList">저장할 썸네일 리스트의 인스턴스</param>
        public void LoadThumnailInFolder(string folderPath, ICollection<Thumbnail> thumbnailList)
        {
            using (var stmt = conn.Prepare(DML_SELECT_THUMBNAIL.Replace("@ADDITIONAL_CONDITION", string.Empty).Replace("@COLUMN", string.Empty)))
            {
                stmt.Bind("@PARENT_PATH", folderPath?.ToLower());
                while(stmt.Step() == SQLiteResult.ROW)
                {
                    Thumbnail thumbnail = new Thumbnail()
                    {
                        Name = stmt.GetText("NAME"),
                        ParentPath = stmt.GetText("PARENT_PATH"),
                        Size = (ulong)stmt.GetInteger2("SIZE"),
                        RunningTime = TimeSpan.FromSeconds(stmt.GetFloat2("RUNNING_TIME"))
                    };

                    //추가된 시간 파싱
                    DateTime addedDateTime;
                    if (DateTime.TryParse(stmt.GetText2("ADDED_DATETIME"), out addedDateTime))
                    {
                        thumbnail.AddedDateTime = addedDateTime;
                    }

                    //파일생성 시간 파싱
                    DateTime createdDateTime;
                    if (DateTime.TryParse(stmt.GetText2("CREATED_DATETIME"), out createdDateTime))
                    {
                        thumbnail.CreatedDateTime = createdDateTime;
                    }

                    thumbnailList.Add(thumbnail);
                }
            }
        }

        /// <summary>
        /// 썸네일 리스트를 조회한다. 실제 이미지 데이터는 제외된다.
        /// </summary>
        /// <param name="folderPath">조회할 폴더의 경로</param>
        /// <param name="thumbnailList">저장할 썸네일 리스트의 인스턴스</param>
        public Thumbnail GetThumnail(string folderPath, string name)
        {
            Thumbnail thumbnail = null;
            using (var stmt = conn.Prepare(DML_SELECT_THUMBNAIL.Replace("@ADDITIONAL_CONDITION", "AND M.NAME = @NAME").Replace("@COLUMN", string.Empty)))
            {
                stmt.Bind("@PARENT_PATH", folderPath.ToLower());
                stmt.Bind("@NAME", name.ToLower());
                if (stmt.Step() == SQLiteResult.ROW)
                {
                    thumbnail = new Thumbnail()
                    {
                        Name = stmt.GetText("NAME"),
                        ParentPath = stmt.GetText("PARENT_PATH"),
                        Size = (ulong)stmt.GetInteger2("SIZE"),
                        RunningTime = TimeSpan.FromSeconds(stmt.GetFloat2("RUNNING_TIME"))
                    };

                    //추가된 시간 파싱
                    DateTime addedDateTime;
                    if (DateTime.TryParse(stmt.GetText2("ADDED_DATETIME"), out addedDateTime))
                    {
                        thumbnail.AddedDateTime = addedDateTime;
                    }

                    //파일생성 시간 파싱
                    DateTime createdDateTime;
                    if (DateTime.TryParse(stmt.GetText2("CREATED_DATETIME"), out createdDateTime))
                    {
                        thumbnail.CreatedDateTime = createdDateTime;
                    }
                }
            }
            return thumbnail;
        }

        public ulong GetThumbnailRetentionSize()
        {
            ulong size = 0;
            using (var pstmt = conn.Prepare(DML_SUM_THUMBNAIL_SIZE))
            {
                if (pstmt.Step() == SQLiteResult.ROW)
                {
                    size = (ulong)pstmt.GetInteger2("SIZE");
                }
            }
            return size;
        }

        /// <summary>
        /// 썸네일 바이너리 데이터를 채운다.
        /// </summary>
        /// <param name="thumbnail">채워질 썸네일 인스턴스</param>
        public void FillThumnailData(Thumbnail thumbnail)
        {
            string condition = @"AND M.NAME = @NAME
                                 AND M.SIZE = @SIZE
                                 AND M.CREATED_DATETIME = @CREATED_DATETIME";

            using (var stmt = conn.Prepare(DML_SELECT_THUMBNAIL.Replace("@ADDITIONAL_CONDITION", condition).Replace("@COLUMN", ",M.THUMBNAIL")))
            {
                stmt.Bind("@NAME", thumbnail.Name.ToLower());
                stmt.Bind("@PARENT_PATH", thumbnail.ParentPath.ToLower());
                stmt.Bind("@SIZE", thumbnail.Size);
                stmt.Bind("@CREATED_DATETIME", thumbnail.CreatedDateTime.ToString("yyyy-MM-dd H:mm:ss.fff"));
                while (stmt.Step() == SQLiteResult.ROW)
                {
                    thumbnail.ThumbnailData = stmt.GetBlob("THUMBNAIL");
                }
            }
        }

        /// <summary>
        /// 특정 썸네일을 삭제한다.
        /// </summary>
        /// <param name="thumbnail">삭제할 썸네일 인스턴스</param>
        /// <returns>DB 처리 결과</returns>
        public SQLiteResult DeleteThumbnail(Thumbnail thumbnail)
        {
            SQLiteResult result = SQLiteResult.EMPTY;
            string where = @"WHERE M.NAME = @NAME
                               AND M.PARENT_PATH = @PARENT_PATH 
                               AND M.SIZE = @SIZE
                               AND M.CREATED_DATETIME = @CREATED_DATETIME";

            using (var stmt = conn.Prepare(DML_DELETE_THUMBNAIL.Replace("@WHERE", where)))
            {
                stmt.Bind("@NAME", thumbnail.Name.ToLower());
                stmt.Bind("@PARENT_PATH", thumbnail.ParentPath.ToLower());
                stmt.Bind("@SIZE", thumbnail.Size);
                stmt.Bind("@CREATED_DATETIME", thumbnail.CreatedDateTime.ToString("yyyy-MM-dd H:mm:ss.fff"));

                result = stmt.Step();
            }

            return result;
        }

        /// <summary>
        /// 특정 썸네일을 삭제한다.
        /// </summary>
        /// <param name="thumbnail">삭제할 썸네일 인스턴스</param>
        /// <returns>DB 처리 결과</returns>
        public SQLiteResult DeletePastPeriodThumbnail(int days)
        {
            SQLiteResult result = SQLiteResult.EMPTY;

            if (days > 0)
            {
                string where = @"WHERE ADDED_DATETIME < @DATE";
                using (var stmt = conn.Prepare(DML_DELETE_THUMBNAIL.Replace("@WHERE", where)))
                {
                    stmt.Bind("@DATE", DateTime.Now.AddDays(days * -1).ToString("yyyy-MM-dd H:mm:ss.fff"));
                    result = stmt.Step();
                }
            }
            else
            {
                using (var stmt = conn.Prepare(DML_DELETE_THUMBNAIL.Replace("@WHERE", string.Empty)))
                {
                    result = stmt.Step();
                }
            }

            return result;
        }

        /// <summary>
        /// 썸네일을 일괄 등록한다.
        /// </summary>
        /// <param name="thumbnailList">썸네일 리스트</param>
        /// <returns>DB 처리 결과</returns>
        public SQLiteResult InsertThumbnails(IEnumerable<Thumbnail> thumbnailList)
        {
            return Transactional(() =>
            {
                SQLiteResult result = SQLiteResult.EMPTY;

                using (var custstmt = this.conn.Prepare(DML_INSERT_THUMBNAIL))
                {
                    foreach (var thumbnail in thumbnailList)
                    {
                        custstmt.Bind("@NAME", thumbnail.Name.ToLower());
                        custstmt.Bind("@PARENT_PATH", thumbnail.ParentPath.ToLower());
                        custstmt.Bind("@SIZE", thumbnail.Size);
                        custstmt.Bind("@RUNNING_TIME", thumbnail.RunningTime.TotalSeconds);
                        custstmt.Bind("@CREATED_DATETIME", thumbnail.CreatedDateTime.ToString("yyyy-MM-dd H:mm:ss.fff"));
                        result = custstmt.Step();

                        if (result != SQLitePCL.SQLiteResult.DONE)
                        {
                            return result;
                        }

                        // Resets the statement, to that it can be used again (with different parameters).
                        custstmt.Reset();
                        custstmt.ClearBindings();
                    }
                }

                return result;
            });
        }

        /// <summary>
        /// 썸네일을 등록한다.
        /// </summary>
        /// <param name="thumbnail">썸네일</param>
        /// <returns>DB 처리 결과</returns>
        public SQLiteResult InsertThumbnail(Thumbnail thumbnail)
        {
            SQLiteResult result = SQLiteResult.EMPTY;

            using (var custstmt = this.conn.Prepare(DML_INSERT_THUMBNAIL))
            {
                custstmt.Bind("@NAME", thumbnail.Name.ToLower());
                custstmt.Bind("@PARENT_PATH", thumbnail.ParentPath.ToLower());
                custstmt.Bind("@SIZE", thumbnail.Size);
                custstmt.Bind("@RUNNING_TIME", thumbnail.RunningTime.TotalSeconds);
                custstmt.Bind("@CREATED_DATETIME", thumbnail.CreatedDateTime.ToString("yyyy-MM-dd H:mm:ss.fff"));
                custstmt.Bind("@THUMBNAIL", thumbnail.ThumbnailData);
                result = custstmt.Step();
            }

            return result;
        }
    }
}
