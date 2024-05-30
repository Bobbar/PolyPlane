namespace PolyPlane.Server
{
    partial class ServerUI
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
            components = new System.ComponentModel.Container();
            PauseButton = new Button();
            SpawnAIPlaneButton = new Button();
            InfoLabel = new Label();
            InterpCheckBox = new CheckBox();
            AddressTextBox = new TextBox();
            PortTextBox = new TextBox();
            label1 = new Label();
            label2 = new Label();
            StartServerButton = new Button();
            ShowViewPortButton = new Button();
            ServerNameTextBox = new TextBox();
            label3 = new Label();
            RemoveAIPlanesButton = new Button();
            AITypeComboBox = new ComboBox();
            label4 = new Label();
            SpawnRandomAIButton = new Button();
            PlayersListBox = new ListBox();
            PlayerListContextMenu = new ContextMenuStrip(components);
            kickToolStripMenuItem = new ToolStripMenuItem();
            ChatBox = new ListBox();
            label5 = new Label();
            ChatMessageTextBox = new TextBox();
            SentChatButton = new Button();
            label6 = new Label();
            TimeOfDaySlider = new TrackBar();
            TimeOfDayLabel = new Label();
            EnableDiscoveryCheckBox = new CheckBox();
            GunsOnlyCheckBox = new CheckBox();
            DeltaTimeLabel = new Label();
            DeltaTimeNumeric = new NumericUpDown();
            DefaultDTButton = new Button();
            PlayerListContextMenu.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)TimeOfDaySlider).BeginInit();
            ((System.ComponentModel.ISupportInitialize)DeltaTimeNumeric).BeginInit();
            SuspendLayout();
            // 
            // PauseButton
            // 
            PauseButton.Location = new Point(745, 118);
            PauseButton.Name = "PauseButton";
            PauseButton.Size = new Size(75, 23);
            PauseButton.TabIndex = 0;
            PauseButton.Text = "Pause";
            PauseButton.UseVisualStyleBackColor = true;
            PauseButton.Click += PauseButton_Click;
            // 
            // SpawnAIPlaneButton
            // 
            SpawnAIPlaneButton.Location = new Point(726, 147);
            SpawnAIPlaneButton.Name = "SpawnAIPlaneButton";
            SpawnAIPlaneButton.Size = new Size(117, 23);
            SpawnAIPlaneButton.TabIndex = 1;
            SpawnAIPlaneButton.Text = "Spawn AI Plane";
            SpawnAIPlaneButton.UseVisualStyleBackColor = true;
            SpawnAIPlaneButton.Click += SpawnAIPlaneButton_Click;
            // 
            // InfoLabel
            // 
            InfoLabel.BorderStyle = BorderStyle.FixedSingle;
            InfoLabel.Location = new Point(34, 16);
            InfoLabel.Name = "InfoLabel";
            InfoLabel.Size = new Size(190, 306);
            InfoLabel.TabIndex = 2;
            InfoLabel.Text = "Info";
            // 
            // InterpCheckBox
            // 
            InterpCheckBox.AutoSize = true;
            InterpCheckBox.Checked = true;
            InterpCheckBox.CheckState = CheckState.Checked;
            InterpCheckBox.Location = new Point(629, 462);
            InterpCheckBox.Name = "InterpCheckBox";
            InterpCheckBox.Size = new Size(76, 19);
            InterpCheckBox.TabIndex = 3;
            InterpCheckBox.Text = "Interp On";
            InterpCheckBox.UseVisualStyleBackColor = true;
            InterpCheckBox.Visible = false;
            InterpCheckBox.CheckedChanged += InterpCheckBox_CheckedChanged;
            // 
            // AddressTextBox
            // 
            AddressTextBox.Location = new Point(504, 20);
            AddressTextBox.Name = "AddressTextBox";
            AddressTextBox.Size = new Size(100, 23);
            AddressTextBox.TabIndex = 4;
            AddressTextBox.Text = "127.0.0.1";
            // 
            // PortTextBox
            // 
            PortTextBox.Location = new Point(701, 20);
            PortTextBox.Name = "PortTextBox";
            PortTextBox.Size = new Size(100, 23);
            PortTextBox.TabIndex = 5;
            PortTextBox.Text = "1234";
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(446, 23);
            label1.Name = "label1";
            label1.Size = new Size(52, 15);
            label1.TabIndex = 6;
            label1.Text = "Address:";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(663, 23);
            label2.Name = "label2";
            label2.Size = new Size(32, 15);
            label2.TabIndex = 7;
            label2.Text = "Port:";
            // 
            // StartServerButton
            // 
            StartServerButton.BackColor = Color.PaleGreen;
            StartServerButton.Location = new Point(610, 90);
            StartServerButton.Name = "StartServerButton";
            StartServerButton.Size = new Size(85, 37);
            StartServerButton.TabIndex = 8;
            StartServerButton.Text = "Start Server";
            StartServerButton.UseVisualStyleBackColor = false;
            StartServerButton.Click += StartServerButton_Click;
            // 
            // ShowViewPortButton
            // 
            ShowViewPortButton.Location = new Point(770, 501);
            ShowViewPortButton.Name = "ShowViewPortButton";
            ShowViewPortButton.Size = new Size(116, 23);
            ShowViewPortButton.TabIndex = 9;
            ShowViewPortButton.Text = "Spectate";
            ShowViewPortButton.UseVisualStyleBackColor = true;
            ShowViewPortButton.Click += ShowViewPortButton_Click;
            // 
            // ServerNameTextBox
            // 
            ServerNameTextBox.Location = new Point(504, 58);
            ServerNameTextBox.Name = "ServerNameTextBox";
            ServerNameTextBox.Size = new Size(297, 23);
            ServerNameTextBox.TabIndex = 10;
            ServerNameTextBox.Text = "PolyPlane Server";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(421, 61);
            label3.Name = "label3";
            label3.Size = new Size(77, 15);
            label3.TabIndex = 11;
            label3.Text = "Server Name:";
            // 
            // RemoveAIPlanesButton
            // 
            RemoveAIPlanesButton.Location = new Point(726, 258);
            RemoveAIPlanesButton.Name = "RemoveAIPlanesButton";
            RemoveAIPlanesButton.Size = new Size(117, 23);
            RemoveAIPlanesButton.TabIndex = 12;
            RemoveAIPlanesButton.Text = "Remove AI Planes";
            RemoveAIPlanesButton.UseVisualStyleBackColor = true;
            RemoveAIPlanesButton.Click += RemoveAIPlanesButton_Click;
            // 
            // AITypeComboBox
            // 
            AITypeComboBox.FormattingEnabled = true;
            AITypeComboBox.Location = new Point(700, 176);
            AITypeComboBox.Name = "AITypeComboBox";
            AITypeComboBox.Size = new Size(174, 23);
            AITypeComboBox.TabIndex = 13;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(612, 179);
            label4.Name = "label4";
            label4.Size = new Size(82, 15);
            label4.TabIndex = 14;
            label4.Text = "AI Personality:";
            // 
            // SpawnRandomAIButton
            // 
            SpawnRandomAIButton.Location = new Point(726, 229);
            SpawnRandomAIButton.Name = "SpawnRandomAIButton";
            SpawnRandomAIButton.Size = new Size(117, 23);
            SpawnRandomAIButton.TabIndex = 15;
            SpawnRandomAIButton.Text = "Spawn Random AI";
            SpawnRandomAIButton.UseVisualStyleBackColor = true;
            SpawnRandomAIButton.Click += SpawnRandomAIButton_Click;
            // 
            // PlayersListBox
            // 
            PlayersListBox.ContextMenuStrip = PlayerListContextMenu;
            PlayersListBox.FormattingEnabled = true;
            PlayersListBox.ItemHeight = 15;
            PlayersListBox.Location = new Point(250, 108);
            PlayersListBox.Name = "PlayersListBox";
            PlayersListBox.Size = new Size(313, 214);
            PlayersListBox.TabIndex = 16;
            // 
            // PlayerListContextMenu
            // 
            PlayerListContextMenu.Items.AddRange(new ToolStripItem[] { kickToolStripMenuItem });
            PlayerListContextMenu.Name = "PlayerListContextMenu";
            PlayerListContextMenu.Size = new Size(97, 26);
            // 
            // kickToolStripMenuItem
            // 
            kickToolStripMenuItem.Name = "kickToolStripMenuItem";
            kickToolStripMenuItem.Size = new Size(96, 22);
            kickToolStripMenuItem.Text = "Kick";
            kickToolStripMenuItem.Click += kickToolStripMenuItem_Click;
            // 
            // ChatBox
            // 
            ChatBox.FormattingEnabled = true;
            ChatBox.ItemHeight = 15;
            ChatBox.Location = new Point(34, 349);
            ChatBox.Name = "ChatBox";
            ChatBox.Size = new Size(529, 139);
            ChatBox.TabIndex = 17;
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(250, 90);
            label5.Name = "label5";
            label5.Size = new Size(63, 15);
            label5.TabIndex = 18;
            label5.Text = "Player List:";
            // 
            // ChatMessageTextBox
            // 
            ChatMessageTextBox.Location = new Point(34, 494);
            ChatMessageTextBox.Name = "ChatMessageTextBox";
            ChatMessageTextBox.Size = new Size(448, 23);
            ChatMessageTextBox.TabIndex = 19;
            ChatMessageTextBox.KeyPress += ChatMessageTextBox_KeyPress;
            // 
            // SentChatButton
            // 
            SentChatButton.Location = new Point(488, 493);
            SentChatButton.Name = "SentChatButton";
            SentChatButton.Size = new Size(75, 23);
            SentChatButton.TabIndex = 20;
            SentChatButton.Text = "Send";
            SentChatButton.UseVisualStyleBackColor = true;
            SentChatButton.Click += SentChatButton_Click;
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new Point(34, 331);
            label6.Name = "label6";
            label6.Size = new Size(95, 15);
            label6.TabIndex = 21;
            label6.Text = "Chat and Events:";
            // 
            // TimeOfDaySlider
            // 
            TimeOfDaySlider.Location = new Point(662, 312);
            TimeOfDaySlider.Name = "TimeOfDaySlider";
            TimeOfDaySlider.Size = new Size(212, 45);
            TimeOfDaySlider.TabIndex = 22;
            TimeOfDaySlider.Scroll += TimeOfDaySlider_Scroll;
            TimeOfDaySlider.ValueChanged += TimeOfDaySlider_ValueChanged;
            // 
            // TimeOfDayLabel
            // 
            TimeOfDayLabel.AutoSize = true;
            TimeOfDayLabel.Location = new Point(663, 294);
            TimeOfDayLabel.Name = "TimeOfDayLabel";
            TimeOfDayLabel.Size = new Size(72, 15);
            TimeOfDayLabel.TabIndex = 23;
            TimeOfDayLabel.Text = "Time of day:";
            // 
            // EnableDiscoveryCheckBox
            // 
            EnableDiscoveryCheckBox.AutoSize = true;
            EnableDiscoveryCheckBox.Checked = true;
            EnableDiscoveryCheckBox.CheckState = CheckState.Checked;
            EnableDiscoveryCheckBox.Location = new Point(286, 36);
            EnableDiscoveryCheckBox.Name = "EnableDiscoveryCheckBox";
            EnableDiscoveryCheckBox.Size = new Size(115, 19);
            EnableDiscoveryCheckBox.TabIndex = 24;
            EnableDiscoveryCheckBox.Text = "Enable Discovery";
            EnableDiscoveryCheckBox.UseVisualStyleBackColor = true;
            EnableDiscoveryCheckBox.CheckedChanged += EnableDiscoveryCheckBox_CheckedChanged;
            // 
            // GunsOnlyCheckBox
            // 
            GunsOnlyCheckBox.AutoSize = true;
            GunsOnlyCheckBox.Location = new Point(629, 487);
            GunsOnlyCheckBox.Name = "GunsOnlyCheckBox";
            GunsOnlyCheckBox.Size = new Size(81, 19);
            GunsOnlyCheckBox.TabIndex = 25;
            GunsOnlyCheckBox.Text = "Guns Only";
            GunsOnlyCheckBox.UseVisualStyleBackColor = true;
            GunsOnlyCheckBox.CheckedChanged += GunsOnlyCheckBox_CheckedChanged;
            // 
            // DeltaTimeLabel
            // 
            DeltaTimeLabel.AutoSize = true;
            DeltaTimeLabel.Location = new Point(663, 370);
            DeltaTimeLabel.Name = "DeltaTimeLabel";
            DeltaTimeLabel.Size = new Size(64, 15);
            DeltaTimeLabel.TabIndex = 27;
            DeltaTimeLabel.Text = "Delta time:";
            // 
            // DeltaTimeNumeric
            // 
            DeltaTimeNumeric.DecimalPlaces = 4;
            DeltaTimeNumeric.Increment = new decimal(new int[] { 1, 0, 0, 196608 });
            DeltaTimeNumeric.Location = new Point(733, 368);
            DeltaTimeNumeric.Maximum = new decimal(new int[] { 1, 0, 0, 0 });
            DeltaTimeNumeric.Minimum = new decimal(new int[] { 1, 0, 0, 196608 });
            DeltaTimeNumeric.Name = "DeltaTimeNumeric";
            DeltaTimeNumeric.Size = new Size(68, 23);
            DeltaTimeNumeric.TabIndex = 28;
            DeltaTimeNumeric.Value = new decimal(new int[] { 1, 0, 0, 196608 });
            DeltaTimeNumeric.ValueChanged += DeltaTimeNumeric_ValueChanged;
            // 
            // DefaultDTButton
            // 
            DefaultDTButton.Location = new Point(733, 397);
            DefaultDTButton.Name = "DefaultDTButton";
            DefaultDTButton.Size = new Size(68, 23);
            DefaultDTButton.TabIndex = 29;
            DefaultDTButton.Text = "Default";
            DefaultDTButton.UseVisualStyleBackColor = true;
            DefaultDTButton.Click += DefaultDTButton_Click;
            // 
            // ServerUI
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(898, 536);
            Controls.Add(DefaultDTButton);
            Controls.Add(DeltaTimeNumeric);
            Controls.Add(DeltaTimeLabel);
            Controls.Add(GunsOnlyCheckBox);
            Controls.Add(EnableDiscoveryCheckBox);
            Controls.Add(TimeOfDayLabel);
            Controls.Add(TimeOfDaySlider);
            Controls.Add(label6);
            Controls.Add(SentChatButton);
            Controls.Add(ChatMessageTextBox);
            Controls.Add(label5);
            Controls.Add(ChatBox);
            Controls.Add(PlayersListBox);
            Controls.Add(SpawnRandomAIButton);
            Controls.Add(label4);
            Controls.Add(AITypeComboBox);
            Controls.Add(RemoveAIPlanesButton);
            Controls.Add(label3);
            Controls.Add(ServerNameTextBox);
            Controls.Add(ShowViewPortButton);
            Controls.Add(StartServerButton);
            Controls.Add(label2);
            Controls.Add(label1);
            Controls.Add(PortTextBox);
            Controls.Add(AddressTextBox);
            Controls.Add(InterpCheckBox);
            Controls.Add(InfoLabel);
            Controls.Add(SpawnAIPlaneButton);
            Controls.Add(PauseButton);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            Name = "ServerUI";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "ServerUI";
            PlayerListContextMenu.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)TimeOfDaySlider).EndInit();
            ((System.ComponentModel.ISupportInitialize)DeltaTimeNumeric).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button PauseButton;
        private Button SpawnAIPlaneButton;
        private Label InfoLabel;
        private CheckBox InterpCheckBox;
        private TextBox AddressTextBox;
        private TextBox PortTextBox;
        private Label label1;
        private Label label2;
        private Button StartServerButton;
        private Button ShowViewPortButton;
        private TextBox ServerNameTextBox;
        private Label label3;
        private Button RemoveAIPlanesButton;
        private ComboBox AITypeComboBox;
        private Label label4;
        private Button SpawnRandomAIButton;
        private ListBox PlayersListBox;
        private ContextMenuStrip PlayerListContextMenu;
        private ToolStripMenuItem kickToolStripMenuItem;
        private ListBox ChatBox;
        private Label label5;
        private TextBox ChatMessageTextBox;
        private Button SentChatButton;
        private Label label6;
        private TrackBar TimeOfDaySlider;
        private Label TimeOfDayLabel;
        private CheckBox EnableDiscoveryCheckBox;
        private CheckBox GunsOnlyCheckBox;
        private Label DeltaTimeLabel;
        private NumericUpDown DeltaTimeNumeric;
        private Button DefaultDTButton;
    }
}