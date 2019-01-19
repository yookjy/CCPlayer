using CCPlayer.WP81.Extensions;
using SQLitePCL;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Storage.AccessCache;

namespace CCPlayer.WP81.Models.DataAccess
{
    public class FolderDAO : BaseDAO
    {
        #region SQL 구문
        private const string DDL_CREATE_TABLE =
            @"CREATE TABLE IF NOT EXISTS
                FOLDER ( 
                    PATH                    VARCHAR(255) NOT NULL
                   ,FOLDER_TYPE             INTEGER
                   ,LEVEL                   NUMERIC
                   ,NAME                    VARCHAR(255) NOT NULL
                   ,FAL_TOKEN               VARCHAR(255) 
                   ,PRIMARY KEY (PATH, FOLDER_TYPE)
                )";

        private const string DDL_ALTER_TABLE =
            @"ALTER TABLE FOLDER ADD COLUMN 'PASSCODE' VARCHAR(4)";

        private const string DML_INSERT =
            @"INSERT INTO
                FOLDER (
                    PATH
                   ,FOLDER_TYPE
                   ,LEVEL
                   ,NAME
                   ,FAL_TOKEN
                   ,PASSCODE
                )
                VALUES (
                    @PATH
                   ,@FOLDER_TYPE
                   ,@LEVEL
                   ,@NAME
                   ,@FAL_TOKEN
                   ,@PASSCODE
                )";

        private const string DML_UPDATE =
            @"UPDATE FOLDER 
                 SET PASSCODE = @PASSCODE 
               WHERE PATH = @PATH 
                 AND FOLDER_TYPE = @FOLDER_TYPE";

        private const string DML_SELECT =
            @"SELECT PATH
                    ,FOLDER_TYPE
                    ,LEVEL
                    ,NAME
                    ,PASSCODE
                    ,FAL_TOKEN
                    ,PASSCODE
                    ${COLUMN}
                FROM FOLDER
               WHERE FOLDER_TYPE = @FOLDER_TYPE";

        public const string DML_SELECT_PROTECTED_FOLDER =
            @"SELECT PATH
                    ,FOLDER_TYPE
                    ,LEVEL
                    ,NAME
                    ,PASSCODE
                    ,FAL_TOKEN
                    ,PASSCODE
                FROM FOLDER
               WHERE PASSCODE IS NOT NULL 
                 AND PASSCODE <> ''
                 AND FOLDER_TYPE = @FOLDER_TYPE";

        private const string DML_DELETE =
            @"DELETE FROM FOLDER WHERE PATH = @PATH AND FOLDER_TYPE = @FOLDER_TYPE";

//        private const string DML_DELETE_NO_TYPE =
//            @"DELETE FROM FOLDER 
//               WHERE FOLDER_RELATIVE_ID IN (SELECT F.FOLDER_RELATIVE_ID 
//                                              FROM FOLDER F 
//                                             WHERE NOT EXISTS (SELECT 1 FROM FOLDER_TYPE T WHERE T.FOLDER_RELATIVE_ID = F.FOLDER_RELATIVE_ID))";

        #endregion 

        /// <summary>
        /// 테이블 생성여부를 체크하여, 생성되지 않은 경우 생성한다.
        /// </summary>
        protected override void CheckCreateTable()
        {
            using (var stmt = conn.Prepare(DDL_CREATE_TABLE))
            {
                stmt.Step();
            }
        }

        /// <summary>
        /// 테이블에 변경된 사항 반영
        /// </summary>
        protected override void CheckAlterTable()
        {
            bool exist = false;
            using (var stmt = conn.Prepare("SELECT * FROM FOLDER LIMIT 0"))
            {
                    
                for(int i=0; i<stmt.ColumnCount; i++)
                {
                    string name = stmt.ColumnName(i);
                    if (name == "PASSCODE")
                    {
                        exist = true;
                    }
                }
            }

            if (!exist)
            {
                using (var stmt = conn.Prepare(DDL_ALTER_TABLE))
                {
                    stmt.Step();
                }
            }
        }

        public FolderDAO(SQLiteConnection conn) : base(conn) { }

