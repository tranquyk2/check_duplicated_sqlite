using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;

namespace Scanner
{
    public static class ScanDatabase
    {
        /// <summary>
        /// Lấy các bản ghi chưa gửi lên server
        /// </summary>
        public static List<ScanRecord> GetUnsentScans(int limit = 1000)
        {
            var records = new List<ScanRecord>();
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();
                var selectCmd = connection.CreateCommand();
                selectCmd.CommandText = @"
                    SELECT Id, STT, Barcode, NgayGio, KetQua, Ca, IsSent
                    FROM ScanRecords
                    WHERE IsSent = 0
                    ORDER BY Id ASC
                    LIMIT @limit
                ";
                selectCmd.Parameters.AddWithValue("@limit", limit);
                using var reader = selectCmd.ExecuteReader();
                while (reader.Read())
                {
                    records.Add(new ScanRecord
                    {
                        Id = reader.GetInt32(0),
                        STT = reader.GetInt32(1),
                        Barcode = reader.GetString(2),
                        NgayGio = reader.GetString(3),
                        KetQua = reader.GetString(4),
                        Ca = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                        IsSent = reader.IsDBNull(6) ? 0 : reader.GetInt32(6)
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetUnsentScans error: {ex.Message}");
            }
            return records;
        }

        /// <summary>
        /// Đánh dấu các bản ghi đã gửi lên server
        /// </summary>
        public static void MarkScansAsSent(List<int> ids)
        {
            if (ids == null || ids.Count == 0) return;
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();
                var updateCmd = connection.CreateCommand();
                updateCmd.CommandText = $@"
                    UPDATE ScanRecords SET IsSent = 1 WHERE Id IN ({string.Join(",", ids)})
                ";
                updateCmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MarkScansAsSent error: {ex.Message}");
            }
        }

        /// <summary>
        /// Đếm số lượng bản ghi chưa gửi
        /// </summary>
        public static int GetUnsentCount()
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();
                var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM ScanRecords WHERE IsSent = 0";
                var result = cmd.ExecuteScalar();
                return result != null ? Convert.ToInt32(result) : 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetUnsentCount error: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Đặt tất cả bản ghi là chưa gửi (để gửi lại toàn bộ dữ liệu)
        /// </summary>
        public static void ResetAllRecordsToUnsent()
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();
                var cmd = connection.CreateCommand();
                cmd.CommandText = "UPDATE ScanRecords SET IsSent = 0";
                cmd.ExecuteNonQuery();
                System.Diagnostics.Debug.WriteLine("Đã reset tất cả bản ghi về trạng thái chưa gửi.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ResetAllRecordsToUnsent error: {ex.Message}");
            }
        }

