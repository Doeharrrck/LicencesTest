using Model;
using System;
using System.Windows;

namespace MyApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow(ModelClass model)
        {
            InitializeComponent();
            DataContext = model;

            try
            {
                this.ViewTextBlock.Text = model.ModelText;
            }
            catch (InvalidProgramException ex)
            {
                MessageBox.Show($"No License: {ex.Message}");
            }
        }
    }
}
