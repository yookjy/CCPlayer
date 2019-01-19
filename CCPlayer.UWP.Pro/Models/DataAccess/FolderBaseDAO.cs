using CCPlayer.UWP.Extensions;
using SQLitePCL;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Storage.AccessCache;

namespace CCPlayer.UWP.Models.DataAccess
{
    abstract public class FolderBaseDAO : BaseDAO
    {
        public abstract string DDL_CREATE_TABLE { get; }

        public abstract string DML_INSERT { get; }

        public abstract string DML_SELECT { get; }

        public abstract string DML_DELETE { get; }

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
        }

        public FolderBaseDAO(SQLiteConnection conn) : base(conn) { }

        /// <summary>
        /// 폴더 정보를 DB에 저장한다.
        /// </summary>
        /// <param name="folderInfo"></param>
        /// <returns></returns>
        public SQLiteResult Insert(StorageItemInfo folderInfo)
        {
            SQLiteResult result = SQLiteResult.EMPTY;
            //폴더 등록
            using (var stmt = conn.Prepare(DML_INSERT))
            {
                stmt.Bind("@PATH", folderInfo.Path);
                stmt.Bind("@FOLDER_TYPE", (int)folderInfo.SubType);
                stmt.Bind("@NAME", folderInfo.Name);
                stmt.Bind("@ROOT_PATH", folderInfo.RootPath);
                stmt.Bind("@FAL_TOKEN", folderInfo.FalToken);

                result = stmt.Step();
            }

            return result;
        }

        /// <summary>
        /// 조회된 한 로우에 대한 데이터를 생성한다.
        /// </summary>
        /// <param name="stmt">DB 구문 객체</param>
        /// <returns>파일 정보</returns>
        private StorageItemInfo GetRowData(ISQLiteStatement stmt)
        {
            return new StorageItemInfo()
            {
                Path = stmt.GetText("PATH"),
                SubType = (SubType)stmt.GetInteger("FOLDER_TYPE"),
                Name = stmt.GetText("NAME"),
                RootPath = stmt.GetText("ROOT_PATH"),
                FalToken = stmt.GetText2("FAL_TOKEN")
            };
        }

        /// <summary>
        /// 탐색기 루트에 추가된 폴더 리스트를 조회한다.
        /// </summary>
        /// <param name="addedFolderList">조회된 결과를 담을 리스트</param>
        /// <param name="command">리스트의 버튼을 위한 커맨드</param>
        /// <returns>조회된 결과 리스트</returns>
        //public void LoadAddedFolderList(ICollection<FolderInfo> addedFolderList, ICommand command1, ICommand command2, bool includeProtectedFolder)
        public void LoadAddedFolderList(ICollection<StorageItemInfo> addedFolderList, bool nameAscending, Action<StorageItemInfo> action)
        {
            string sql = DML_SELECT.Replace("${COLUMN}", string.Empty);
            sql += " ORDER BY NAME " + (nameAscending ? "ASC" : "DESC");

            using (var stmt = conn.Prepare(sql))
            {
                stmt.Bind("@FOLDER_TYPE", (int)SubType.RootFolder);
                while (stmt.Step() == SQLitePCL.SQLiteResult.ROW)
                {
                    StorageItemInfo fi = GetRowData(stmt);

                    //이름이 중복되는 경우 경로를 표시
                    if (addedFolderList.Any(x => x.Name == fi.Name))
                    {
                        fi.Name = string.Format("{0} ({1})", fi.Name, Path.GetPathRoot(fi.Path));
                    }

                    addedFolderList.Add(fi);

                    if (action != null)
                    {
                        action.Invoke(fi);
                    }
                }
            }
        }
        
        /// <summary>
        /// 마지막 접근할 폴더를 조회한다.
        /// </summary>
        /// <returns>마지막 접근할 폴더 정보</returns>
        public StorageItemInfo GetLastFolder()
        {
            using (var stmt = conn.Prepare(DML_SELECT.Replace("${COLUMN}", string.Empty)))
            {
                stmt.Bind("@FOLDER_TYPE", (int)SubType.LastFolder);

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
        public SQLiteResult Delete(StorageItemInfo folderInfo)
        {
            SQLiteResult result = SQLiteResult.EMPTY;
            //폴더 삭제
            using (var stmt = conn.Prepare(DML_DELETE))
            {
                stmt.Bind("@PATH", folderInfo.Path);
                stmt.Bind("@FOLDER_TYPE", (int)folderInfo.SubType);
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
        public SQLiteResult ReplaceLastFolder(StorageItemInfo folderInfo)
        {
            SQLiteResult result = SQLiteResult.EMPTY;

            string coumn = @",CASE WHEN (SELECT COUNT(*) 
                                           FROM FOLDER T 
                                          WHERE T.PATH = PATH 
                                            AND T.FOLDER_TYPE = 1) > 0 THEN 'Y' 
                                   ELSE 'N' 
                              END AS IS_ADDED_FOLDER";

            //이전의 마지막 폴더 조회
            using (var stmt = conn.Prepare(DML_SELECT.Replace("${COLUMN}", coumn)))
            {
                string prevItemName = string.Empty;
                stmt.Bind("@FOLDER_TYPE", (int)SubType.LastFolder);

                while (stmt.Step() == SQLitePCL.SQLiteResult.ROW)
                {
                    StorageItemInfo fi = GetRowData(stmt);
                    bool isAddedFolder = stmt.GetText("IS_ADDED_FOLDER") == "Y";

                    //먼저 마지막 폴더의 타입이 Last folder인 것을 삭제
                    using (var stmt2 = conn.Prepare(DML_DELETE))
                    {
                        stmt2.Bind("@PATH", fi.Path);
                        stmt2.Bind("@FOLDER_TYPE", (int)fi.SubType);
                        result = stmt2.Step();
                    }
                    //타입 삭제 여부
                    if (result != SQLiteResult.DONE) return result;

                    //삭제하려는 폴더가 추가된 폴더로 등록되어 있지 않는 폴더라면, FutrueAccessList를 검사하여 삭제 시킴
                    if (!isAddedFolder && !string.IsNullOrEmpty(fi.FalToken) && StorageApplicationPermissions.FutureAccessList.ContainsItem(fi.FalToken))
                    {
                        //FAL삭제
                        StorageApplicationPermissions.FutureAccessList.Remove(fi.FalToken);
                    }
                }
            }

            if (folderInfo != null)
            {
                //새롭게 폴더 등록
                using (var stmt = conn.Prepare(DML_INSERT))
                {
                    stmt.Bind("@PATH", folderInfo.Path);
                    stmt.Bind("@FOLDER_TYPE", (int)SubType.LastFolder);
                    stmt.Bind("@NAME", folderInfo.Name);
                    stmt.Bind("@ROOT_PATH", folderInfo.RootPath);
                    stmt.Bind("@FAL_TOKEN", folderInfo.FalToken);

                    result = stmt.Step();
                }
            }

            return result;
        }
    }
}
