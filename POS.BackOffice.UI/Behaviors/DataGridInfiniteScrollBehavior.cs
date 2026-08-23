using Microsoft.Xaml.Behaviors;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace POS.BackOffice.UI.Behaviors
{
    public class DataGridInfiniteScrollBehavior : Behavior<DataGrid>
    {
        private ScrollViewer? _scrollViewer;

        public static readonly DependencyProperty LoadMoreCommandProperty =
            DependencyProperty.Register(
                nameof(LoadMoreCommand),
                typeof(ICommand),
                typeof(DataGridInfiniteScrollBehavior),
                new PropertyMetadata(null));

        public ICommand LoadMoreCommand
        {
            get => (ICommand)GetValue(LoadMoreCommandProperty);
            set => SetValue(LoadMoreCommandProperty, value);
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.Loaded += AssociatedObject_Loaded;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.Loaded -= AssociatedObject_Loaded;
            if (_scrollViewer != null)
            {
                _scrollViewer.ScrollChanged -= ScrollViewer_ScrollChanged;
            }
            base.OnDetaching();
        }

        private void AssociatedObject_Loaded(object sender, RoutedEventArgs e)
        {
            // Find the ScrollViewer inside the DataGrid
            _scrollViewer = GetVisualChild<ScrollViewer>(AssociatedObject);
            if (_scrollViewer != null)
            {
                _scrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
            }
        }

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_scrollViewer == null) return;

            // Trigger when scrolled to the bottom (with a 20px threshold for early fetching)
            bool isScrolledToBottom = _scrollViewer.VerticalOffset >= _scrollViewer.ScrollableHeight - 20;
            
            // Only trigger if we are actively scrolling down (Change in VerticalOffset is positive)
            bool isScrollingDown = e.VerticalChange > 0;

            if (isScrolledToBottom && isScrollingDown)
            {
                if (LoadMoreCommand != null && LoadMoreCommand.CanExecute(null))
                {
                    LoadMoreCommand.Execute(null);
                }
            }
        }

        // Helper to find the visual child of a specific type
        private static T? GetVisualChild<T>(DependencyObject parent) where T : Visual
        {
            if (parent == null) return null;
            
            T? child = default;
            int numVisuals = VisualTreeHelper.GetChildrenCount(parent);
            
            for (int i = 0; i < numVisuals; i++)
            {
                Visual v = (Visual)VisualTreeHelper.GetChild(parent, i);
                child = v as T;
                if (child == null)
                {
                    child = GetVisualChild<T>(v);
                }
                if (child != null)
                {
                    break;
                }
            }
            return child;
        }
    }
}
