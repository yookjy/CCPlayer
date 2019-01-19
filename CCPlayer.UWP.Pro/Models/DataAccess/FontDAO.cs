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
    public class FontDAO : BaseDAO
    {
        #region 썸네일 SQL 구문
        private const string DDL_CREATE_TABLE_TEMP_FONT =
            @"CREATE TABLE IF NOT EXISTS
                TEMP_FONT_FILE ( 
                    PATH             VARCHAR(255)  PRIMARY KEY NOT NULL 
                )";

        private const string DML_INSERT_TEMP_FONT =
            @"INSERT OR REPLACE INTO
                TEMP_FONT_FILE (
                    PATH
                )
                VALUES (
                    @PATH
                )";

        private const string DML_SELECT_TEMP_FONT =
            @"SELECT PATH
                FROM TEMP_FONT_FILE";

        private const string DML_DELETE_TEMP_FONT =
            @"DELETE FROM TEMP_FONT_FILE WHERE PATH = @PATH";

        #endregion

        /// <summary>
        /// 테이블이 존재하지 않는 경우 생성한다. 
        /// </summary>
        protected override void CheckCreateTable()
        {
            using (var stmt = conn.Prepare(DDL_CREATE_TABLE_TEMP_FONT))
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
        public FontDAO(SQLiteConnection conn) : base(conn) { }

        /// <summary>
        /// 삭제할 폰트를 조회한다. 
        /// </summary>
        /// <returns>삭제할 폰트 리스트</returns>
        public List<string> GetTempFontList()
        {
            using (var stmt = conn.Prepare(DML_SELECT_TEMP_FONT))
            {
                var list = new List<string>();
                while(stmt.Step() == SQLiteResult.ROW)
                {
                    list.Add(stmt.GetText("PATH"));
                }
                return list;
            }
        }
        
        /// <summary>
        /// 폰트 경로를 삭제한다.
        /// </summary>
        /// <param name="path">삭제할 폰트 경로</param>
        public void DeleteTempFont(IEnumerable<string> paths)
        {
            foreach(var path in paths)
            {
                using (var pstmt = conn.Prepare(DML_DELETE_TEMP_FONT))
                {
                    pstmt.Bind("@PATH", path);
                    pstmt.Step();

                    // Resets the statement, to that it can be used again (with different parameters).
                    pstmt.Reset();
                    pstmt.ClearBindings();
                }
            }
        }

        public void InsertTempFont(string path)
        {
            using (var pstmt = conn.Prepare(DML_INSERT_TEMP_FONT))
            {
                pstmt.Bind("@PATH", path);
                pstmt.Step();
            }
        }

    }
}
