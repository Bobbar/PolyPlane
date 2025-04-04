﻿namespace PolyPlane
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
            ChooseColorButton = new Button();
            PlaneColorDialog = new ColorDialog();
            RandomColorButton = new Button();
            label5 = new Label();
            HudColorPreviewLabel = new Label();
            HudColorAlphaNumeric = new NumericUpDown();
            label6 = new Label();
            DefaultHubColorButton = new Button();
            ((System.ComponentModel.ISupportInitialize)PlanePreviewBox).BeginInit();
            ((System.ComponentModel.ISupportInitialize)HudColorAlphaNumeric).BeginInit();
            SuspendLayout();
            // 
            // PortTextBox
            // 
            PortTextBox.Location = new Point(334, 57);
            PortTextBox.Name = "PortTextBox";
            PortTextBox.Size = new Size(57, 23);
            PortTextBox.TabIndex = 0;
            PortTextBox.Text = "1234";
            // 
            // IPAddressTextBox
            // 
            IPAddressTextBox.Location = new Point(150, 57);
            IPAddressTextBox.Name = "IPAddressTextBox";
            IPAddressTextBox.Size = new Size(100, 23);
            IPAddressTextBox.TabIndex = 1;
            IPAddressTextBox.Text = "SELECT A SERVER";
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(124, 60);
            label1.Name = "label1";
            label1.Size = new Size(20, 15);
            label1.TabIndex = 2;
            label1.Text = "IP:";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(296, 60);
            label2.Name = "label2";
            label2.Size = new Size(32, 15);
            label2.TabIndex = 3;
            label2.Text = "Port:";
            // 
            // StartClientButton
            // 
            StartClientButton.Location = new Point(253, 177);
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
            SinglePlayerButton.Size = new Size(107, 23);
            SinglePlayerButton.TabIndex = 6;
            SinglePlayerButton.Text = "Start Local Game";
            SinglePlayerButton.UseVisualStyleBackColor = true;
            SinglePlayerButton.Click += SinglePlayerButton_Click;
            // 
            // AIPlaneCheckBox
            // 
            AIPlaneCheckBox.AutoSize = true;
            AIPlaneCheckBox.Location = new Point(253, 152);
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
            ServerListBox.Location = new Point(150, 242);
            ServerListBox.Name = "ServerListBox";
            ServerListBox.Size = new Size(295, 154);
            ServerListBox.TabIndex = 8;
            ServerListBox.SelectedValueChanged += ServerListBox_SelectedValueChanged;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(150, 224);
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
            PlayerNameTextBox.Location = new Point(230, 111);
            PlayerNameTextBox.MaxLength = 15;
            PlayerNameTextBox.Name = "PlayerNameTextBox";
            PlayerNameTextBox.Size = new Size(151, 23);
            PlayerNameTextBox.TabIndex = 12;
            PlayerNameTextBox.Text = "Player";
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(147, 114);
            label4.Name = "label4";
            label4.Size = new Size(77, 15);
            label4.TabIndex = 13;
            label4.Text = "Player Name:";
            // 
            // PlanePreviewBox
            // 
            PlanePreviewBox.BorderStyle = BorderStyle.FixedSingle;
            PlanePreviewBox.Location = new Point(502, 54);
            PlanePreviewBox.Name = "PlanePreviewBox";
            PlanePreviewBox.Size = new Size(200, 200);
            PlanePreviewBox.TabIndex = 14;
            PlanePreviewBox.TabStop = false;
            // 
            // ChooseColorButton
            // 
            ChooseColorButton.Location = new Point(627, 260);
            ChooseColorButton.Name = "ChooseColorButton";
            ChooseColorButton.Size = new Size(75, 40);
            ChooseColorButton.TabIndex = 15;
            ChooseColorButton.Text = "Choose Color";
            ChooseColorButton.UseVisualStyleBackColor = true;
            ChooseColorButton.Click += NewColorButton_Click;
            // 
            // PlaneColorDialog
            // 
            PlaneColorDialog.SolidColorOnly = true;
            // 
            // RandomColorButton
            // 
            RandomColorButton.Location = new Point(502, 260);
            RandomColorButton.Name = "RandomColorButton";
            RandomColorButton.Size = new Size(75, 40);
            RandomColorButton.TabIndex = 16;
            RandomColorButton.Text = "Random Color";
            RandomColorButton.UseVisualStyleBackColor = true;
            RandomColorButton.Click += RandomColorButton_Click;
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(513, 318);
            label5.Name = "label5";
            label5.Size = new Size(68, 15);
            label5.TabIndex = 17;
            label5.Text = "Hud Color: ";
            // 
            // HudColorPreviewLabel
            // 
            HudColorPreviewLabel.BorderStyle = BorderStyle.FixedSingle;
            HudColorPreviewLabel.Cursor = Cursors.Hand;
            HudColorPreviewLabel.Location = new Point(590, 313);
            HudColorPreviewLabel.Name = "HudColorPreviewLabel";
            HudColorPreviewLabel.Size = new Size(98, 25);
            HudColorPreviewLabel.TabIndex = 18;
            HudColorPreviewLabel.Click += HudColorPreviewLabel_Click;
            // 
            // HudColorAlphaNumeric
            // 
            HudColorAlphaNumeric.DecimalPlaces = 1;
            HudColorAlphaNumeric.Increment = new decimal(new int[] { 5, 0, 0, 131072 });
            HudColorAlphaNumeric.Location = new Point(590, 341);
            HudColorAlphaNumeric.Maximum = new decimal(new int[] { 1, 0, 0, 0 });
            HudColorAlphaNumeric.Name = "HudColorAlphaNumeric";
            HudColorAlphaNumeric.Size = new Size(45, 23);
            HudColorAlphaNumeric.TabIndex = 19;
            HudColorAlphaNumeric.ValueChanged += HudColorAlphaNumeric_ValueChanged;
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new Point(513, 341);
            label6.Name = "label6";
            label6.Size = new Size(44, 15);
            label6.TabIndex = 20;
            label6.Text = "Alpha: ";
            // 
            // DefaultHubColorButton
            // 
            DefaultHubColorButton.Location = new Point(697, 315);
            DefaultHubColorButton.Name = "DefaultHubColorButton";
            DefaultHubColorButton.Size = new Size(54, 23);
            DefaultHubColorButton.TabIndex = 21;
            DefaultHubColorButton.Text = "Default";
            DefaultHubColorButton.UseVisualStyleBackColor = true;
            DefaultHubColorButton.Click += DefaultHubColorButton_Click;
            // 
            // ClientServerConfigForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(772, 419);
            Controls.Add(DefaultHubColorButton);
            Controls.Add(label6);
            Controls.Add(HudColorAlphaNumeric);
            Controls.Add(HudColorPreviewLabel);
            Controls.Add(label5);
            Controls.Add(RandomColorButton);
            Controls.Add(ChooseColorButton);
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
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "ClientServerConfigForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Join Server";
            FormClosing += ClientServerConfigForm_FormClosing;
            Load += ClientServerConfigForm_Load;
            ((System.ComponentModel.ISupportInitialize)PlanePreviewBox).EndInit();
            ((System.ComponentModel.ISupportInitialize)HudColorAlphaNumeric).EndInit();
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
        private Button ChooseColorButton;
        private ColorDialog PlaneColorDialog;
        private Button RandomColorButton;
        private Label label5;
        private Label HudColorPreviewLabel;
        private NumericUpDown HudColorAlphaNumeric;
        private Label label6;
        private Button DefaultHubColorButton;
    }
}