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
            SuspendLayout();
            // 
            // PauseButton
            // 
            PauseButton.Location = new Point(364, 256);
            PauseButton.Name = "PauseButton";
            PauseButton.Size = new Size(75, 23);
            PauseButton.TabIndex = 0;
            PauseButton.Text = "Pause";
            PauseButton.UseVisualStyleBackColor = true;
            PauseButton.Click += PauseButton_Click;
            // 
            // SpawnAIPlaneButton
            // 
            SpawnAIPlaneButton.Location = new Point(345, 285);
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
            InfoLabel.Location = new Point(36, 26);
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
            InterpCheckBox.Location = new Point(363, 231);
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
            AddressTextBox.Location = new Point(362, 75);
            AddressTextBox.Name = "AddressTextBox";
            AddressTextBox.Size = new Size(100, 23);
            AddressTextBox.TabIndex = 4;
            AddressTextBox.Text = "127.0.0.1";
            // 
            // PortTextBox
            // 
            PortTextBox.Location = new Point(559, 75);
            PortTextBox.Name = "PortTextBox";
            PortTextBox.Size = new Size(100, 23);
            PortTextBox.TabIndex = 5;
            PortTextBox.Text = "1234";
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(304, 78);
            label1.Name = "label1";
            label1.Size = new Size(52, 15);
            label1.TabIndex = 6;
            label1.Text = "Address:";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(521, 78);
            label2.Name = "label2";
            label2.Size = new Size(32, 15);
            label2.TabIndex = 7;
            label2.Text = "Port:";
            // 
            // StartServerButton
            // 
            StartServerButton.Location = new Point(468, 151);
            StartServerButton.Name = "StartServerButton";
            StartServerButton.Size = new Size(75, 23);
            StartServerButton.TabIndex = 8;
            StartServerButton.Text = "Start Server";
            StartServerButton.UseVisualStyleBackColor = true;
            StartServerButton.Click += StartServerButton_Click;
            // 
            // ShowViewPortButton
            // 
            ShowViewPortButton.Location = new Point(672, 415);
            ShowViewPortButton.Name = "ShowViewPortButton";
            ShowViewPortButton.Size = new Size(116, 23);
            ShowViewPortButton.TabIndex = 9;
            ShowViewPortButton.Text = "Show View Port";
            ShowViewPortButton.UseVisualStyleBackColor = true;
            ShowViewPortButton.Click += ShowViewPortButton_Click;
            // 
            // ServerNameTextBox
            // 
            ServerNameTextBox.Location = new Point(362, 113);
            ServerNameTextBox.Name = "ServerNameTextBox";
            ServerNameTextBox.Size = new Size(297, 23);
            ServerNameTextBox.TabIndex = 10;
            ServerNameTextBox.Text = "PolyPlane Server";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(279, 116);
            label3.Name = "label3";
            label3.Size = new Size(77, 15);
            label3.TabIndex = 11;
            label3.Text = "Server Name:";
            // 
            // RemoveAIPlanesButton
            // 
            RemoveAIPlanesButton.Location = new Point(345, 386);
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
            AITypeComboBox.Location = new Point(319, 314);
            AITypeComboBox.Name = "AITypeComboBox";
            AITypeComboBox.Size = new Size(174, 23);
            AITypeComboBox.TabIndex = 13;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(231, 317);
            label4.Name = "label4";
            label4.Size = new Size(82, 15);
            label4.TabIndex = 14;
            label4.Text = "AI Personality:";
            // 
            // SpawnRandomAIButton
            // 
            SpawnRandomAIButton.Location = new Point(345, 357);
            SpawnRandomAIButton.Name = "SpawnRandomAIButton";
            SpawnRandomAIButton.Size = new Size(117, 23);
            SpawnRandomAIButton.TabIndex = 15;
            SpawnRandomAIButton.Text = "Spawn Random AI";
            SpawnRandomAIButton.UseVisualStyleBackColor = true;
            SpawnRandomAIButton.Click += SpawnRandomAIButton_Click;
            // 
            // ServerUI
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
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
    }
}