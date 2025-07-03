namespace PolyPlane
{
    partial class PolyPlaneUI
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PolyPlaneUI));
            RenderTarget = new PictureBox();
            ((System.ComponentModel.ISupportInitialize)RenderTarget).BeginInit();
            SuspendLayout();
            // 
            // RenderTarget
            // 
            RenderTarget.Dock = DockStyle.Fill;
            RenderTarget.Location = new Point(0, 0);
            RenderTarget.Name = "RenderTarget";
            RenderTarget.Size = new Size(1350, 839);
            RenderTarget.TabIndex = 0;
            RenderTarget.TabStop = false;
            RenderTarget.MouseDown += RenderTarget_MouseDown;
            RenderTarget.MouseMove += RenderTarget_MouseMove;
            // 
            // PolyPlaneUI
            // 
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(1350, 839);
            Controls.Add(RenderTarget);
            Cursor = Cursors.Cross;
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "PolyPlaneUI";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "PolyPlane - 2D Flight Physics";
            FormClosing += PolyPlaneUI_FormClosing;
            Shown += PolyPlaneUI_Shown;
            KeyDown += PolyPlaneUI_KeyDown;
            KeyPress += PolyPlaneUI_KeyPress;
            KeyUp += PolyPlaneUI_KeyUp;
            ((System.ComponentModel.ISupportInitialize)RenderTarget).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private PictureBox RenderTarget;
    }
}