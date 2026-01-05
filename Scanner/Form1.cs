using HidSharp;
using ClosedXML.Excel;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace Scanner
{
    public partial class Form1 : Form
    {
        // Cấu hình URL server (thay đổi địa chỉ này theo server thực tế)
        private const string SERVER_URL = "http://localhost:8000/api/scans";
        private const int SYNC_INTERVAL_MINUTES = 1; // Gửi dữ liệu mỗi 5 phút
        private const int BATCH_SIZE = 1000; // Số bản ghi gửi mỗi lần
        
        private System.Windows.Forms.Timer? syncTimer;
        private bool isSyncing = false;

        public Form1()
        {
            InitializeComponent();
            txtBarcode.KeyDown += TxtBarcode_KeyDown;
            btnAdd.Click += BtnAdd_Click;
            btnDelete.Click += BtnDelete_Click;
            btnLoadDB.Click += BtnLoadDB_Click;
            btnSearch.Click += BtnSearch_Click;
            btnExportMonth.Click += BtnExportMonth_Click;
            
            // Khởi tạo timer tự động gửi dữ liệu
            InitializeSyncTimer();
        }

        private void InitializeSyncTimer()
        {
            syncTimer = new System.Windows.Forms.Timer();
            syncTimer.Interval = SYNC_INTERVAL_MINUTES * 60 * 1000;
            syncTimer.Tick += async (s, e) => await SyncDataToServer();
            syncTimer.Start();
            
            // Gửi ngay lần đầu sau 5 giây
            Task.Delay(5000).ContinueWith(async _ => await SyncDataToServer());
        }

        private async Task SyncDataToServer()
        {
            if (isSyncing) return;
            
            isSyncing = true;
            try
            {
                if (!IsNetworkAvailable())
                {
                    return;
                }

                // Gửi dữ liệu theo batch cho đến khi hết
                bool hasMoreData = true;
                
                while (hasMoreData)
                {
                    var result = await ScanDatabase.SendScansToServerAsync(SERVER_URL, BATCH_SIZE);
                    
                    if (result)
                    {
                        var unsentCount = ScanDatabase.GetUnsentCount();
                        hasMoreData = unsentCount > 0;
                        
                        if (hasMoreData)
                        {
                            await Task.Delay(1000);
                        }
                    }
                    else
                    {
                        hasMoreData = false;
                    }
                }
            }
            catch
            {
                // Bỏ qua lỗi, sẽ thử lại ở lần gửi tiếp theo
            }
            finally
            {
                isSyncing = false;
            }
        }

        private bool IsNetworkAvailable()
        {
            try
            {
                return NetworkInterface.GetIsNetworkAvailable();
            }
            catch
            {
                return false;
            }
        }

        private void BtnSearch_Click(object? sender, EventArgs e)
        {
            var searchText = txtSearch.Text.Trim();
            if (string.IsNullOrEmpty(searchText))
            {
                MessageBox.Show("Vui lòng nhập barcode cần tìm.", "Thiếu dữ liệu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtSearch.Focus();
                return;
            }

            try
            {
                
                dataGridView1.Rows.Clear();

                // Tìm kiếm trong database
                var records = ScanDatabase.SearchByBarcode(searchText, 1000);

                if (records.Count == 0)
                {
                    MessageBox.Show($"Không tìm thấy barcode chứa '{searchText}'.", "Không có kết quả", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                
                foreach (var record in records)
                {
                    dataGridView1.Rows.Add(record.STT.ToString(), record.Barcode, record.NgayGio, record.KetQua, record.Ca);
                }

                // Cập nhật số lượng
                txtSTTscan.Text = dataGridView1.Rows.Count.ToString();

                MessageBox.Show($"Tìm thấy {records.Count} kết quả.", "Tìm kiếm thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tìm kiếm: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnExportMonth_Click(object? sender, EventArgs e)
        {
            try
            {
                var selectedDate = dtpMonth.Value;
                var year = selectedDate.Year;
                var month = selectedDate.Month;

                // Lấy dữ liệu từ database
                var records = ScanDatabase.GetRecordsByMonth(year, month);

                if (records.Count == 0)
                {
                    MessageBox.Show($"Không có dữ liệu trong tháng {month:00}/{year}.", "Không có dữ liệu", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Chọn nơi lưu file
                using var sfd = new SaveFileDialog();
                sfd.Filter = "Excel Workbook (*.xlsx)|*.xlsx";
                sfd.FileName = $"ScanData_{year}_{month:00}.xlsx";
                if (sfd.ShowDialog() != DialogResult.OK) return;

                var file = sfd.FileName;

                // Xuất ra Excel
                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add($"Thang {month:00}-{year}");

                // Headers
                ws.Cell(1, 1).Value = "STT";
                ws.Cell(1, 2).Value = "Barcode";
                ws.Cell(1, 3).Value = "Ngày giờ";
                ws.Cell(1, 4).Value = "Kết quả";
                ws.Cell(1, 5).Value = "Ca";

                // Style headers
                var headerRange = ws.Range(1, 1, 1, 5);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
                headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Data rows
                int row = 2;
                foreach (var record in records)
                {
                    ws.Cell(row, 1).Value = record.STT;
                    ws.Cell(row, 2).Value = record.Barcode;
                    ws.Cell(row, 3).Value = record.NgayGio;
                    ws.Cell(row, 4).Value = record.KetQua;
                    ws.Cell(row, 5).Value = record.Ca;
                    row++;
                }

                // Adjust columns
                ws.Columns().AdjustToContents();

                wb.SaveAs(file);

                MessageBox.Show($"Đã xuất {records.Count} records của tháng {month:00}/{year} thành công!", "Xuất thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi xuất file: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnLoadDB_Click(object? sender, EventArgs e)
        {
            try
            {
                
                dataGridView1.Rows.Clear();

                
                var records = ScanDatabase.GetRecentScans(10000);
                
                
                foreach (var record in records)
                {
                    dataGridView1.Rows.Add(record.STT.ToString(), record.Barcode, record.NgayGio, record.KetQua, record.Ca);
                }

                
                txtSTTscan.Text = dataGridView1.Rows.Count.ToString();

                var totalRecords = ScanDatabase.GetTotalRecordCount();
                MessageBox.Show($"Đã load {records.Count} record gần nhất.\nTổng số record trong database: {totalRecords:N0}", 
                    "Load thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi load dữ liệu: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnExcel_Click(object? sender, EventArgs e)
        {
            using var sfd = new SaveFileDialog();
            sfd.Filter = "Excel Workbook (*.xlsx)|*.xlsx|CSV files (*.csv)|*.csv";
            sfd.FileName = $"scans_{DateTime.Now:yyyy_MM}.xlsx";
            if (sfd.ShowDialog() != DialogResult.OK) return;

            var file = sfd.FileName;
            try
            {
                if (Path.GetExtension(file).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    using var wb = new XLWorkbook();
                    var ws = wb.Worksheets.Add("Scans");

                    
                    int col = 1;
                    foreach (DataGridViewColumn c in dataGridView1.Columns)
                    {
                        ws.Cell(1, col).Value = c.HeaderText;
                        ws.Cell(1, col).Style.Font.Bold = true;
                        col++;
                    }

                    
                    int row = 2;
                    foreach (DataGridViewRow r in dataGridView1.Rows)
                    {
                        for (int cidx = 0; cidx < dataGridView1.Columns.Count; cidx++)
                        {
                            var v = r.Cells[cidx].Value;
                            ws.Cell(row, cidx + 1).Value = v?.ToString() ?? string.Empty;
                        }
                        row++;
                    }

                    
                    ws.Columns().AdjustToContents();

                    wb.SaveAs(file);
                }
                else
                {
                    
                    var sb = new System.Text.StringBuilder();
                    var headers = new List<string>();
                    foreach (DataGridViewColumn col in dataGridView1.Columns)
                    {
                        headers.Add(EscapeCsv(col.HeaderText));
                    }
                    sb.AppendLine(string.Join(",", headers));
                    foreach (DataGridViewRow row in dataGridView1.Rows)
                    {
                        var cells = new List<string>();
                        for (int i = 0; i < dataGridView1.Columns.Count; i++)
                        {
                            var val = row.Cells[i].Value;
                            cells.Add(EscapeCsv(val?.ToString() ?? string.Empty));
                        }
                        sb.AppendLine(string.Join(",", cells));
                    }
                    var bytes = System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
                    File.WriteAllBytes(file, bytes);
                }

                MessageBox.Show("Xuất file thành công.", "OK", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi xuất file: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string EscapeCsv(string s)
        {
            if (s == null) return string.Empty;
            
            if (s.Contains('"')) s = s.Replace("\"", "\"\"");
            if (s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r'))
            {
                return '"' + s + '"';
            }
            return s;
        }

        private void BtnDelete_Click(object? sender, EventArgs e)
        {
            
            if (dataGridView1.SelectedRows.Count == 0) return;
            
            var deletedCount = 0;
            foreach (DataGridViewRow r in dataGridView1.SelectedRows)
            {
                
                var barcode = r.Cells[1].Value?.ToString() ?? string.Empty;
                var ngayGio = r.Cells[2].Value?.ToString() ?? string.Empty;
                var ketQua = r.Cells[3].Value?.ToString() ?? string.Empty;
                
                
                if (ScanDatabase.DeleteRecordByBarcode(barcode, ngayGio, ketQua))
                {
                    deletedCount++;
                }
                
                
                dataGridView1.Rows.Remove(r);
            }

            
            txtSTTscan.Text = dataGridView1.Rows.Count.ToString();
            
            if (deletedCount > 0)
            {
                
                var totalRecords = ScanDatabase.GetTotalRecordCount();
                this.Text = $"Scanner - Tổng số record trong database: {totalRecords:N0}";
            }
        }

        private void BtnAdd_Click(object? sender, EventArgs e)
        {
            
            using var f = new Form2();
            f.ShowDialog();
            
        }

        private void TxtBarcode_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;

                
                if (!int.TryParse(txtStartscan.Text, out var start))
                {
                    MessageBox.Show("Vui lòng nhập STT Start Scan (số nguyên).", "Thiếu STT Start", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtStartscan.Focus();
                    return;
                }

                
                if (cbCasx.SelectedItem == null || string.IsNullOrWhiteSpace(cbCasx.SelectedItem.ToString()))
                {
                    MessageBox.Show("Vui lòng chọn Ca sản xuất trước khi quét.", "Thiếu Ca sản xuất", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    cbCasx.Focus();
                    return;
                }

                ProcessScannedBarcode(txtBarcode.Text);
            }
        }

        private void ProcessScannedBarcode(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode)) return;
            barcode = barcode.Trim();

            
            int start = 1;
            if (!string.IsNullOrWhiteSpace(txtStartscan.Text) && int.TryParse(txtStartscan.Text, out var s)) start = s;

            
            int stt = start + dataGridView1.Rows.Count;

            
            var lookup = barcode.ToUpperInvariant();

            
            DataGridViewRow? existingRow = null;
            foreach (DataGridViewRow r in dataGridView1.Rows)
            {
                if (r.Cells.Count > 1 && r.Cells[1].Value is string v)
                {
                    if (v.Trim().ToUpperInvariant() == lookup)
                    {
                        existingRow = r;
                        break;
                    }
                }
            }

            string resultText;
            Color backColor;

            
            if (ModelStore.TryMatchModel(barcode, out var model))
            {
                
                if (ModelStore.IsBarcodeScanned(barcode) || existingRow != null)
                {
                    resultText = "Trùng barcode";
                    backColor = Color.Gold;
                    

                    
                    _ = Patlite.AlertDuplicateAsync();
                }
                else
                {
                    
                    resultText = "OK";
                    backColor = Color.LimeGreen;
                    ModelStore.MarkScanned(barcode);
                }
            }
            else
            {
                resultText = "Sai model";
                backColor = Color.Red;
                
            }

            
            btnStatus.Text = resultText;
            btnStatus.BackColor = backColor;
            btnStatus.ForeColor = Color.White;

            
            if (existingRow != null)
            {
                dataGridView1.ClearSelection();
                existingRow.Selected = true;
                try { dataGridView1.FirstDisplayedScrollingRowIndex = existingRow.Index; } catch { }
            }

            
            var ngay = DateTime.Now.ToString("dd/MM/yyyy h:mm tt", new System.Globalization.CultureInfo("vi-VN"));
            var ca = cbCasx.SelectedItem?.ToString() ?? string.Empty;

            
            dataGridView1.Rows.Insert(0, stt.ToString(), barcode, ngay, resultText, ca);

            
            ScanDatabase.SaveScanRecord(stt, barcode, ngay, resultText, ca);

            
            txtSTTscan.Text = dataGridView1.Rows.Count.ToString();

            
            if (dataGridView1.Rows.Count > 0)
            {
                dataGridView1.ClearSelection();
                dataGridView1.Rows[0].Selected = true;
                try { dataGridView1.FirstDisplayedScrollingRowIndex = 0; } catch { }
            }

            
            txtBarcode.Clear();
            txtBarcode.Focus();
        }

        private void groupBox1_Enter(object sender, EventArgs e)
        {

        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {

        }

        private void label4_Click(object sender, EventArgs e)
        {

        }

        private void label3_Click(object sender, EventArgs e)
        {

        }

        private void Form1_Load(object sender, EventArgs e)
        {
            
            try
            {
                var records = ScanDatabase.GetRecentScans(10000);
                
                foreach (var record in records)
                {
                    dataGridView1.Rows.Add(record.STT.ToString(), record.Barcode, record.NgayGio, record.KetQua, record.Ca);
                }
                
                
                txtSTTscan.Text = dataGridView1.Rows.Count.ToString();

                
                var totalRecords = ScanDatabase.GetTotalRecordCount();
                this.Text = $"Scanner - Tổng số bản ghi: {totalRecords:N0}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi load dữ liệu: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {

        }

        private void label1_Click_1(object sender, EventArgs e)
        {

        }
    }
}
