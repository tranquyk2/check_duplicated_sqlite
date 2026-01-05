using System.Text.Json;

namespace Scanner
{
    public static class ModelStore
    {
        private static readonly List<Model> _models = new List<Model>();
        private static readonly HashSet<string> _scanned = new HashSet<string>();

        private static readonly string DataFolder;
        private static readonly string ModelsFilePath;

        static ModelStore()
        {
            DataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Scanner");
            ModelsFilePath = Path.Combine(DataFolder, "models.json");
            LoadModels();
        }

        public static IReadOnlyList<Model> Models => _models.AsReadOnly();

        public static void AddModel(string name, string barcode)
        {
            var n = name?.Trim() ?? string.Empty;
            var b = barcode?.Trim() ?? string.Empty;
            var id = !string.IsNullOrEmpty(b) ? b : n;
            if (string.IsNullOrEmpty(id)) return;
            if (!string.IsNullOrEmpty(b))
            {
                if (_models.Exists(m => !string.IsNullOrEmpty(m.Barcode) && m.Barcode == b)) return;
            }
            else
            {
                if (_models.Exists(m => string.IsNullOrEmpty(m.Barcode) && m.Name == n)) return;
            }
            _models.Add(new Model { Name = n, Barcode = b });
            SaveModels();
        }

        public static bool TryMatchModel(string barcode, out string matchedModel)
        {
            matchedModel = null!;
            if (string.IsNullOrWhiteSpace(barcode)) return false;
            barcode = barcode.Trim();
            string best = null!;
            foreach (var m in _models)
            {
                var key = !string.IsNullOrEmpty(m.Barcode) ? m.Barcode : m.Name;
                if (string.IsNullOrEmpty(key)) continue;
                if (barcode.StartsWith(key))
                {
                    if (best == null || key.Length > best.Length) best = key;
                }
            }
            if (best != null)
            {
                matchedModel = best;
                return true;
            }
            return false;
        }

        public static bool IsBarcodeScanned(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode)) return false;
            return _scanned.Contains(barcode.Trim());
        }

        public static void MarkScanned(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode)) return;
            _scanned.Add(barcode.Trim());
        }

        public static void ClearScanned() => _scanned.Clear();

        public static bool RemoveModel(string model)
        {
            if (string.IsNullOrWhiteSpace(model)) return false;
            model = model.Trim();
            var removed = _models.RemoveAll(m => m.Barcode == model || m.Name == model) > 0;
            if (removed) SaveModels();
            return removed;
        }

        public static bool UpdateModel(string oldModelIdentifier, string newName, string newBarcode)
        {
            if (string.IsNullOrWhiteSpace(oldModelIdentifier)) return false;
            oldModelIdentifier = oldModelIdentifier.Trim();
            int idx = _models.FindIndex(m => m.Barcode == oldModelIdentifier || m.Name == oldModelIdentifier);
            if (idx == -1) return false;

            var n = newName?.Trim();
            var b = newBarcode?.Trim();

            if (string.IsNullOrEmpty(n)) n = _models[idx].Name;
            if (string.IsNullOrEmpty(b)) b = _models[idx].Barcode;

            if (!string.IsNullOrEmpty(b))
            {
                var conflict = _models.FindIndex((m) => !string.IsNullOrEmpty(m.Barcode) && m.Barcode == b);
                if (conflict != -1 && conflict != idx) return false;
            }
            else
            {
                var conflict = _models.FindIndex((m) => string.IsNullOrEmpty(m.Barcode) && m.Name == n);
                if (conflict != -1 && conflict != idx) return false;
            }

            _models[idx].Name = n ?? string.Empty;
            _models[idx].Barcode = b ?? string.Empty;

            SaveModels();
            return true;
        }

        private static void SaveModels()
        {
            try
            {
                if (!Directory.Exists(DataFolder)) Directory.CreateDirectory(DataFolder);
                var opts = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(_models, opts);
                File.WriteAllText(ModelsFilePath, json);
            }
            catch
            {

            }
        }

        private static void LoadModels()
        {
            try
            {
                if (!File.Exists(ModelsFilePath)) return;
                var json = File.ReadAllText(ModelsFilePath);

                try
                {
                    var list = JsonSerializer.Deserialize<List<Model>>(json);
                    if (list != null)
                    {
                        _models.Clear();
                        foreach (var m in list)
                        {
                            _models.Add(new Model { Name = m.Name?.Trim() ?? string.Empty, Barcode = m.Barcode?.Trim() ?? string.Empty });
                        }
                        return;
                    }
                }
                catch
                {

                }

                try
                {
                    var list2 = JsonSerializer.Deserialize<List<string>>(json);
                    if (list2 != null)
                    {
                        _models.Clear();
                        foreach (var s in list2)
                        {
                            if (!string.IsNullOrWhiteSpace(s)) _models.Add(new Model { Name = string.Empty, Barcode = s.Trim() });
                        }
                        SaveModels();
                    }
                }
                catch
                {

                }
            }
            catch
            {

            }
        }
    }
}
