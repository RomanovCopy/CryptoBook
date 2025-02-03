using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Media = System.Windows.Media;

namespace CryptoBook.Helpers
{
    static class ColorContrastHelper
    {
        // Вычисляет относительную яркость (luminance) для System.Drawing.Color
        private static double GetLuminance(Color color)
        {
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;

            r = (r <= 0.04045) ? r / 12.92 : Math.Pow((r + 0.055) / 1.055, 2.4);
            g = (g <= 0.04045) ? g / 12.92 : Math.Pow((g + 0.055) / 1.055, 2.4);
            b = (b <= 0.04045) ? b / 12.92 : Math.Pow((b + 0.055) / 1.055, 2.4);

            return 0.2126 * r + 0.7152 * g + 0.0722 * b;
        }

        // Вычисляет контрастное соотношение между двумя цветами
        private static double GetContrastRatio(Color color1, Color color2)
        {
            double lum1 = GetLuminance(color1);
            double lum2 = GetLuminance(color2);

            return (Math.Max(lum1, lum2) + 0.05) / (Math.Min(lum1, lum2) + 0.05);
        }

        // Выбирает наиболее контрастный цвет из списка
        public static Color GetMostContrastingColor(Color bgColor)
        {
            // Набор возможных цветов шрифта
            Color[] candidates = new Color[]
            {
            Color.Black, Color.White, Color.Red, Color.Blue, Color.Yellow,
            Color.Green, Color.Cyan, Color.Magenta, Color.Orange, Color.Purple
            };

            // Выбираем цвет с наибольшим контрастом
            return candidates.OrderByDescending(c => GetContrastRatio(bgColor, c)).First();
        }
    }
}
