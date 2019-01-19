using SQLitePCL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CCPlayer.WP81.Models.DataAccess
{
    public abstract class BaseDAO
    {
        public const string TCL_BEGIN = "BEGIN TRANSACTION";

        public const string TCL_COMMIT = "COMMIT TRANSACTION";

        public const string TCL_ROLLBACK = "ROLLBACK TRANSACTION";

        protected SQLitePCL.SQLiteConnection conn;

        public BaseDAO(SQLitePCL.SQLiteConnection conn)
        {
            this.conn = conn;
            this.CheckCreateTable();
            this.CheckAlterTable();
        }

        protected abstract void CheckCreateTable();

        protected abstract void CheckAlterTable();

        /// <summary>
        /// 하나의 트랜젝션으로 묶어 처리를 수행한다.
        /// </summary>
        /// <param name="func">처리할 함수</param>
        /// <returns>DB수행 결과</returns>
        public SQLiteResult Transactional(Func<SQLiteResult> func)
        {
            var result = SQLitePCL.SQLiteResult.EMPTY;
            try
            {
                using (var stmt = conn.Prepare(TCL_BEGIN)) { stmt.Step(); }
                //결과가 Done이 아니면 롤백 처리
                if (func.Invoke() != SQLiteResult.DONE) throw new Exception();
                //커밋
                using (var stmt = conn.Prepare(TCL_COMMIT)) { result = stmt.Step(); }
            }
            catch (Exception)
            {
                using (var stmt = conn.Prepare(TCL_ROLLBACK)) { stmt.Step(); }
            }

            return result;
        }

    }
}
