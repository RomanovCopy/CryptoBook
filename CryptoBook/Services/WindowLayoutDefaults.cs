using System.Windows;

namespace CryptoBook.Services
{
    internal static class WindowLayoutDefaults
    {
        private const double MainPreferredWidth = 1200;
        private const double MainPreferredHeight = 800;
        private const double ExplorerPreferredWidth = 1100;
        private const double ExplorerPreferredHeight = 720;

        public static bool IsLegacyMainSize(double width, double height) =>
            !IsFinitePositive(width) ||
            !IsFinitePositive(height) ||
            (width <= 400 && height <= 100);

        public static bool IsLegacyExplorerSize(double width, double height) =>
            !IsFinitePositive(width) ||
            !IsFinitePositive(height) ||
            (width <= 600 && height <= 400);

        public static Rect CreateMain(Rect workArea) =>
            CreateCentered(workArea, MainPreferredWidth, MainPreferredHeight);

        public static Rect CreateExplorer(Rect workArea) =>
            CreateCentered(workArea, ExplorerPreferredWidth, ExplorerPreferredHeight);

        private static Rect CreateCentered(
            Rect workArea,
            double preferredWidth,
            double preferredHeight)
        {
            if(workArea.IsEmpty ||
               !IsFinitePositive(workArea.Width) ||
               !IsFinitePositive(workArea.Height))
            {
                return new Rect(80, 60, preferredWidth, preferredHeight);
            }

            double width = Math.Min(preferredWidth, workArea.Width * 0.9);
            double height = Math.Min(preferredHeight, workArea.Height * 0.9);
            double left = workArea.Left + ((workArea.Width - width) / 2);
            double top = workArea.Top + ((workArea.Height - height) / 2);

            return new Rect(left, top, width, height);
        }

        private static bool IsFinitePositive(double value) =>
            value > 0 && !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
