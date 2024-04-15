using PolyPlane.Net;
using PolyPlane.Net.Discovery;
using PolyPlane.Rendering;
using System.ComponentModel;
using System.Net;
using unvell.D2DLib;

namespace PolyPlane
{
    public partial class ClientServerConfigForm : Form
    {
        public string ServerIPAddress;
        public string ClientIPAddress;
        public string PlayerName;
        public ushort Port;
        public bool IsServer = true;
        public bool IsAI = false;
        public D2DColor PlaneColor = D2DColor.Randomly();

        private DiscoveryServer _discovery;
        private BindingList<ServerEntry> _serverEntries = new BindingList<ServerEntry>();
        private PlanePreview _planePreview;

        public ClientServerConfigForm()
        {
            InitializeComponent();

            this.Disposed += ClientServerConfigForm_Disposed;

            _planePreview = new PlanePreview(PlanePreviewBox, PlaneColor);
        }

        private void ClientServerConfigForm_Disposed(object? sender, EventArgs e)
        {
            _planePreview?.Dispose();
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

            _serverEntries.RaiseListChangedEvents = true;

            ServerListBox.DataBindings.Clear();
            ServerListBox.DataSource = _serverEntries;
        }

        private void Discovery_NewDiscoveryReceived(object? sender, DiscoveryPacket e)
        {
            try
            {
                if (this.InvokeRequired)
                {
                    this.Invoke(() => Discovery_NewDiscoveryReceived(sender, e));
                }
                else
                {
                    if (!_serverEntries.Any(s => (s.IP == e.IP && s.Port == e.Port)))
                        _serverEntries.Add(new ServerEntry(e.IP, e.Port, e.Name));
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

        private void StartClientButton_Click(object sender, EventArgs e)
        {
            if (IPAddress.TryParse(IPAddressTextBox.Text.Trim(), out IPAddress addy))
            {
                PlayerName = PlayerNameTextBox.Text.Trim();
                ServerIPAddress = addy.ToString();
                Port = ushort.Parse(PortTextBox.Text.Trim());

                DialogResult = DialogResult.OK;
            }
            else
            {
                ErrorLabel.Text = "Invalid IP address!";
                ErrorLabel.Visible = true;
            }
        }

        private void SinglePlayerButton_Click(object sender, EventArgs e)
        {
            PlayerName = PlayerNameTextBox.Text.Trim();
            DialogResult = DialogResult.Cancel;
        }

        private void AIPlaneCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            IsAI = AIPlaneCheckBox.Checked;
        }

        private void ServerListBox_SelectedValueChanged(object sender, EventArgs e)
        {
            ErrorLabel.Visible = false;

            var selected = ServerListBox.SelectedItem;

            if (selected != null)
            {
                var entry = selected as ServerEntry;
                IPAddressTextBox.Text = entry.IP;
                PortTextBox.Text = entry.Port.ToString();
            }
        }

        private void ClientServerConfigForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            _discovery?.StopListen();
            _discovery?.Dispose();
        }

        private void ExitButton_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Abort;
        }

        private void NewColorButton_Click(object sender, EventArgs e)
        {
            PlaneColor = D2DColor.Randomly();
            _planePreview.PlaneColor = PlaneColor;
        }

        private class ServerEntry
        {
            public string IP { get; set; }
            public int Port;
            public string Name { get; set; }

            public ServerEntry(string iP, int port, string name)
            {
                IP = iP;
                Port = port;
                Name = name;
            }

            public override string ToString()
            {
                return $"{IP} - {Name}";
            }
        }

    }
}
