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
            ErrorLabel = new Label();
            ExitButton = new Button();
            PlayerNameTextBox = new TextBox();
            label4 = new Label();
            PlanePreviewBox = new PictureBox();
            NewColorButton = new Button();
            ((System.ComponentModel.ISupportInitialize)PlanePreviewBox).BeginInit();
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
            IPAddressTextBox.Text = "SELECT A SERVER";
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
            StartClientButton.Location = new Point(228, 170);
            StartClientButton.Name = "StartClientButton";
            StartClientButton.Size = new Size(75, 23);
            StartClientButton.TabIndex = 5;
            StartClientButton.Text = "Connect";
            StartClientButton.UseVisualStyleBackColor = true;
            StartClientButton.Click += StartClientButton_Click;
            // 
            // SinglePlayerButton
            // 
            SinglePlayerButton.Location = new Point(12, 384);
            SinglePlayerButton.Name = "SinglePlayerButton";
            SinglePlayerButton.Size = new Size(75, 23);
            SinglePlayerButton.TabIndex = 6;
            SinglePlayerButton.Text = "Solo";
            SinglePlayerButton.UseVisualStyleBackColor = true;
            SinglePlayerButton.Click += SinglePlayerButton_Click;
            // 
            // AIPlaneCheckBox
            // 
            AIPlaneCheckBox.AutoSize = true;
            AIPlaneCheckBox.Location = new Point(228, 145);
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
            // ErrorLabel
            // 
            ErrorLabel.BackColor = Color.Silver;
            ErrorLabel.ForeColor = Color.Maroon;
            ErrorLabel.Location = new Point(0, 18);
            ErrorLabel.Name = "ErrorLabel";
            ErrorLabel.Size = new Size(772, 23);
            ErrorLabel.TabIndex = 10;
            ErrorLabel.TextAlign = ContentAlignment.MiddleCenter;
            ErrorLabel.Visible = false;
            // 
            // ExitButton
            // 
            ExitButton.Location = new Point(676, 384);
            ExitButton.Name = "ExitButton";
            ExitButton.Size = new Size(75, 23);
            ExitButton.TabIndex = 11;
            ExitButton.Text = "Exit";
            ExitButton.UseVisualStyleBackColor = true;
            ExitButton.Click += ExitButton_Click;
            // 
            // PlayerNameTextBox
            // 
            PlayerNameTextBox.Location = new Point(205, 104);
            PlayerNameTextBox.MaxLength = 15;
            PlayerNameTextBox.Name = "PlayerNameTextBox";
            PlayerNameTextBox.Size = new Size(151, 23);
            PlayerNameTextBox.TabIndex = 12;
            PlayerNameTextBox.Text = "Player";
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(122, 107);
            label4.Name = "label4";
            label4.Size = new Size(77, 15);
            label4.TabIndex = 13;
            label4.Text = "Player Name:";
            // 
            // PlanePreviewBox
            // 
            PlanePreviewBox.BorderStyle = BorderStyle.FixedSingle;
            PlanePreviewBox.Location = new Point(487, 89);
            PlanePreviewBox.Name = "PlanePreviewBox";
            PlanePreviewBox.Size = new Size(200, 200);
            PlanePreviewBox.TabIndex = 14;
            PlanePreviewBox.TabStop = false;
            // 
            // NewColorButton
            // 
            NewColorButton.Location = new Point(555, 310);
            NewColorButton.Name = "NewColorButton";
            NewColorButton.Size = new Size(75, 23);
            NewColorButton.TabIndex = 15;
            NewColorButton.Text = "Next Color";
            NewColorButton.UseVisualStyleBackColor = true;
            NewColorButton.Click += NewColorButton_Click;
            // 
            // ClientServerConfigForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(772, 419);
            Controls.Add(NewColorButton);
            Controls.Add(PlanePreviewBox);
            Controls.Add(label4);
            Controls.Add(PlayerNameTextBox);
            Controls.Add(ExitButton);
            Controls.Add(ErrorLabel);
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
            StartPosition = FormStartPosition.CenterParent;
            Text = "Join Server";
            FormClosing += ClientServerConfigForm_FormClosing;
            Load += ClientServerConfigForm_Load;
            ((System.ComponentModel.ISupportInitialize)PlanePreviewBox).EndInit();
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
        private Label ErrorLabel;
        private Button ExitButton;
        private TextBox PlayerNameTextBox;
        private Label label4;
        private PictureBox PlanePreviewBox;
        private Button NewColorButton;
    }
}