using Microsoft.Xaml.Behaviors;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace CryptoBook.Helpers
{
    public class BehaviorComboBox: Behavior<System.Windows.Controls.ComboBox>
    {

        public ItemCollection Items => AssociatedObject.Items;

        public int SelectedIndex
        {
            get => AssociatedObject.SelectedIndex;
            set => AssociatedObject.SelectedIndex = value;
        }

        public object SelectedItem
        {
            get => AssociatedObject.SelectedItem;
            set => AssociatedObject.SelectedItem = value;
        }

        public object SelectedValue
        {
            get => AssociatedObject.SelectedValue;
            set => AssociatedObject.SelectedValue = value;
        }

        public object SelectedValuePath
        {
            get => AssociatedObject.SelectedValuePath;
            set => AssociatedObject.SelectedValuePath = (string)value;
        }

        public IEnumerable ItemsSource
        {
            get => AssociatedObject.ItemsSource;
            set => AssociatedObject.ItemsSource = value;
        }

        public object Tag
        {
            get => AssociatedObject.Tag;
            set => AssociatedObject.Tag = value;
        }

        protected override void OnAttached()
        {

        }

        protected override void OnDetaching()
        {
        }


        public event SelectionChangedEventHandler SelectionChanged
        {
            add
            {
                AssociatedObject.SelectionChanged += value;
            }
            remove
            {
                AssociatedObject.SelectionChanged -= value;
            }
        }

        public event RoutedEventHandler Loaded
        {
            add
            {
                AssociatedObject.Loaded += value;
            }
            remove
            {
                AssociatedObject.Loaded -= value;
            }
        }
    }
}
