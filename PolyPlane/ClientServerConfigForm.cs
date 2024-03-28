using PolyPlane.Net;
using PolyPlane.Net.Discovery;

namespace PolyPlane
{
    public partial class ClientServerConfigForm : Form
    {
        public string ServerIPAddress;
        public string ClientIPAddress;
        public ushort Port;
        public bool IsServer = true;
        public bool IsAI = false;

        private DiscoveryServer _discovery;
        private List<DiscoveryPacket> _servers = new List<DiscoveryPacket>();

        public ClientServerConfigForm()
        {
            InitializeComponent();
        }

        private void ClientServerConfigForm_Load(object sender, EventArgs e)
        {
            SetLocalIP();

            if (!string.IsNullOrEmpty(ClientIPAddress))
            {
                _discovery = new DiscoveryServer();
                _discovery.NewDiscoveryReceived += Discovery_NewDiscoveryReceived;
                _discovery.StartListen();
            }
        }

        private void Discovery_NewDiscoveryReceived(object? sender, Net.DiscoveryPacket e)
        {
            try
            {
                if (this.InvokeRequired)
                {
                    this.Invoke(() => Discovery_NewDiscoveryReceived(sender, e));
                }
                else
                {
                    if (!_servers.Any(s => s.IP == e.IP))
                    {
                        _servers.Add(e);
                    }

                    UpdateListBox();
                }
            }
            catch
            {

            }
        }

        private void SetLocalIP()
        {
            var addy = Helpers.GetLocalIP();

            if (addy != null)
            {
                ClientIPAddress = addy;
            }
            else
            {
                throw new Exception("No local IP was found.");
            }
        }

        private void UpdateListBox()
        {
            ServerListBox.Items.Clear();

            foreach (var server in _servers)
            {
                ServerListBox.Items.Add(server.IP);
            }

            ServerListBox.Refresh();
        }

        private void StartClientButton_Click(object sender, EventArgs e)
        {
            ServerIPAddress = IPAddressTextBox.Text.Trim();
            Port = ushort.Parse(PortTextBox.Text.Trim());

            DialogResult = DialogResult.OK;
        }

        private void SinglePlayerButton_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }

        private void AIPlaneCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            IsAI = AIPlaneCheckBox.Checked;
        }

        private void ServerListBox_SelectedValueChanged(object sender, EventArgs e)
        {
            var selected = ServerListBox.SelectedItem as string;

            if (selected != null)
                IPAddressTextBox.Text = selected;
        }

        private void ClientServerConfigForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            _discovery?.StopListen();
            _discovery?.Dispose();
        }
    }
}
