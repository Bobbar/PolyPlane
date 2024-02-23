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
            SuspendLayout();
            // 
            // PolyPlaneUI
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1330, 775);
            Name = "PolyPlaneUI";
            Text = "PolyPlane - 2D Flight Physics";
            FormClosing += PolyPlaneUI_FormClosing;
            KeyDown += PolyPlaneUI_KeyDown;
            KeyPress += PolyPlaneUI_KeyPress;
            KeyUp += PolyPlaneUI_KeyUp;
            MouseDown += PolyPlaneUI_MouseDown;
            MouseMove += PolyPlaneUI_MouseMove;
            MouseUp += PolyPlaneUI_MouseUp;
            ResumeLayout(false);
        }

        #endregion
    }
}