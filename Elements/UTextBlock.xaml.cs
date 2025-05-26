using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace LinesBrowser
{
    public sealed partial class UTextBlock : UserControl
    {
        public UTextBlock()
        {
            this.InitializeComponent();
            this.Loaded += UTextBlock_Loaded;
        }

        private void UTextBlock_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyUpperCase();
        }

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(
                nameof(Text),
                typeof(string),
                typeof(UTextBlock),
                new PropertyMetadata(string.Empty, OnTextChanged));

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (UTextBlock)d;
            control.ApplyUpperCase();
        }

        private void ApplyUpperCase()
        {
            if (InnerTextBlock != null)
            {
                InnerTextBlock.Text = (Text ?? string.Empty).ToUpperInvariant();
            }
        }
    }
}
