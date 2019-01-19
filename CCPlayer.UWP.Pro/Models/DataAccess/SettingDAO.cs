using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lime.Encoding;
using Windows.System.Threading;
using Windows.UI;
using Windows.UI.Xaml.Media;

namespace CCPlayer.UWP.Models.DataAccess
{
    public class SettingDAO : BaseDAO
    {
        private const string DDL_SETTINGS_CREATE_TABLE = @"CREATE TABLE IF NOT EXISTS
                                                              SETTING (
                                                                  CODE     VARCHAR(25) PRIMARY KEY NOT NULL, 
                                                                  VALUE    VARCHAR(100) NOT NULL,
                                                                  TYPE     VARCHAR(1)   NOT NULL,
                                                                  ATTR1    VARCHAR(100),
                                                                  ATTR2    VARCHAR(100)
                                                              )";

        private const string DML_SETTINGS_CREATE = @"INSERT INTO SETTING ( CODE,  VALUE,  TYPE,  ATTR1,  ATTR2 ) 
                                                                  VALUES (@CODE, @VALUE, @TYPE, @ATTR1, @ATTR2)";

        private const string DML_SETTINGS_REPLACE = @"INSERT OR REPLACE INTO SETTING ( CODE,  VALUE,  TYPE,  ATTR1,  ATTR2 ) 
                                                                              VALUES (@CODE, @VALUE, @TYPE, @ATTR1, @ATTR2)";

        private const string DML_SETTINGS_READ = @"SELECT CODE, VALUE, TYPE, ATTR1, ATTR2 FROM SETTING";

        private const string DML_SETTINGS_UPDATE = @"UPDATE SETTING 
                                                       SET VALUE = @VALUE
                                                          ,TYPE  = @TYPE
                                                          ,ATTR1 = @ATTR1
                                                          ,ATTR2 = @ATTR2
                                                     WHERE CODE  = @CODE";

        private const string DML_SETTINGS_DESTROY = @"DELETE FROM SETTING 
                                                       WHERE CODE  = @CODE";


        public SettingDAO(SQLitePCL.SQLiteConnection conn) : base(conn)
        {
            SettingCache = this.SelectAll();
        }

        protected override void CheckCreateTable()
        {
            using (var stmt = conn.Prepare(DDL_SETTINGS_CREATE_TABLE))
            {
                stmt.Step();
            }
        }

        protected override void CheckAlterTable()
        {
        }

        public Settings SettingCache { get; set; }

        private Settings SelectAll()
        {
            if (SettingCache == null)
            {
                SettingCache = new Settings();
            }

            using (var statement = this.conn.Prepare(DML_SETTINGS_READ))
            {
                while (statement.Step() == SQLitePCL.SQLiteResult.ROW)
                {
                    SettingCache.Add(new Setting()
                    {
                        Code = statement.GetText("CODE"),
                        Value = statement["VALUE"],
                        Type = statement.GetText("TYPE"),
                        Attr1 = statement["ATTR1"] != null ? statement.GetText("ATTR1") : null,
                        Attr2 = statement["ATTR2"] != null ? statement.GetText("ATTR2") : null,
                    });
                }
            }

            var general = new Settings.GeneralSetting(SettingCache);
            var playback = new Settings.PlaybackSetting(SettingCache);
            var subtitle = new Settings.ClosedCaptionSetting(SettingCache);
            var privacy = new Settings.PrivacySetting(SettingCache);
            var server = new Settings.ServerSetting(SettingCache);
            var thumbnail = new Settings.ThumbnailSetting(SettingCache);

            if (SettingCache.Count == 0)
            {
                //최초 상태이면 fal 초기화
                Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList.Clear();

                //디폴트 값 등록
                //일반
                general.ResetDefaultValue();
                //재생
                playback.ResetDefaultValue();
                //자막
                subtitle.ResetDefaultvalue();
                //개인설정
                privacy.ResetDefaultValue();
                //서버
                server.ResetDefaultValue();
                //썸네일
                thumbnail.ResetDefaultValue();

                try
                {
                    using (var stmt = this.conn.Prepare(TCL_BEGIN)) { stmt.Step(); }
                    using (var custstmt = this.conn.Prepare(DML_SETTINGS_CREATE))
                    {
                        foreach (var setting in SettingCache)
                        {
                            //if (setting.Code == "ForegroundColor")
                            //    System.Diagnostics.Debugger.Break();
                            //System.Diagnostics.Debug.WriteLine(setting.Code + " " + setting.Value + " " + setting.Type + " " + setting.Attr1 + " " + setting.Attr2);
                            custstmt.Bind("@CODE", setting.Code);
                            custstmt.Bind("@VALUE", setting.Value);
                            custstmt.Bind("@TYPE", setting.Type);
                            custstmt.Bind("@ATTR1", setting.Attr1);
                            custstmt.Bind("@ATTR2", setting.Attr2);

                            var result = custstmt.Step();
                            if (result != SQLitePCL.SQLiteResult.DONE)
                            {
                                throw new Exception();
                            }

                            // Resets the statement, to that it can be used again (with different parameters).
                            custstmt.Reset();
                            custstmt.ClearBindings();
                        }
                    }
                    using (var stmt = this.conn.Prepare(TCL_COMMIT)) { stmt.Step(); }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.Write("Settings 데이터의 DB 저장이 실패함!!!! : " + ex.Message);
                    using (var stmt = this.conn.Prepare(TCL_ROLLBACK)) { stmt.Step(); }
                }
            }

            return SettingCache;
        }

        public void Replace(Settings settings)
        {
            using (var custstmt = this.conn.Prepare(DML_SETTINGS_REPLACE))
            {
                foreach (var setting in settings)
                {
                    if (setting.IsUpdated)
                    {
                        custstmt.Bind("@CODE", setting.Code);
                        custstmt.Bind("@VALUE", setting.Value);
                        custstmt.Bind("@TYPE", setting.Type);
                        custstmt.Bind("@ATTR1", setting.Attr1);
                        custstmt.Bind("@ATTR2", setting.Attr2);

                        var result = custstmt.Step();
                        //상태 변경
                        setting.IsUpdated = result != SQLitePCL.SQLiteResult.DONE;

                        // Resets the statement, to that it can be used again (with different parameters).
                        custstmt.Reset();
                        custstmt.ClearBindings();
                    }
                }
            }
        }
    }
}
