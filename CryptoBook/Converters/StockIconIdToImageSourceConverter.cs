using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace CryptoBook.Converters
{
    public sealed class StockIconIdToImageSourceConverter: IValueConverter
    {
        private readonly IStockIconService _stockIconService;

        public StockIconIdToImageSourceConverter(IStockIconService stockIconService)
            => _stockIconService = stockIconService ?? throw new ArgumentNullException(nameof(stockIconService));

        // value: SHSTOCKICONID | string ("SIID_FOLDER") | uint/int
        // parameter: "small" | "large" | null (по умолчанию small)
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if(value is null)
                return null;

            var small = ParseSmall(parameter);

            if(TryParseId(value, out var id))
                return _stockIconService.GetStockIcon(id, small);

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();

        private static bool ParseSmall(object parameter)
        {
            if(parameter is null)
                return true;

            if(parameter is bool b)
                return b;

            var s = parameter.ToString()?.Trim();
            if(string.IsNullOrEmpty(s))
                return true;

            return !s.Equals("large", StringComparison.OrdinalIgnoreCase)
                   && !s.Equals("l", StringComparison.OrdinalIgnoreCase)
                   && !s.Equals("big", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryParseId(object value, out DTO.SHSTOCKICONID id)
        {
            switch(value)
            {
                case DTO.SHSTOCKICONID e:
                id = e;
                return true;

                case string s:
                s = s.Trim();
                if(Enum.TryParse(s, ignoreCase: true, out DTO.SHSTOCKICONID parsed))
                {
                    id = parsed;
                    return true;
                }
                break;

                case int i:
                id = (DTO.SHSTOCKICONID)(uint)i;
                return true;

                case uint u:
                id = (DTO.SHSTOCKICONID)u;
                return true;

                case long l when l >= 0 && l <= uint.MaxValue:
                id = (DTO.SHSTOCKICONID)(uint)l;
                return true;

                case short sh when sh >= 0:
                id = (DTO.SHSTOCKICONID)(uint)sh;
                return true;
            }

            id = default;
            return false;
        }
    }
}
