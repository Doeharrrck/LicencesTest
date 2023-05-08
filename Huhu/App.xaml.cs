using Model;
using System.Windows;

namespace Huhu
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            var model = new ModelClass();
            var wnd = new MainWindow(model);

            wnd.Show();
        }
    }
}
