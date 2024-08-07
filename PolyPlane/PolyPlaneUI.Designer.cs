﻿namespace PolyPlane
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
            SuspendLayout();
            // 
            // PolyPlaneUI
            // 
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(1008, 729);
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
            MouseDown += PolyPlaneUI_MouseDown;
            MouseMove += PolyPlaneUI_MouseMove;
            ResumeLayout(false);
        }

        #endregion
    }
}