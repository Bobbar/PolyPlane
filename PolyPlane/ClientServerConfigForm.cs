using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PolyPlane
{
    public partial class ClientServerConfigForm : Form
    {
        public string IPAddress;
        public ushort Port;
        public bool IsServer = true;

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
            IsServer = false;
            IPAddress = IPAddressTextBox.Text.Trim();
            Port = ushort.Parse(PortTextBox.Text.Trim());

            DialogResult = DialogResult.OK;
        }

        private void SinglePlayerButton_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }
    }
}
