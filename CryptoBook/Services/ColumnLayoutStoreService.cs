using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CryptoBook.Services
{
    public class ColumnLayoutStoreService:IColumnLayoutStore
    {

        private const string SettingName = "GridViewColumnRatios";
        private const string FileExplorerViewId = "FileExplorer.MainGrid";
        private const string FileExplorerFlatViewId =
            "FileExplorer.MainGrid|Name,RelativeDirectory,LastWriteTimeUtc,Extension,Size";
        private static readonly double[] FileExplorerDefaultRatios =
            [0.46, 0.27, 0.15, 0.12];
        private static readonly double[] FileExplorerFlatDefaultRatios =
            [0.30, 0.28, 0.20, 0.12, 0.10];

        private sealed class Model
        {
            public Dictionary<string, double[]> Items { get; set; } = new();
        }



        public bool TryLoad(string viewId, out IReadOnlyList<double> ratios)
        {
            ratios = Array.Empty<double>();

            var json = Properties.Settings.Default[SettingName] as string;
            if(string.IsNullOrWhiteSpace(json))
                return TryLoadDefault(viewId, out ratios);

            Model? model;
            try
            { model = JsonSerializer.Deserialize<Model>(json); } catch { return TryLoadDefault(viewId, out ratios); }

            if(model?.Items is null) return TryLoadDefault(viewId, out ratios);
            if(!model.Items.TryGetValue(viewId, out var arr)) return TryLoadDefault(viewId, out ratios);
            if(arr is null || arr.Length == 0) return TryLoadDefault(viewId, out ratios);

            // защита от мусора
            if(arr.Any(x => double.IsNaN(x) || x <= 0)) return false;

            ratios = arr;
            return true;
        }

        private static bool TryLoadDefault(
            string viewId,
            out IReadOnlyList<double> ratios)
        {
            if(string.Equals(
                viewId,
                FileExplorerViewId,
                StringComparison.Ordinal))
            {
                ratios = FileExplorerDefaultRatios;
                return true;
            }
            if(string.Equals(
                viewId,
                FileExplorerFlatViewId,
                StringComparison.Ordinal))
            {
                ratios = FileExplorerFlatDefaultRatios;
                return true;
            }

            ratios = Array.Empty<double>();
            return false;
        }


        public void Save(string viewId, IReadOnlyList<double> ratios)
        {
            if(string.IsNullOrWhiteSpace(viewId))
                return;
            if(ratios is null || ratios.Count == 0)
                return;

            var json = Properties.Settings.Default[SettingName] as string;
            Model model;
            try
            { model = string.IsNullOrWhiteSpace(json) ? new Model() : (JsonSerializer.Deserialize<Model>(json) ?? new Model()); } catch { model = new Model(); }

            model.Items[viewId] = ratios.ToArray();

            Properties.Settings.Default[SettingName] = JsonSerializer.Serialize(model);
            Properties.Settings.Default.Save();
        }
    }
}
