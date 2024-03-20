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
            SuspendLayout();
            // 
            // PauseButton
            // 
            PauseButton.Location = new Point(371, 327);
            PauseButton.Name = "PauseButton";
            PauseButton.Size = new Size(75, 23);
            PauseButton.TabIndex = 0;
            PauseButton.Text = "Pause";
            PauseButton.UseVisualStyleBackColor = true;
            PauseButton.Click += PauseButton_Click;
            // 
            // SpawnAIPlaneButton
            // 
            SpawnAIPlaneButton.Location = new Point(352, 356);
            SpawnAIPlaneButton.Name = "SpawnAIPlaneButton";
            SpawnAIPlaneButton.Size = new Size(117, 23);
            SpawnAIPlaneButton.TabIndex = 1;
            SpawnAIPlaneButton.Text = "Spawn AI Plane";
            SpawnAIPlaneButton.UseVisualStyleBackColor = true;
            SpawnAIPlaneButton.Click += SpawnAIPlaneButton_Click;
            // 
            // InfoLabel
            // 
            InfoLabel.AutoSize = true;
            InfoLabel.BorderStyle = BorderStyle.FixedSingle;
            InfoLabel.Location = new Point(36, 26);
            InfoLabel.Name = "InfoLabel";
            InfoLabel.Size = new Size(30, 17);
            InfoLabel.TabIndex = 2;
            InfoLabel.Text = "Info";
            // 
            // InterpCheckBox
            // 
            InterpCheckBox.AutoSize = true;
            InterpCheckBox.Checked = true;
            InterpCheckBox.CheckState = CheckState.Checked;
            InterpCheckBox.Location = new Point(363, 287);
            InterpCheckBox.Name = "InterpCheckBox";
            InterpCheckBox.Size = new Size(76, 19);
            InterpCheckBox.TabIndex = 3;
            InterpCheckBox.Text = "Interp On";
            InterpCheckBox.UseVisualStyleBackColor = true;
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
            StartServerButton.Location = new Point(463, 129);
            StartServerButton.Name = "StartServerButton";
            StartServerButton.Size = new Size(75, 23);
            StartServerButton.TabIndex = 8;
            StartServerButton.Text = "Start Server";
            StartServerButton.UseVisualStyleBackColor = true;
            StartServerButton.Click += StartServerButton_Click;
            // 
            // ServerUI
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(StartServerButton);
            Controls.Add(label2);
            Controls.Add(label1);
            Controls.Add(PortTextBox);
            Controls.Add(AddressTextBox);
            Controls.Add(InterpCheckBox);
            Controls.Add(InfoLabel);
            Controls.Add(SpawnAIPlaneButton);
            Controls.Add(PauseButton);
            Name = "ServerUI";
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
    }
}