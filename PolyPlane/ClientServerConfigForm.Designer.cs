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
            ChooseColorButton = new Button();
            PlaneColorDialog = new ColorDialog();
            RandomColorButton = new Button();
            label5 = new Label();
            HudColorPreviewLabel = new Label();
            HudColorAlphaNumeric = new NumericUpDown();
            label6 = new Label();
            DefaultHubColorButton = new Button();
            label7 = new Label();
            TargetFPSTextBox = new TextBox();
            SpawnDistTextBox = new TextBox();
            label8 = new Label();
            ((System.ComponentModel.ISupportInitialize)PlanePreviewBox).BeginInit();
            ((System.ComponentModel.ISupportInitialize)HudColorAlphaNumeric).BeginInit();
            SuspendLayout();
            // 
            // PortTextBox
            // 
            PortTextBox.Location = new Point(573, 114);
            PortTextBox.Margin = new Padding(5, 6, 5, 6);
            PortTextBox.Name = "PortTextBox";
            PortTextBox.Size = new Size(95, 35);
            PortTextBox.TabIndex = 0;
            PortTextBox.Text = "1234";
            // 
            // IPAddressTextBox
            // 
            IPAddressTextBox.Location = new Point(257, 114);
            IPAddressTextBox.Margin = new Padding(5, 6, 5, 6);
            IPAddressTextBox.Name = "IPAddressTextBox";
            IPAddressTextBox.Size = new Size(169, 35);
            IPAddressTextBox.TabIndex = 1;
            IPAddressTextBox.Text = "SELECT A SERVER";
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(213, 120);
            label1.Margin = new Padding(5, 0, 5, 0);
            label1.Name = "label1";
            label1.Size = new Size(36, 30);
            label1.TabIndex = 2;
            label1.Text = "IP:";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(507, 120);
            label2.Margin = new Padding(5, 0, 5, 0);
            label2.Name = "label2";
            label2.Size = new Size(55, 30);
            label2.TabIndex = 3;
            label2.Text = "Port:";
            // 
            // StartClientButton
            // 
            StartClientButton.Location = new Point(434, 354);
            StartClientButton.Margin = new Padding(5, 6, 5, 6);
            StartClientButton.Name = "StartClientButton";
            StartClientButton.Size = new Size(129, 46);
            StartClientButton.TabIndex = 5;
            StartClientButton.Text = "Connect";
            StartClientButton.UseVisualStyleBackColor = true;
            StartClientButton.Click += StartClientButton_Click;
            // 
            // SinglePlayerButton
            // 
            SinglePlayerButton.Location = new Point(21, 768);
            SinglePlayerButton.Margin = new Padding(5, 6, 5, 6);
            SinglePlayerButton.Name = "SinglePlayerButton";
            SinglePlayerButton.Size = new Size(183, 46);
            SinglePlayerButton.TabIndex = 6;
            SinglePlayerButton.Text = "Start Local Game";
            SinglePlayerButton.UseVisualStyleBackColor = true;
            SinglePlayerButton.Click += SinglePlayerButton_Click;
            // 
            // AIPlaneCheckBox
            // 
            AIPlaneCheckBox.AutoSize = true;
            AIPlaneCheckBox.Location = new Point(434, 304);
            AIPlaneCheckBox.Margin = new Padding(5, 6, 5, 6);
            AIPlaneCheckBox.Name = "AIPlaneCheckBox";
            AIPlaneCheckBox.Size = new Size(116, 34);
            AIPlaneCheckBox.TabIndex = 7;
            AIPlaneCheckBox.Text = "AI Plane";
            AIPlaneCheckBox.UseVisualStyleBackColor = true;
            AIPlaneCheckBox.CheckedChanged += AIPlaneCheckBox_CheckedChanged;
            // 
            // ServerListBox
            // 
            ServerListBox.FormattingEnabled = true;
            ServerListBox.Location = new Point(257, 484);
            ServerListBox.Margin = new Padding(5, 6, 5, 6);
            ServerListBox.Name = "ServerListBox";
            ServerListBox.Size = new Size(503, 304);
            ServerListBox.TabIndex = 8;
            ServerListBox.SelectedValueChanged += ServerListBox_SelectedValueChanged;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(257, 448);
            label3.Margin = new Padding(5, 0, 5, 0);
            label3.Name = "label3";
            label3.Size = new Size(84, 30);
            label3.TabIndex = 9;
            label3.Text = "Servers:";
            // 
            // ErrorLabel
            // 
            ErrorLabel.BackColor = Color.Silver;
            ErrorLabel.ForeColor = Color.Maroon;
            ErrorLabel.Location = new Point(0, 36);
            ErrorLabel.Margin = new Padding(5, 0, 5, 0);
            ErrorLabel.Name = "ErrorLabel";
            ErrorLabel.Size = new Size(1323, 46);
            ErrorLabel.TabIndex = 10;
            ErrorLabel.TextAlign = ContentAlignment.MiddleCenter;
            ErrorLabel.Visible = false;
            // 
            // ExitButton
            // 
            ExitButton.BackColor = Color.FromArgb(192, 0, 0);
            ExitButton.Font = new Font("Segoe UI Black", 15.8571434F, FontStyle.Bold, GraphicsUnit.Point, 0);
            ExitButton.ForeColor = Color.WhiteSmoke;
            ExitButton.Location = new Point(1139, 732);
            ExitButton.Margin = new Padding(5, 6, 5, 6);
            ExitButton.Name = "ExitButton";
            ExitButton.Size = new Size(149, 82);
            ExitButton.TabIndex = 11;
            ExitButton.Text = "QUIT";
            ExitButton.UseVisualStyleBackColor = false;
            ExitButton.Click += ExitButton_Click;
            // 
            // PlayerNameTextBox
            // 
            PlayerNameTextBox.Location = new Point(394, 222);
            PlayerNameTextBox.Margin = new Padding(5, 6, 5, 6);
            PlayerNameTextBox.MaxLength = 15;
            PlayerNameTextBox.Name = "PlayerNameTextBox";
            PlayerNameTextBox.Size = new Size(256, 35);
            PlayerNameTextBox.TabIndex = 12;
            PlayerNameTextBox.Text = "Player";
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(252, 228);
            label4.Margin = new Padding(5, 0, 5, 0);
            label4.Name = "label4";
            label4.Size = new Size(136, 30);
            label4.TabIndex = 13;
            label4.Text = "Player Name:";
            // 
            // PlanePreviewBox
            // 
            PlanePreviewBox.BorderStyle = BorderStyle.FixedSingle;
            PlanePreviewBox.Location = new Point(861, 108);
            PlanePreviewBox.Margin = new Padding(5, 6, 5, 6);
            PlanePreviewBox.Name = "PlanePreviewBox";
            PlanePreviewBox.Size = new Size(341, 398);
            PlanePreviewBox.TabIndex = 14;
            PlanePreviewBox.TabStop = false;
            // 
            // ChooseColorButton
            // 
            ChooseColorButton.Location = new Point(1075, 520);
            ChooseColorButton.Margin = new Padding(5, 6, 5, 6);
            ChooseColorButton.Name = "ChooseColorButton";
            ChooseColorButton.Size = new Size(129, 80);
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
            RandomColorButton.Location = new Point(861, 520);
            RandomColorButton.Margin = new Padding(5, 6, 5, 6);
            RandomColorButton.Name = "RandomColorButton";
            RandomColorButton.Size = new Size(129, 80);
            RandomColorButton.TabIndex = 16;
            RandomColorButton.Text = "Random Color";
            RandomColorButton.UseVisualStyleBackColor = true;
            RandomColorButton.Click += RandomColorButton_Click;
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(883, 618);
            label5.Margin = new Padding(5, 0, 5, 0);
            label5.Name = "label5";
            label5.Size = new Size(118, 30);
            label5.TabIndex = 17;
            label5.Text = "Hud Color: ";
            // 
            // HudColorPreviewLabel
            // 
            HudColorPreviewLabel.BorderStyle = BorderStyle.FixedSingle;
            HudColorPreviewLabel.Cursor = Cursors.Hand;
            HudColorPreviewLabel.Location = new Point(1015, 608);
            HudColorPreviewLabel.Margin = new Padding(5, 0, 5, 0);
            HudColorPreviewLabel.Name = "HudColorPreviewLabel";
            HudColorPreviewLabel.Size = new Size(167, 48);
            HudColorPreviewLabel.TabIndex = 18;
            HudColorPreviewLabel.Click += HudColorPreviewLabel_Click;
            // 
            // HudColorAlphaNumeric
            // 
            HudColorAlphaNumeric.DecimalPlaces = 1;
            HudColorAlphaNumeric.Increment = new decimal(new int[] { 5, 0, 0, 131072 });
            HudColorAlphaNumeric.Location = new Point(1015, 664);
            HudColorAlphaNumeric.Margin = new Padding(5, 6, 5, 6);
            HudColorAlphaNumeric.Maximum = new decimal(new int[] { 1, 0, 0, 0 });
            HudColorAlphaNumeric.Name = "HudColorAlphaNumeric";
            HudColorAlphaNumeric.Size = new Size(77, 35);
            HudColorAlphaNumeric.TabIndex = 19;
            HudColorAlphaNumeric.ValueChanged += HudColorAlphaNumeric_ValueChanged;
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new Point(883, 668);
            label6.Margin = new Padding(5, 0, 5, 0);
            label6.Name = "label6";
            label6.Size = new Size(78, 30);
            label6.TabIndex = 20;
            label6.Text = "Alpha: ";
            // 
            // DefaultHubColorButton
            // 
            DefaultHubColorButton.Location = new Point(1198, 612);
            DefaultHubColorButton.Margin = new Padding(5, 6, 5, 6);
            DefaultHubColorButton.Name = "DefaultHubColorButton";
            DefaultHubColorButton.Size = new Size(93, 46);
            DefaultHubColorButton.TabIndex = 21;
            DefaultHubColorButton.Text = "Default";
            DefaultHubColorButton.UseVisualStyleBackColor = true;
            DefaultHubColorButton.Click += DefaultHubColorButton_Click;
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Location = new Point(883, 718);
            label7.Margin = new Padding(5, 0, 5, 0);
            label7.Name = "label7";
            label7.Size = new Size(114, 30);
            label7.TabIndex = 22;
            label7.Text = "Target FPS:";
            // 
            // TargetFPSTextBox
            // 
            TargetFPSTextBox.Location = new Point(1011, 712);
            TargetFPSTextBox.Margin = new Padding(5, 6, 5, 6);
            TargetFPSTextBox.Name = "TargetFPSTextBox";
            TargetFPSTextBox.Size = new Size(78, 35);
            TargetFPSTextBox.TabIndex = 23;
            TargetFPSTextBox.Text = "60";
            TargetFPSTextBox.TextAlign = HorizontalAlignment.Center;
            TargetFPSTextBox.Validating += TargetFPSTextBox_Validating;
            // 
            // SpawnDistTextBox
            // 
            SpawnDistTextBox.Location = new Point(21, 702);
            SpawnDistTextBox.Margin = new Padding(5, 6, 5, 6);
            SpawnDistTextBox.Name = "SpawnDistTextBox";
            SpawnDistTextBox.Size = new Size(169, 35);
            SpawnDistTextBox.TabIndex = 24;
            SpawnDistTextBox.Validating += SpawnDistTextBox_Validating;
            // 
            // label8
            // 
            label8.AutoSize = true;
            label8.Location = new Point(21, 668);
            label8.Margin = new Padding(5, 0, 5, 0);
            label8.Name = "label8";
            label8.Size = new Size(165, 30);
            label8.TabIndex = 25;
            label8.Text = "Spawn Distance:";
            // 
            // ClientServerConfigForm
            // 
            AutoScaleDimensions = new SizeF(12F, 30F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1323, 838);
            Controls.Add(label8);
            Controls.Add(SpawnDistTextBox);
            Controls.Add(TargetFPSTextBox);
            Controls.Add(label7);
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
            Margin = new Padding(5, 6, 5, 6);
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
        private Label label7;
        private TextBox TargetFPSTextBox;
        private TextBox SpawnDistTextBox;
        private Label label8;
    }
}