        /// <summary>
        /// 폴더 정보를 DB에 저장한다.
        /// </summary>
        /// <param name="folderInfo"></param>
        /// <returns></returns>
        public SQLiteResult Insert(FolderInfo folderInfo)
        {
            SQLiteResult result = SQLiteResult.EMPTY;
            //폴더 등록
            using (var stmt = conn.Prepare(DML_INSERT))
            {
                stmt.Bind("@FOLDER_TYPE", (int)folderInfo.Type);
                stmt.Bind("@LEVEL", folderInfo.Level);
                stmt.Bind("@NAME", folderInfo.Name);
                stmt.Bind("@PATH", folderInfo.Path);
                stmt.Bind("@FAL_TOKEN", folderInfo.FalToken);
                stmt.Bind("@PASSCODE", folderInfo.Passcode);

                result = stmt.Step();
            }

            return result;
        }

        /// <summary>
        /// 폴더 정보를 DB에 저장한다.
        /// </summary>
        /// <param name="folderInfo"></param>
        /// <returns></returns>
        public SQLiteResult Update(FolderInfo folderInfo)
        {
            SQLiteResult result = SQLiteResult.EMPTY;
            //폴더 등록
            using (var stmt = conn.Prepare(DML_UPDATE))
            {
                stmt.Bind("@FOLDER_TYPE", (int)folderInfo.Type);
                stmt.Bind("@PATH", folderInfo.Path);
                stmt.Bind("@PASSCODE", folderInfo.Passcode);

                result = stmt.Step();
            }

            return result;
        } 

        /// <summary>
        /// 조회된 한 로우에 대한 데이터를 생성한다.
        /// </summary>
        /// <param name="stmt">DB 구문 객체</param>
        /// <returns>파일 정보</returns>
        private FolderInfo GetRowData(ISQLiteStatement stmt)
        {
            return new FolderInfo()
            {
                Type = (FolderType)stmt.GetInteger("FOLDER_TYPE"),
                Level = stmt.GetInteger2("LEVEL"),
                Name = stmt.GetText("NAME"),
                Path = stmt.GetText("PATH"),
                FalToken = stmt.GetText2("FAL_TOKEN"),
                Passcode = stmt.GetText2("PASSCODE")
            };
        }

        public List<FolderInfo> GetProtectedRootFolderList()
        {
            List<FolderInfo> rootFolderList = new List<FolderInfo>();

            using (var stmt = conn.Prepare(DML_SELECT_PROTECTED_FOLDER))
            {
                string prevItemName = string.Empty;
                stmt.Bind("@FOLDER_TYPE", (int)FolderType.Root);

                while (stmt.Step() == SQLitePCL.SQLiteResult.ROW)
                {
                    FolderInfo fi = GetRowData(stmt);
                    rootFolderList.Add(fi);
                    prevItemName = fi.Name;
                }
            }
            return rootFolderList;
        }

        /// <summary>
        /// 탐색기 루트 폴더 리스트를 조회한다.
        /// </summary>
        /// <param name="rootFolderList">조회된 결과를 담을 리스트</param>
        /// <param name="command">리스트의 버튼을 위한 커맨드</param>
        /// <returns>조회된 결과 리스트</returns>
        public void LoadRootFolderList(ICollection<FolderInfo> rootFolderList, ICommand command1, ICommand command2, bool includeProtectedFolder)
        {
            string sql = DML_SELECT.Replace("${COLUMN}", string.Empty);
            if (!includeProtectedFolder)
            {
                sql += " AND (PASSCODE IS NULL OR PASSCODE = '') ";
            }

            using (var stmt = conn.Prepare(sql))
            {
                string prevItemName = string.Empty;
                stmt.Bind("@FOLDER_TYPE", (int)FolderType.Root);

                while (stmt.Step() == SQLitePCL.SQLiteResult.ROW)
                {
                    FolderInfo fi = GetRowData(stmt);

                    if (command1 != null)
                    {
                        fi.ButtonTappedCommand1 = command1;
                    }

                    if (command2 != null)
                    {
                        fi.ButtonTappedCommand2 = command2;
                    }
                    
                    //이름이 중복되는 경우 루트 경로를 표시
                    if (prevItemName == fi.Name)
                    {
                        fi.Name = string.Format("{0} ({1})", fi.Name, Path.GetPathRoot(fi.Path));
                    }

                    rootFolderList.Add(fi);
                    prevItemName = fi.Name;
                }
            }
        }

