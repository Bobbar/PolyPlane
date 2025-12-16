using CommandLine;

namespace PolyPlane
{
    public sealed class CommandLineOptions
    {
        [Option('a', "Is AI", Default = false, HelpText = "True if the client plane will be an AI bot.", Required = false)]
        public bool IsAI { get; set; }

        [Option('r', "Disable Rendering", Default = false, HelpText = "True the client will start with rendering disabled.", Required = false)]
        public bool DisableRender { get; set; }

        [Option('n', "Player Name", Default = "Player", HelpText = "Specify the player name for the client.", Required = false)]
        public string? PlayerName { get; set; }

        [Option('i', "IP Address", Default = "127.0.0.1", HelpText = "Specify the IP address of the server to connect to.", Required = false)]
        public string? IPAddress { get; set; }

        [Option('p', "Port", Default = (ushort)1234, HelpText = "Specify the port number for the server.", Required = false)]
        public ushort Port { get; set; }

    }
}
