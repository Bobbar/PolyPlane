using CommandLine;

namespace PolyPlane
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            //if (args.Length > 0)
            {
                var parsedArgs = Parser.Default.ParseArguments<CommandLineOptions>(args);

                if (!parsedArgs.Errors.Any())
                    World.LaunchOptions = parsedArgs.Value;

            }

            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Application.SetHighDpiMode(HighDpiMode.DpiUnaware);
            Application.Run(new PolyPlaneUI());
        }
    }
}