namespace PolyPlane
{
    partial class ClientServerConfigForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            PortTextBox = new TextBox();
            IPAddressTextBox = new TextBox();
            label1 = new Label();
            label2 = new Label();
            StartClientButton = new Button();
            SinglePlayerButton = new Button();
            AIPlaneCheckBox = new CheckBox();
            ServerListBox = new ListBox();
            label3 = new Label();
            SuspendLayout();
            // 
            // PortTextBox
            // 
            PortTextBox.Location = new Point(309, 50);
            PortTextBox.Name = "PortTextBox";
            PortTextBox.Size = new Size(57, 23);
            PortTextBox.TabIndex = 0;
            PortTextBox.Text = "1234";
            // 
            // IPAddressTextBox
            // 
            IPAddressTextBox.Location = new Point(125, 50);
            IPAddressTextBox.Name = "IPAddressTextBox";
            IPAddressTextBox.Size = new Size(100, 23);
            IPAddressTextBox.TabIndex = 1;
            IPAddressTextBox.Text = "127.0.0.1";
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(99, 53);
            label1.Name = "label1";
            label1.Size = new Size(20, 15);
            label1.TabIndex = 2;
            label1.Text = "IP:";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(271, 53);
            label2.Name = "label2";
            label2.Size = new Size(32, 15);
            label2.TabIndex = 3;
            label2.Text = "Port:";
            // 
            // StartClientButton
            // 
            StartClientButton.Location = new Point(228, 133);
            StartClientButton.Name = "StartClientButton";
            StartClientButton.Size = new Size(75, 23);
            StartClientButton.TabIndex = 5;
            StartClientButton.Text = "Connect";
            StartClientButton.UseVisualStyleBackColor = true;
            StartClientButton.Click += StartClientButton_Click;
            // 
            // SinglePlayerButton
            // 
            SinglePlayerButton.Location = new Point(228, 175);
            SinglePlayerButton.Name = "SinglePlayerButton";
            SinglePlayerButton.Size = new Size(75, 23);
            SinglePlayerButton.TabIndex = 6;
            SinglePlayerButton.Text = "Solo";
            SinglePlayerButton.UseVisualStyleBackColor = true;
            SinglePlayerButton.Visible = false;
            SinglePlayerButton.Click += SinglePlayerButton_Click;
            // 
            // AIPlaneCheckBox
            // 
            AIPlaneCheckBox.AutoSize = true;
            AIPlaneCheckBox.Location = new Point(228, 108);
            AIPlaneCheckBox.Name = "AIPlaneCheckBox";
            AIPlaneCheckBox.Size = new Size(69, 19);
            AIPlaneCheckBox.TabIndex = 7;
            AIPlaneCheckBox.Text = "AI Plane";
            AIPlaneCheckBox.UseVisualStyleBackColor = true;
            AIPlaneCheckBox.CheckedChanged += AIPlaneCheckBox_CheckedChanged;
            // 
            // ServerListBox
            // 
            ServerListBox.FormattingEnabled = true;
            ServerListBox.ItemHeight = 15;
            ServerListBox.Location = new Point(125, 235);
            ServerListBox.Name = "ServerListBox";
            ServerListBox.Size = new Size(295, 154);
            ServerListBox.TabIndex = 8;
            ServerListBox.SelectedValueChanged += ServerListBox_SelectedValueChanged;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(125, 217);
            label3.Name = "label3";
            label3.Size = new Size(47, 15);
            label3.TabIndex = 9;
            label3.Text = "Servers:";
            // 
            // ClientServerConfigForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(555, 419);
            Controls.Add(label3);
            Controls.Add(ServerListBox);
            Controls.Add(AIPlaneCheckBox);
            Controls.Add(SinglePlayerButton);
            Controls.Add(StartClientButton);
            Controls.Add(label2);
            Controls.Add(label1);
            Controls.Add(IPAddressTextBox);
            Controls.Add(PortTextBox);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            Name = "ClientServerConfigForm";
            Text = "ClientServerConfigForm";
            FormClosing += ClientServerConfigForm_FormClosing;
            Load += ClientServerConfigForm_Load;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private TextBox PortTextBox;
        private TextBox IPAddressTextBox;
        private Label label1;
        private Label label2;
        private Button StartClientButton;
        private Button SinglePlayerButton;
        private CheckBox AIPlaneCheckBox;
        private ListBox ServerListBox;
        private Label label3;
    }
}