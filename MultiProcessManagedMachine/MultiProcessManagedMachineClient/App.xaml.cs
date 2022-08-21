using System.Windows;

namespace MultiProcessManagedMachineClient
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        void App_Startup(object sender, StartupEventArgs e)
        {
            // Application is running
            // Process command line args
            ProcessClient processClient;

            if (e.Args.Length == 1)
            {
                processClient = new ProcessClient(e.Args[0]);
            }
        }
    }
}
