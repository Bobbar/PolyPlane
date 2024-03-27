using PolyPlane.Net;
using PolyPlane.Net.Discovery;
using System.Net;

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
                _discovery = new DiscoveryServer(ClientIPAddress);
                _discovery.NewDiscoveryReceived += Discovery_NewDiscoveryReceived;
                _discovery.Start();
            }
        }

        private void Discovery_NewDiscoveryReceived(object? sender, Net.DiscoveryPacket e)
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

        private void StartServerButton_Click(object sender, EventArgs e)
        {
            IsServer = true;
            ServerIPAddress = IPAddressTextBox.Text.Trim();
            Port = ushort.Parse(PortTextBox.Text.Trim());

            DialogResult = DialogResult.OK;
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

        private void RefreshServersButton_Click(object sender, EventArgs e)
        {
            _servers.Clear();
            //ServerListBox.DisplayMember = $"{nameof(DiscoveryPacket.IP)}";
            //ServerListBox.ValueMember = $"{nameof(DiscoveryPacket.IP)}";
            ServerListBox.DataSource = _servers;
            ServerListBox.DisplayMember = $"{nameof(DiscoveryPacket.IP)}";
            ServerListBox.ValueMember = $"{nameof(DiscoveryPacket.IP)}";
            ServerListBox.Refresh();

            //_discovery?.QueryForServers(ClientIPAddress);
        }

        private void ServerListBox_SelectedValueChanged(object sender, EventArgs e)
        {
            var selected = ServerListBox.SelectedItem as string;

            if (selected != null)
                IPAddressTextBox.Text = selected;
        }
    }
}
