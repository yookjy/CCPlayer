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
    public class FolderDAO : FolderBaseDAO
    {
        #region SQL 구문                
        public override string DDL_CREATE_TABLE
        {
            get
            {
                return @"CREATE TABLE IF NOT EXISTS
                            FOLDER ( 
                                PATH                    VARCHAR(255) NOT NULL
                               ,FOLDER_TYPE             INTEGER
                               ,NAME                    VARCHAR(255) NOT NULL
                               ,ROOT_PATH               VARCHAR(255) NOT NULL
                               ,FAL_TOKEN               VARCHAR(255) 
                               ,PRIMARY KEY (PATH, FOLDER_TYPE)
                            )";
            }
        }

        public override string DML_INSERT
        {
            get
            {
                return @"INSERT INTO
                            FOLDER (
                                PATH
                               ,FOLDER_TYPE
                               ,NAME
                               ,ROOT_PATH
                               ,FAL_TOKEN
                            )
                            VALUES (
                                @PATH
                               ,@FOLDER_TYPE
                               ,@NAME
                               ,@ROOT_PATH
                               ,@FAL_TOKEN
                            )";
            }
        }

        public string DML_UPDATE
        {
            get
            {
                return @"UPDATE FOLDER SET FAL_TOKEN = @FAL_TOKEN 
                            WHERE PATH = @PATH AND FOLDER_TYPE = @FOLDER_TYPE";
            }
        }

        public override string DML_SELECT
        {
            get
            {
                return @"SELECT PATH
                               ,FOLDER_TYPE
                               ,NAME
                               ,ROOT_PATH
                               ,FAL_TOKEN
                               ${COLUMN}
                          FROM FOLDER
                         WHERE FOLDER_TYPE = @FOLDER_TYPE";
            }
        }

        public override string DML_DELETE
        {
            get
            {
                return @"DELETE FROM FOLDER WHERE PATH = @PATH AND FOLDER_TYPE = @FOLDER_TYPE";
            }
        }

        #endregion

        public FolderDAO(SQLiteConnection conn) : base(conn) { }

        public SQLiteResult Update(StorageItemInfo folderInfo)
        {
            SQLiteResult result = SQLiteResult.EMPTY;
            //폴더 등록
            using (var stmt = conn.Prepare(DML_UPDATE))
            {
                stmt.Bind("@FAL_TOKEN", folderInfo.FalToken);
                stmt.Bind("@PATH", folderInfo.Path);
                stmt.Bind("@FOLDER_TYPE", (int)folderInfo.SubType);

                result = stmt.Step();
            }

            return result;
        }
    }
}
