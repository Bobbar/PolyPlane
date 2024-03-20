namespace PolyPlane
{
    public partial class ClientServerConfigForm : Form
    {
        public string IPAddress;
        public ushort Port;
        public bool IsServer = true;
        public bool IsAI = false;

        public ClientServerConfigForm()
        {
            InitializeComponent();
        }

        private void ClientServerConfigForm_Load(object sender, EventArgs e)
        {

        }

        private void StartServerButton_Click(object sender, EventArgs e)
        {
            IsServer = true;
            IPAddress = IPAddressTextBox.Text.Trim();
            Port = ushort.Parse(PortTextBox.Text.Trim());

            DialogResult = DialogResult.OK;
        }

        private void StartClientButton_Click(object sender, EventArgs e)
        {
            IPAddress = IPAddressTextBox.Text.Trim();
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
    }
}
