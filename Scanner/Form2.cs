using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Scanner
{
    public partial class Form2 : Form
    {
        public Form2()
        {
            InitializeComponent();
            // wire up events
            button1.Click += Button1_Click;
            listView1.DoubleClick += ListView1_DoubleClick;
            listView1.SelectedIndexChanged += listView1_SelectedIndexChanged;
            button2.Click += Button2_Click; // EDIT
            button3.Click += Button3_Click; // DELETE
        }

        private void Form2_Load(object sender, EventArgs e)
        {
            ResizeColumnsEvenly();
            RefreshListView();
        }

        private void RefreshListView()
        {
            listView1.Items.Clear();
            foreach (var m in ModelStore.Models)
            {
                var it = new ListViewItem(m.Name ?? string.Empty);
                it.SubItems.Add(m.Barcode ?? string.Empty);
                listView1.Items.Add(it);
            }
        }

        private void Form2_Resize(object? sender, EventArgs e)
        {
            ResizeColumnsEvenly();
        }

        private void ResizeColumnsEvenly()
        {
            if (listView1 == null || listView1.ClientSize.Width <= 0) return;
            int half = listView1.ClientSize.Width / 2;
            listView1.Columns[0].Width = half;
            listView1.Columns[1].Width = listView1.ClientSize.Width - half;
        }

        private void Button1_Click(object? sender, EventArgs e)
        {
            string name = txtNamemodel?.Text?.Trim() ?? string.Empty;
            string barcode = txtBarcodemodel?.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(barcode))
            {
                MessageBox.Show("Vui lòng nhập Tên Model hoặc Barcode Model.", "Thiếu dữ liệu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ModelStore.AddModel(name, barcode);
            RefreshListView();

            txtNamemodel.Clear();
            txtBarcodemodel.Clear();
            txtNamemodel.Focus();
        }

        private void ListView1_DoubleClick(object? sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count == 0) return;
            var selected = listView1.SelectedItems[0];
            var modelIdentifier = !string.IsNullOrEmpty(selected.SubItems[1].Text) ? selected.SubItems[1].Text : selected.Text;
            var result = MessageBox.Show($"Xóa dòng: '{modelIdentifier}'?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result == DialogResult.Yes)
            {
                if (ModelStore.RemoveModel(modelIdentifier))
                {
                    RefreshListView();
                }
                else
                {
                    MessageBox.Show("Xóa thất bại.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count == 0) return;
            var it = listView1.SelectedItems[0];
            txtNamemodel.Text = it.Text;
            txtBarcodemodel.Text = it.SubItems.Count > 1 ? it.SubItems[1].Text : string.Empty;
        }

        private void Button2_Click(object? sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn model để sửa.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selected = listView1.SelectedItems[0];
            string oldModelIdentifier = !string.IsNullOrEmpty(selected.SubItems[1].Text) ? selected.SubItems[1].Text : selected.Text;

            // Use selected item's values as defaults if textbox fields are empty
            string currentName = selected.Text;
            string currentBarcode = selected.SubItems.Count > 1 ? selected.SubItems[1].Text : string.Empty;

            string newNameInput = txtNamemodel?.Text?.Trim() ?? string.Empty;
            string newBarcodeInput = txtBarcodemodel?.Text?.Trim() ?? string.Empty;

            string newName = string.IsNullOrEmpty(newNameInput) ? currentName : newNameInput;
            string newBarcode = string.IsNullOrEmpty(newBarcodeInput) ? currentBarcode : newBarcodeInput;

            if (string.IsNullOrEmpty(newName) && string.IsNullOrEmpty(newBarcode))
            {
                MessageBox.Show("Vui lòng nhập Tên Model hoặc Barcode Model.", "Thiếu dữ liệu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (ModelStore.UpdateModel(oldModelIdentifier, newName, newBarcode))
            {
                MessageBox.Show("Cập nhật thành công.", "OK", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Cập nhật thất bại (có thể trùng hoặc không tìm thấy).", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            RefreshListView();
        }

        private void Button3_Click(object? sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn model để xóa.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selected = listView1.SelectedItems[0];
            string model = selected.SubItems.Count > 1 && !string.IsNullOrEmpty(selected.SubItems[1].Text) ? selected.SubItems[1].Text : selected.Text;
            var result = MessageBox.Show($"Xóa model: '{model}'?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result == DialogResult.Yes)
            {
                if (ModelStore.RemoveModel(model))
                {
                    RefreshListView();
                }
                else
                {
                    MessageBox.Show("Xóa thất bại.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}
