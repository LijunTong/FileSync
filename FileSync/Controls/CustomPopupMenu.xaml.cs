using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace FileSync.Controls
{
    public partial class CustomPopupMenu : UserControl
    {
        private readonly List<MenuItemData> _menuItems = new List<MenuItemData>();

        public CustomPopupMenu()
        {
            InitializeComponent();
        }

        public void AddItem(string text, Geometry icon, Action action, Brush foreground = null, bool isDanger = false)
        {
            _menuItems.Add(new MenuItemData
            {
                Text = text,
                Icon = icon,
                Action = action,
                Foreground = foreground,
                IsDanger = isDanger
            });
        }

        public void AddSeparator()
        {
            _menuItems.Add(new MenuItemData { IsSeparator = true });
        }

        public void Show()
        {
            BuildMenu();
            MenuPopup.IsOpen = true;
        }

        public void Hide()
        {
            MenuPopup.IsOpen = false;
            _menuItems.Clear();
        }

        private void BuildMenu()
        {
            MenuItemsPanel.Children.Clear();

            foreach (var item in _menuItems)
            {
                if (item.IsSeparator)
                {
                    var separator = new Border
                    {
                        Height = 1,
                        Background = (Brush)FindResource("BorderBrush"),
                        Margin = new Thickness(4, 4, 4, 4)
                    };
                    MenuItemsPanel.Children.Add(separator);
                }
                else
                {
                    var menuItem = CreateMenuItem(item);
                    MenuItemsPanel.Children.Add(menuItem);
                }
            }
        }

        private Border CreateMenuItem(MenuItemData item)
        {
            var foreground = item.Foreground ?? (Brush)FindResource("TextPrimaryBrush");
            if (item.IsDanger)
                foreground = (Brush)FindResource("DangerBrush");

            var iconPath = new Path
            {
                Data = item.Icon,
                Stroke = foreground,
                Fill = Brushes.Transparent,
                Width = 14,
                Height = 14,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            var textBlock = new TextBlock
            {
                Text = item.Text,
                Foreground = foreground,
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center
            };

            var contentPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(10, 6, 10, 6)
            };

            contentPanel.Children.Add(iconPath);
            contentPanel.Children.Add(textBlock);

            var border = new Border
            {
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(4),
                Child = contentPanel,
                Tag = item.Action
            };

            border.MouseLeftButtonDown += (s, e) =>
            {
                item.Action?.Invoke();
                Hide();
                e.Handled = true;
            };

            border.MouseEnter += (s, e) =>
            {
                border.Background = (Brush)FindResource("SurfaceHoverBrush");
            };

            border.MouseLeave += (s, e) =>
            {
                border.Background = Brushes.Transparent;
            };

            return border;
        }

        private class MenuItemData
        {
            public string Text { get; set; }
            public Geometry Icon { get; set; }
            public Action Action { get; set; }
            public Brush Foreground { get; set; }
            public bool IsDanger { get; set; }
            public bool IsSeparator { get; set; }
        }
    }
}