        /// <summary>
        /// Đảm bảo tất cả bản ghi cũ có giá trị IsSent = 0
        /// </summary>
        public static void EnsureOldRecordsMarkedAsUnsent()
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();
                var cmd = connection.CreateCommand();
                cmd.CommandText = "UPDATE ScanRecords SET IsSent = 0 WHERE IsSent IS NULL";
                var updated = cmd.ExecuteNonQuery();
                if (updated > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Đã đánh dấu {updated} bản ghi cũ là chưa gửi.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EnsureOldRecordsMarkedAsUnsent error: {ex.Message}");
            }
        }
        /// <summary>
        /// Gửi các bản ghi quét lên server qua HTTP POST (API server viết sau)
        /// </summary>
        /// <param name="serverUrl">Địa chỉ API server nhận dữ liệu</param>
        /// <param name="limit">Số lượng bản ghi gửi (mặc định 1000)</param>
        /// <returns>True nếu gửi thành công, False nếu lỗi</returns>
        public static async System.Threading.Tasks.Task<bool> SendScansToServerAsync(string serverUrl, int limit = 1000)
        {
            try
            {
                var records = GetUnsentScans(limit);
                if (records.Count == 0) return true; // Không có bản ghi nào để gửi
                var json = System.Text.Json.JsonSerializer.Serialize(records);
                using var client = new System.Net.Http.HttpClient();
                var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");
                var response = await client.PostAsync(serverUrl, content);
                if (response.IsSuccessStatusCode)
                {
                    // Đánh dấu các bản ghi đã gửi
                    MarkScansAsSent(records.ConvertAll(r => r.Id));
                }
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SendScansToServerAsync error: {ex.Message}");
                return false;
            }
        }

        private static readonly string DatabaseFolder;
        private static readonly string DatabasePath;
        private static readonly string ConnectionString;

        static ScanDatabase()
        {
            DatabaseFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Scanner");
            DatabasePath = Path.Combine(DatabaseFolder, "scans.db");
            ConnectionString = $"Data Source={DatabasePath}";

            InitializeDatabase();
            EnsureOldRecordsMarkedAsUnsent();
        }

        private static void InitializeDatabase()
        {
            try
            {
                if (!Directory.Exists(DatabaseFolder))
                {
                    Directory.CreateDirectory(DatabaseFolder);
                }

                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();

                var createTableCmd = connection.CreateCommand();
                createTableCmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS ScanRecords (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        STT INTEGER NOT NULL,
                        Barcode TEXT NOT NULL,
                        NgayGio TEXT NOT NULL,
                        KetQua TEXT NOT NULL,
                        Ca TEXT,
                        IsSent INTEGER DEFAULT 0
                    );
                    CREATE INDEX IF NOT EXISTS idx_barcode ON ScanRecords(Barcode);
                    CREATE INDEX IF NOT EXISTS idx_ngaygio ON ScanRecords(NgayGio);
                ";
                createTableCmd.ExecuteNonQuery();

                // Migration: Thêm trường IsSent vào database cũ nếu chưa có
                try
                {
                    var checkColumnCmd = connection.CreateCommand();
                    checkColumnCmd.CommandText = "PRAGMA table_info(ScanRecords)";
                    bool hasIsSentColumn = false;
                    
                    using (var reader = checkColumnCmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var columnName = reader.GetString(1);
                            if (columnName == "IsSent")
                            {
                                hasIsSentColumn = true;
                                break;
                            }
                        }
                    }

                    if (!hasIsSentColumn)
                    {
                        var alterTableCmd = connection.CreateCommand();
                        alterTableCmd.CommandText = "ALTER TABLE ScanRecords ADD COLUMN IsSent INTEGER DEFAULT 0";
                        alterTableCmd.ExecuteNonQuery();
                        System.Diagnostics.Debug.WriteLine("Đã thêm trường IsSent vào database.");
                    }
                }
                catch (Exception migrationEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Migration error: {migrationEx.Message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Database initialization error: {ex.Message}");
            }
        }

        public static void SaveScanRecord(int stt, string barcode, string ngayGio, string ketQua, string ca)
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();

                var insertCmd = connection.CreateCommand();
                insertCmd.CommandText = @"
                    INSERT INTO ScanRecords (STT, Barcode, NgayGio, KetQua, Ca)
                    VALUES (@stt, @barcode, @ngaygio, @ketqua, @ca)
                ";
                insertCmd.Parameters.AddWithValue("@stt", stt);
                insertCmd.Parameters.AddWithValue("@barcode", barcode ?? string.Empty);
                insertCmd.Parameters.AddWithValue("@ngaygio", ngayGio ?? string.Empty);
                insertCmd.Parameters.AddWithValue("@ketqua", ketQua ?? string.Empty);
                insertCmd.Parameters.AddWithValue("@ca", ca ?? string.Empty);

                insertCmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Database save error: {ex.Message}");
            }
        }

        public static List<ScanRecord> GetRecentScans(int limit = 1000)
        {
            var records = new List<ScanRecord>();

            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();

                var selectCmd = connection.CreateCommand();
                selectCmd.CommandText = @"
                    SELECT Id, STT, Barcode, NgayGio, KetQua, Ca
                    FROM ScanRecords
                    ORDER BY Id DESC
                    LIMIT @limit
                ";
                selectCmd.Parameters.AddWithValue("@limit", limit);

                using var reader = selectCmd.ExecuteReader();
                while (reader.Read())
                {
                    records.Add(new ScanRecord
                    {
                        Id = reader.GetInt32(0),
                        STT = reader.GetInt32(1),
                        Barcode = reader.GetString(2),
                        NgayGio = reader.GetString(3),
                        KetQua = reader.GetString(4),
                        Ca = reader.IsDBNull(5) ? string.Empty : reader.GetString(5)
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Database read error: {ex.Message}");
            }

            return records;
        }

        public static List<ScanRecord> GetScansByDateRange(DateTime fromDate, DateTime toDate, int limit = 100000)
        {
            var records = new List<ScanRecord>();

            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();

                // Tạo pattern tìm kiếm cho định dạng dd/MM/yyyy
                var patterns = new List<string>();
                for (var date = fromDate.Date; date <= toDate.Date; date = date.AddDays(1))
                {
                    patterns.Add($"{date:dd/MM/yyyy}%");
                }

                var selectCmd = connection.CreateCommand();
                // Tạo query với multiple LIKE conditions
                var likeConditions = string.Join(" OR ", patterns.Select((_, i) => $"NgayGio LIKE @pattern{i}"));
                
                selectCmd.CommandText = $@"
                    SELECT Id, STT, Barcode, NgayGio, KetQua, Ca
                    FROM ScanRecords
                    WHERE {likeConditions}
                    ORDER BY Id DESC
                    LIMIT @limit
                ";
                
                for (int i = 0; i < patterns.Count; i++)
                {
                    selectCmd.Parameters.AddWithValue($"@pattern{i}", patterns[i]);
                }
                selectCmd.Parameters.AddWithValue("@limit", limit);

                using var reader = selectCmd.ExecuteReader();
                while (reader.Read())
                {
                    records.Add(new ScanRecord
                    {
                        Id = reader.GetInt32(0),
                        STT = reader.GetInt32(1),
                        Barcode = reader.GetString(2),
                        NgayGio = reader.GetString(3),
                        KetQua = reader.GetString(4),
                        Ca = reader.IsDBNull(5) ? string.Empty : reader.GetString(5)
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Database query error: {ex.Message}");
            }

            return records;
        }

        public static int GetTotalRecordCount()
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();

                var countCmd = connection.CreateCommand();
                countCmd.CommandText = "SELECT COUNT(*) FROM ScanRecords";
                var result = countCmd.ExecuteScalar();
                return result != null ? Convert.ToInt32(result) : 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Database count error: {ex.Message}");
                return 0;
            }
        }

        public static void DeleteOldRecords(int daysToKeep = 90)
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();

                var deleteCmd = connection.CreateCommand();
                deleteCmd.CommandText = @"
                    DELETE FROM ScanRecords 
                    WHERE ScanTime < datetime('now', '-' || @days || ' days')
                ";
                deleteCmd.Parameters.AddWithValue("@days", daysToKeep);
                deleteCmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Database delete error: {ex.Message}");
            }
        }

        public static bool DeleteRecordByBarcode(string barcode, string ngayGio, string ketQua)
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();

                var deleteCmd = connection.CreateCommand();
                deleteCmd.CommandText = @"
                    DELETE FROM ScanRecords 
                    WHERE Barcode = @barcode 
                    AND NgayGio = @ngaygio 
                    AND KetQua = @ketqua
                    AND Id = (
                        SELECT Id FROM ScanRecords 
                        WHERE Barcode = @barcode 
                        AND NgayGio = @ngaygio 
                        AND KetQua = @ketqua
                        ORDER BY ScanTime DESC LIMIT 1
                    )
                ";
                deleteCmd.Parameters.AddWithValue("@barcode", barcode ?? string.Empty);
                deleteCmd.Parameters.AddWithValue("@ngaygio", ngayGio ?? string.Empty);
                deleteCmd.Parameters.AddWithValue("@ketqua", ketQua ?? string.Empty);
                
                var rowsAffected = deleteCmd.ExecuteNonQuery();
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Database delete error: {ex.Message}");
                return false;
            }
        }

        public static List<ScanRecord> SearchByBarcode(string searchText, int limit = 1000)
        {
            var records = new List<ScanRecord>();

            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();

                var selectCmd = connection.CreateCommand();
                selectCmd.CommandText = @"
                    SELECT Id, STT, Barcode, NgayGio, KetQua, Ca
                    FROM ScanRecords
                    WHERE Barcode LIKE @searchText
                    ORDER BY NgayGio DESC
                    LIMIT @limit
                ";
                selectCmd.Parameters.AddWithValue("@searchText", $"%{searchText}%");
                selectCmd.Parameters.AddWithValue("@limit", limit);

                using var reader = selectCmd.ExecuteReader();
                while (reader.Read())
                {
                    records.Add(new ScanRecord
                    {
                        Id = reader.GetInt32(0),
                        STT = reader.GetInt32(1),
                        Barcode = reader.GetString(2),
                        NgayGio = reader.GetString(3),
                        KetQua = reader.GetString(4),
                        Ca = reader.IsDBNull(5) ? string.Empty : reader.GetString(5)
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Database search error: {ex.Message}");
            }

            return records;
        }

        public static List<ScanRecord> GetRecordsByMonth(int year, int month, int limit = 1000000)
        {
            var records = new List<ScanRecord>();

            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();

                var selectCmd = connection.CreateCommand();
                // Lọc theo định dạng dd/MM/yyyy (VN format)
                // Pattern: __/01/2026 cho tháng 1 năm 2026
                selectCmd.CommandText = @"
                    SELECT Id, STT, Barcode, NgayGio, KetQua, Ca
                    FROM ScanRecords
                    WHERE NgayGio LIKE @pattern
                    ORDER BY Id DESC
                    LIMIT @limit
                ";
                var pattern = $"%/{month:00}/{year}%";
                selectCmd.Parameters.AddWithValue("@pattern", pattern);
                selectCmd.Parameters.AddWithValue("@limit", limit);

                using var reader = selectCmd.ExecuteReader();
                while (reader.Read())
                {
                    records.Add(new ScanRecord
                    {
                        Id = reader.GetInt32(0),
                        STT = reader.GetInt32(1),
                        Barcode = reader.GetString(2),
                        NgayGio = reader.GetString(3),
                        KetQua = reader.GetString(4),
                        Ca = reader.IsDBNull(5) ? string.Empty : reader.GetString(5)
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Database query error: {ex.Message}");
            }

            return records;
        }
    }

    public class ScanRecord
    {
        public int Id { get; set; }
        public int STT { get; set; }
        public string Barcode { get; set; } = string.Empty;
        public string NgayGio { get; set; } = string.Empty;
        public string KetQua { get; set; } = string.Empty;
        public string Ca { get; set; } = string.Empty;
        public int IsSent { get; set; } // 0: chưa gửi, 1: đã gửi
    }
}
