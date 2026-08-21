using System.Windows;

using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace CryptoBook.Services
{
    internal static class WindowLayoutDefaults
    {
        private const double MediaPlayerCascadeOffset = 36;
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

        public static Point CreateMediaPlayerCascade(
            Rect workArea,
            Rect anchor,
            Size windowSize,
            IReadOnlyCollection<Rect> occupiedBounds)
        {
            if(!IsUsable(workArea) ||
               !IsFinitePositive(windowSize.Width) ||
               !IsFinitePositive(windowSize.Height))
            {
                return new Point(
                    anchor.Left + MediaPlayerCascadeOffset,
                    anchor.Top + MediaPlayerCascadeOffset);
            }

            double maximumLeft = Math.Max(
                workArea.Left,
                workArea.Right - windowSize.Width);
            double maximumTop = Math.Max(
                workArea.Top,
                workArea.Bottom - windowSize.Height);
            var desired = new Point(
                Wrap(
                    anchor.Left + MediaPlayerCascadeOffset,
                    workArea.Left,
                    maximumLeft),
                Wrap(
                    anchor.Top + MediaPlayerCascadeOffset,
                    workArea.Top,
                    maximumTop));

            var candidates = new List<Point> { desired };
            AddGridCandidates(
                candidates,
                workArea.Left,
                maximumLeft,
                workArea.Top,
                maximumTop);

            foreach(var candidate in candidates
                .Distinct()
                .OrderBy(point => DistanceSquared(point, desired)))
            {
                var bounds = new Rect(candidate, windowSize);
                if(occupiedBounds.All(occupied =>
                    !CompletelyOverlaps(bounds, occupied)))
                {
                    return candidate;
                }
            }

            return desired;
        }

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

        private static void AddGridCandidates(
            ICollection<Point> candidates,
            double minimumLeft,
            double maximumLeft,
            double minimumTop,
            double maximumTop)
        {
            var leftPositions = CreateGridPositions(minimumLeft, maximumLeft);
            var topPositions = CreateGridPositions(minimumTop, maximumTop);
            foreach(double top in topPositions)
            {
                foreach(double left in leftPositions)
                    candidates.Add(new Point(left, top));
            }
        }

        private static IReadOnlyList<double> CreateGridPositions(
            double minimum,
            double maximum)
        {
            var positions = new List<double> { minimum };
            for(double value = minimum + MediaPlayerCascadeOffset;
                value < maximum;
                value += MediaPlayerCascadeOffset)
            {
                positions.Add(value);
            }

            if(maximum > minimum)
                positions.Add(maximum);
            return positions;
        }

        private static bool CompletelyOverlaps(Rect first, Rect second) =>
            Contains(first, second) || Contains(second, first);

        private static bool Contains(Rect outer, Rect inner) =>
            outer.Left <= inner.Left &&
            outer.Top <= inner.Top &&
            outer.Right >= inner.Right &&
            outer.Bottom >= inner.Bottom;

        private static double DistanceSquared(Point first, Point second)
        {
            double x = first.X - second.X;
            double y = first.Y - second.Y;
            return (x * x) + (y * y);
        }

        private static double Wrap(double value, double minimum, double maximum)
        {
            if(maximum <= minimum)
                return minimum;
            if(value >= minimum && value <= maximum)
                return value;

            double range = maximum - minimum;
            double offset = (value - minimum) % range;
            if(offset < 0)
                offset += range;
            return minimum + offset;
        }

        private static bool IsUsable(Rect value) =>
            !value.IsEmpty &&
            IsFinitePositive(value.Width) &&
            IsFinitePositive(value.Height);

        private static bool IsFinitePositive(double value) =>
            value > 0 && !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
