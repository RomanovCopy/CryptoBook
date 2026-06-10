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

        private sealed class Model
        {
            public Dictionary<string, double[]> Items { get; set; } = new();
        }



        public bool TryLoad(string viewId, out IReadOnlyList<double> ratios)
        {
            ratios = Array.Empty<double>();

            var json = Properties.Settings.Default[SettingName] as string;
            if(string.IsNullOrWhiteSpace(json))
                return false;

            Model? model;
            try
            { model = JsonSerializer.Deserialize<Model>(json); } catch { return false; }

            if(model?.Items is null) return false;
            if(!model.Items.TryGetValue(viewId, out var arr)) return false;
            if(arr is null || arr.Length == 0) return false;

            // защита от мусора
            if(arr.Any(x => double.IsNaN(x) || x <= 0)) return false;

            ratios = arr;
            return true;
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
