namespace PolyPlane
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
            InfoLabel.Location = new Point(76, 69);
            InfoLabel.Name = "InfoLabel";
            InfoLabel.Size = new Size(28, 15);
            InfoLabel.TabIndex = 2;
            InfoLabel.Text = "Info";
            // 
            // ServerUI
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
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
    }
}