        /// <summary>
        /// 마지막 접근할 폴더를 조회한다.
        /// </summary>
        /// <returns>마지막 접근할 폴더 정보</returns>
        public FolderInfo GetLastFolder()
        {
            using (var stmt = conn.Prepare(DML_SELECT.Replace("${COLUMN}", string.Empty)))
            {
                stmt.Bind("@FOLDER_TYPE", (int)FolderType.Last);

                if (stmt.Step() == SQLitePCL.SQLiteResult.ROW)
                {
                    return GetRowData(stmt);
                }
            }

            return null;
        }

        /// <summary>
        /// 폴더를 삭제한다.
        /// </summary>
        /// <param name="folderInfo">삭제할 폴더 정보</param>
        /// <returns>DB 처리 결과</returns>
        public SQLiteResult Delete(FolderInfo folderInfo)
        {
            SQLiteResult result = SQLiteResult.EMPTY;
            //폴더 삭제
            using (var stmt = conn.Prepare(DML_DELETE))
            {
                stmt.Bind("@PATH", folderInfo.Path);
                stmt.Bind("@FOLDER_TYPE", (int)folderInfo.Type);
                result = stmt.Step();
            }
                
            //정상적으로 삭제되었으면
            if (result == SQLiteResult.DONE && !string.IsNullOrEmpty(folderInfo.FalToken) && StorageApplicationPermissions.FutureAccessList.ContainsItem(folderInfo.FalToken))
            {
                //FAL삭제
                StorageApplicationPermissions.FutureAccessList.Remove(folderInfo.FalToken);
            }

            return result;
        }

        /// <summary>
        /// 마지막 접근 폴더를 갱신한다.
        /// </summary>
        /// <param name="folderInfo"></param>
        /// <returns></returns>
        public SQLiteResult ReplaceLastFolder(FolderInfo folderInfo)
        {
            SQLiteResult result = SQLiteResult.EMPTY;

            string coumn = @",CASE WHEN (SELECT COUNT(*) 
                                           FROM FOLDER T 
                                          WHERE T.PATH = PATH 
                                            AND T.FOLDER_TYPE = 1) > 0 THEN 'Y' 
                                   ELSE 'N' 
                              END AS ROOT_YN";

            //이전의 마지막 폴더 조회
            using (var stmt = conn.Prepare(DML_SELECT.Replace("${COLUMN}", coumn)))
            {
                string prevItemName = string.Empty;
                stmt.Bind("@FOLDER_TYPE", (int)FolderType.Last);

                while (stmt.Step() == SQLitePCL.SQLiteResult.ROW)
                {
                    FolderInfo fi = GetRowData(stmt);
                    string rootYn = stmt.GetText("ROOT_YN");

                    //먼저 마지막 폴더의 타입이 Last folder인 것을 삭제
                    using (var stmt2 = conn.Prepare(DML_DELETE))
                    {
                        stmt2.Bind("@PATH", fi.Path);
                        stmt2.Bind("@FOLDER_TYPE", (int)fi.Type);
                        result = stmt2.Step();
                    }
                    //타입 삭제 여부
                    if (result != SQLiteResult.DONE) return result;

                    //삭제하려는 폴더가 루트로 등록되어 있지 않는 폴더라면, FutrueAccessList를 검사하여 삭제 시킴
                    if (rootYn == "N" && !string.IsNullOrEmpty(fi.FalToken) && StorageApplicationPermissions.FutureAccessList.ContainsItem(fi.FalToken))
                    {
                        //FAL삭제
                        StorageApplicationPermissions.FutureAccessList.Remove(fi.FalToken);
                    }
                }
            }

            //새롭게 폴더 등록
            using (var stmt = conn.Prepare(DML_INSERT))
            {
                stmt.Bind("@PATH", folderInfo.Path);
                stmt.Bind("@FOLDER_TYPE", (int)FolderType.Last);
                stmt.Bind("@LEVEL", folderInfo.Level);
                stmt.Bind("@NAME", folderInfo.Name);
                stmt.Bind("@FAL_TOKEN", folderInfo.FalToken);
                stmt.Bind("@PASSCODE", folderInfo.Passcode);

                result = stmt.Step();
            }

            return result;
        }
    }
}
