using CryptoBook.Interfaces;

using Microsoft.Xaml.Behaviors;

using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace CryptoBook.Behaviors
{

    public sealed class GridViewColumnRatioBehavior: Behavior<System.Windows.Controls.ListView>
    {
        public static readonly DependencyProperty ViewIdProperty = DependencyProperty.Register(nameof(ViewId), typeof(string), 
        typeof(GridViewColumnRatioBehavior), new PropertyMetadata("default"));

        public string ViewId
        {
            get => (string)GetValue(ViewIdProperty);
            set => SetValue(ViewIdProperty, value);
        }

        public static readonly DependencyProperty StoreProperty = DependencyProperty.Register(nameof(Store), typeof(IColumnLayoutStore), 
        typeof(GridViewColumnRatioBehavior), new PropertyMetadata(null));

        public IColumnLayoutStore? Store
        {
            get => (IColumnLayoutStore?)GetValue(StoreProperty);
            set => SetValue(StoreProperty, value);
        }

        public static readonly DependencyProperty MinColumnWidthProperty = DependencyProperty.Register(nameof(MinColumnWidth), 
        typeof(double), typeof(GridViewColumnRatioBehavior), new PropertyMetadata(40d));

        public double MinColumnWidth
        {
            get => (double)GetValue(MinColumnWidthProperty);
            set => SetValue(MinColumnWidthProperty, value);
        }

        public static readonly DependencyProperty ReflowOnSizeChangedProperty = DependencyProperty.Register(nameof(ReflowOnSizeChanged), 
        typeof(bool), typeof(GridViewColumnRatioBehavior), new PropertyMetadata(true));

        public bool ReflowOnSizeChanged
        {
            get => (bool)GetValue(ReflowOnSizeChangedProperty);
            set => SetValue(ReflowOnSizeChangedProperty, value);
        }

        /// <summary>
        /// Если true — пересчитываем ширины при ScrollChanged (появление/исчезновение вертикального скролла меняет ViewportWidth)
        /// </summary>
        public static readonly DependencyProperty ReflowOnScrollViewerChangedProperty = DependencyProperty.Register(
        nameof(ReflowOnScrollViewerChanged), typeof(bool), typeof(GridViewColumnRatioBehavior), new PropertyMetadata(true));

        public bool ReflowOnScrollViewerChanged
        {
            get => (bool)GetValue(ReflowOnScrollViewerChangedProperty);
            set => SetValue(ReflowOnScrollViewerChangedProperty, value);
        }

        /// <summary>
        /// Дебаунс сохранения (ms)
        /// </summary>
        public static readonly DependencyProperty SaveDebounceMsProperty =
            DependencyProperty.Register(nameof(SaveDebounceMs), typeof(int), typeof(GridViewColumnRatioBehavior),
                new PropertyMetadata(400));
        public int SaveDebounceMs
        {
            get => (int)GetValue(SaveDebounceMsProperty);
            set => SetValue(SaveDebounceMsProperty, value);
        }

        // ---- state ----
        private DispatcherTimer? _saveTimer;
        private bool _applying;              // чтобы не ловить собственные изменения
        private bool _restoredOnce;
        private IReadOnlyList<double>? _lastRatios;

        private ScrollViewer? _scrollViewer;
        private double _lastViewportWidth;

        protected override void OnAttached()
        {
            base.OnAttached();

            _saveTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(Math.Max(50, SaveDebounceMs))
            };
            _saveTimer.Tick += SaveTimer_Tick;

            AssociatedObject.Loaded += OnLoaded;
            AssociatedObject.Unloaded += OnUnloaded;

            // drag-resize заголовков колонок
            AssociatedObject.AddHandler(FrameworkElement.SizeChangedEvent, new SizeChangedEventHandler(OnAnySizeChanged), 
            handledEventsToo: true);

            //по окончании изменения колонки
            //AssociatedObject.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(OnThumbDragCompleted), true);

            //  во время изменения колонки
            AssociatedObject.AddHandler(Thumb.DragDeltaEvent,
                new DragDeltaEventHandler(OnThumbDragDelta), true);

            if(ReflowOnSizeChanged)
                AssociatedObject.SizeChanged += OnListViewSizeChanged;
        }

        protected override void OnDetaching()
        {
            SaveNow();

            DetachScrollViewer();

            if(_saveTimer != null)
            {
                _saveTimer.Stop();
                _saveTimer.Tick -= SaveTimer_Tick;
                _saveTimer = null;
            }

            AssociatedObject.Loaded -= OnLoaded;
            AssociatedObject.Unloaded -= OnUnloaded;

            AssociatedObject.RemoveHandler(FrameworkElement.SizeChangedEvent,
                new SizeChangedEventHandler(OnAnySizeChanged));

            if(ReflowOnSizeChanged)
                AssociatedObject.SizeChanged -= OnListViewSizeChanged;

            base.OnDetaching();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            HookScrollViewer();

            if(_restoredOnce)
                return;
            _restoredOnce = true;

            // восстановление после layout
            AssociatedObject.Dispatcher.BeginInvoke(new Action(() =>
            {
                TryRestore();
                // зафиксируем viewport после восстановления
                _lastViewportWidth = GetViewportWidth();
            }), DispatcherPriority.Loaded);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            SaveNow();
            DetachScrollViewer();
        }

        private void OnListViewSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if(_applying)
                return;
            if(_lastRatios == null)
                return;

            // Меняется ширина -> применяем пропорции
            AssociatedObject.Dispatcher.BeginInvoke(new Action(() =>
            {
                ApplyRatios_NoGaps(_lastRatios);
                _lastViewportWidth = GetViewportWidth();
            }), DispatcherPriority.Background);
        }

        private void OnAnySizeChanged(object sender, SizeChangedEventArgs e)
        {
            if(_applying)
                return;

            // 1) если заголовок колонки — пользователь тащит разделитель => дебаунс-сохранение
            if(e.OriginalSource is GridViewColumnHeader)
            {
                DebouncedSave();
                return;
            }
        }

        private void OnThumbDragCompleted(object sender, DragCompletedEventArgs e)
        {
            if(_applying)
                return;
            if(_lastRatios == null)
                return;

            // DragCompleted прилетает от Thumb внутри GridViewColumnHeader
            if(e.OriginalSource is not Thumb thumb)
                return;

            // фильтр: это должен быть Thumb из заголовка колонки
            if(FindAncestor<GridViewColumnHeader>(thumb) is null)
                return;

            // Нормализуем: пересчитываем ratios из текущих фактических ширин
            // и сразу применяем "no gaps".
            NormalizeFromCurrentWidthsAndApply();

            // и сохраняем (можно debounce, можно сразу)
            DebouncedSave();
        }

        // Если очень хочется убирать пустоту "на лету" во время drag (может быть чуть дергано)
        private void OnThumbDragDelta(object sender, DragDeltaEventArgs e)
        {
            if(_applying)
                return;
            if(_lastRatios == null)
                return;

            if(e.OriginalSource is not Thumb thumb)
                return;
            if(FindAncestor<GridViewColumnHeader>(thumb) is null)
                return;

            // Легкая коррекция только последней flexible колонки, чтобы не было пустоты
            FixGapByAdjustingOneColumn();
        }


        private void NormalizeFromCurrentWidthsAndApply()
        {
            var cols = GetColumns();
            if(cols == null || cols.Length == 0)
                return;

            var widths = cols.Select(GetActualWidth).ToArray();
            var total = widths.Sum();
            if(total <= 0)
                return;

            var ratios = widths.Select(w => w / total).ToArray();

            _lastRatios = ratios;

            // ключ: применяем наш алгоритм, который гарантирует sum==viewport
            ApplyRatios_NoGaps(ratios);
        }


        /// <summary>
        /// Минимальная коррекция "без перерасчета ratios": просто добиваем/убираем diff в одной колонке.
        /// Удобно для DragDelta (чтобы не дёргать всё).
        /// </summary>
        private void FixGapByAdjustingOneColumn()
        {
            var cols = GetColumns();
            if(cols == null || cols.Length == 0)
                return;

            var available = GetViewportWidth();
            if(available <= 0)
                return;

            var sum = cols.Sum(c => c.Width);
            var diff = available - sum;

            if(Math.Abs(diff) < 0.5)
                return; // уже ок

            // поправим последнюю колонку, но не ниже MinWidth
            var last = cols[^1];
            var newW = last.Width + diff;
            if(newW < MinColumnWidth)
                newW = MinColumnWidth;

            _applying = true;
            try
            { last.Width = newW; } finally { _applying = false; }
        }


        private static T? FindAncestor<T>(DependencyObject start) where T : DependencyObject
        {
            DependencyObject? cur = start;
            while(cur != null)
            {
                if(cur is T typed)
                    return typed;
                cur = VisualTreeHelper.GetParent(cur);
            }
            return null;
        }


        private void HookScrollViewer()
        {
            if(!ReflowOnScrollViewerChanged)
                return;

            // ScrollViewer появляется после ApplyTemplate/Loaded
            _scrollViewer = FindDescendant<ScrollViewer>(AssociatedObject);
            if(_scrollViewer != null)
            {
                _scrollViewer.ScrollChanged += OnScrollChanged;
                _lastViewportWidth = GetViewportWidth();
            }
        }

        private void DetachScrollViewer()
        {
            if(_scrollViewer != null)
            {
                _scrollViewer.ScrollChanged -= OnScrollChanged;
                _scrollViewer = null;
            }
        }

        private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if(_applying)
                return;
            if(_lastRatios == null)
                return;

            // Нас интересует изменение viewport width (часто из-за вертикального scrollbar)
            var vw = GetViewportWidth();
            if(vw <= 0)
                return;

            // Чтобы не дергать постоянно: реагируем только если ширина реально изменилась
            if(Math.Abs(vw - _lastViewportWidth) < 0.5)
                return;

            _lastViewportWidth = vw;

            AssociatedObject.Dispatcher.BeginInvoke(new Action(() =>
            {
                ApplyRatios_NoGaps(_lastRatios);
            }), DispatcherPriority.Background);
        }

        private void SaveTimer_Tick(object? sender, EventArgs e)
        {
            _saveTimer?.Stop();
            SaveNow();
        }

        private void DebouncedSave()
        {
            if(_saveTimer == null)
                return;
            _saveTimer.Stop();
            _saveTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(50, SaveDebounceMs));
            _saveTimer.Start();
        }

        private void TryRestore()
        {
            if(Store == null)
                return;

            var cols = GetColumns();
            if(cols == null || cols.Length == 0)
                return;

            if(!Store.TryLoad(ViewId, out var ratios))
                return;
            if(ratios.Count != cols.Length)
                return;

            _lastRatios = ratios;
            ApplyRatios_NoGaps(ratios);
        }

        private void SaveNow()
        {
            if(Store == null)
                return;

            var cols = GetColumns();
            if(cols == null || cols.Length == 0)
                return;

            // Берём фактические ширины (ActualWidth)
            var widths = cols.Select(GetActualWidth).ToArray();
            var total = widths.Sum();
            if(total <= 0)
                return;

            // ratios сохраняем "как есть"
            var ratios = widths.Select(w => w / total).ToArray();

            _lastRatios = ratios;
            Store.Save(ViewId, ratios);
        }

        private GridViewColumn[]? GetColumns()
        {
            if(AssociatedObject.View is not GridView gv)
                return null;
            if(gv.Columns == null)
                return null;
            return gv.Columns.ToArray();
        }

        private double GetViewportWidth()
        {
            var sv = _scrollViewer ?? FindDescendant<ScrollViewer>(AssociatedObject);
            if(sv == null)
                return AssociatedObject.ActualWidth;

            // ViewportWidth — ширина контента без рамок/скроллбаров
            return sv.ViewportWidth > 0 ? sv.ViewportWidth : sv.ActualWidth;
        }

        /// <summary>
        /// Главная фишка: пропорционально + строго без пустоты справа
        /// (sum(widths) == viewportWidth) с учётом MinColumnWidth
        /// </summary>
        private void ApplyRatios_NoGaps(IReadOnlyList<double> ratios)
        {
            var cols = GetColumns();
            if(cols == null || cols.Length == 0)
                return;
            if(ratios.Count != cols.Length)
                return;

            var available = GetViewportWidth();
            if(available <= 0)
                return;

            var sumRatios = ratios.Sum();
            if(sumRatios <= 0)
                return;

            int n = cols.Length;

            _applying = true;
            try
            {
                // 1) целевые ширины по ratios
                var target = new double[n];
                for(int i = 0; i < n; i++)
                    target[i] = available * (ratios[i] / sumRatios);

                // 2) фиксируем те, кто упирается в MinWidth
                var widths = new double[n];
                var flexible = new bool[n];

                double usedMin = 0;
                for(int i = 0; i < n; i++)
                {
                    if(target[i] <= MinColumnWidth)
                    {
                        widths[i] = MinColumnWidth;
                        usedMin += MinColumnWidth;
                        flexible[i] = false;
                    } else
                    {
                        flexible[i] = true;
                    }
                }

                // Если MinWidth всех превысил available — идеально не получится, но MinWidth важнее.
                if(usedMin >= available)
                {
                    for(int i = 0; i < n; i++)
                        cols[i].Width = widths[i] > 0 ? widths[i] : MinColumnWidth;

                    return;
                }

                // 3) оставшееся распределяем пропорционально между flexible
                double remaining = available - usedMin;

                double flexWeight = 0;
                for(int i = 0; i < n; i++)
                    if(flexible[i])
                        flexWeight += target[i];

                if(flexWeight <= 0)
                {
                    // все стали min, но usedMin < available — добьём последнюю
                    for(int i = 0; i < n; i++)
                        cols[i].Width = widths[i] > 0 ? widths[i] : MinColumnWidth;

                    var sum = cols.Sum(c => c.Width);
                    var diff = available - sum;
                    cols[^1].Width = Math.Max(MinColumnWidth, cols[^1].Width + diff);
                    return;
                }

                for(int i = 0; i < n; i++)
                {
                    if(!flexible[i])
                        continue;
                    widths[i] = remaining * (target[i] / flexWeight);
                }

                // 4) применяем ширины
                for(int i = 0; i < n; i++)
                {
                    var w = widths[i] > 0 ? widths[i] : MinColumnWidth;
                    cols[i].Width = w;
                }

                // 5) Финальная коррекция на округления: diff добиваем/убираем в последней flexible колонке
                var sumW = cols.Sum(c => c.Width);
                var diff2 = available - sumW;

                int idx = FindLastFlexibleIndex(flexible);
                if(idx < 0)
                    idx = n - 1;

                var newW = cols[idx].Width + diff2;
                if(newW < MinColumnWidth)
                    newW = MinColumnWidth;

                cols[idx].Width = newW;
            } finally
            {
                _applying = false;
            }

            static int FindLastFlexibleIndex(bool[] flex)
            {
                for(int i = flex.Length - 1; i >= 0; i--)
                    if(flex[i])
                        return i;
                return -1;
            }
        }

        // Reflection для ActualWidth GridViewColumn
        private static readonly PropertyInfo? ActualWidthProp =
            typeof(GridViewColumn).GetProperty("ActualWidth", BindingFlags.Instance | BindingFlags.NonPublic);

        private static double GetActualWidth(GridViewColumn c)
        {
            if(ActualWidthProp?.GetValue(c) is double aw && aw > 0)
                return aw;
            return (!double.IsNaN(c.Width) && c.Width > 0) ? c.Width : 0;
        }

        private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
        {
            for(int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if(child is T typed)
                    return typed;

                var found = FindDescendant<T>(child);
                if(found != null)
                    return found;
            }
            return null;
        }
    }
}
