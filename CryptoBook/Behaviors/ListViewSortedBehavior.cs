using CryptoBook.Interfaces;

using Microsoft.Xaml.Behaviors;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace CryptoBook.Behaviors
{
    public sealed class ListViewSortedBehavior: Behavior<System.Windows.Controls.ListView>
    {
        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.AddHandler(GridViewColumnHeader.ClickEvent, new RoutedEventHandler(OnHeaderClick));

            DependencyPropertyDescriptor.FromProperty( ItemsControl.ItemsSourceProperty,
            typeof(System.Windows.Controls.ListView))?.AddValueChanged( AssociatedObject, OnItemsSourceChanged);

            TryInitializeView();
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();
        }


        private void OnHeaderClick(object sender, RoutedEventArgs e)
        {
            if(e.OriginalSource is GridViewColumnHeader header && header.Tag is string name)
            {
                if(sender is System.Windows.Controls.ListView lv)
                {
                    var view = CollectionViewSource.GetDefaultView(lv.ItemsSource);
                    view.SortDescriptions.Clear();
                    switch(name)
                    {
                        case "Name":
                        {
                            view.SortDescriptions.Add(new SortDescription("Kind", ListSortDirection.Ascending));
                            view.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
                            break;
                        }
                        case "LastWriteTimeUtc":
                        {
                            view.SortDescriptions.Add(new SortDescription("Kind", ListSortDirection.Ascending));
                            view.SortDescriptions.Add(new SortDescription("LastWriteTimeUtc", ListSortDirection.Ascending));
                            view.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
                            break;
                        }
                        case "Extension":
                        {
                            view.SortDescriptions.Add(new SortDescription("Kind", ListSortDirection.Ascending));
                            view.SortDescriptions.Add(new SortDescription("Extension", ListSortDirection.Ascending));
                            view.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
                            break;
                        }
                        case "Size":
                        {
                            view.SortDescriptions.Add(new SortDescription("Kind", ListSortDirection.Ascending));
                            view.SortDescriptions.Add(new SortDescription("Size", ListSortDirection.Ascending));
                            view.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
                            break;
                        }
                    }
                }
            }
        }

        private void OnItemsSourceChanged(object? sender, EventArgs e)
        {
            TryInitializeView();
        }

        private void TryInitializeView()
        {
            if(AssociatedObject.ItemsSource is null)
                return;

            ICollectionView view = CollectionViewSource.GetDefaultView( AssociatedObject.ItemsSource);

            if(view == null)
                return;

            view.SortDescriptions.Clear();

            view.SortDescriptions.Add(new SortDescription("Kind", ListSortDirection.Ascending));
            view.SortDescriptions.Add( new SortDescription( nameof(ISystemItem.Name), ListSortDirection.Ascending));
        }


    }
}
