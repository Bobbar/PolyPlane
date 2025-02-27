﻿namespace PolyPlane
{
    partial class SelectObjectUI
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
            ObjectTypeCombo = new ComboBox();
            ObjectsListbox = new ListBox();
            OKButton = new Button();
            RefreshButton = new Button();
            SuspendLayout();
            // 
            // ObjectTypeCombo
            // 
            ObjectTypeCombo.FormattingEnabled = true;
            ObjectTypeCombo.Location = new Point(28, 21);
            ObjectTypeCombo.Name = "ObjectTypeCombo";
            ObjectTypeCombo.Size = new Size(171, 23);
            ObjectTypeCombo.TabIndex = 0;
            ObjectTypeCombo.SelectedValueChanged += ObjectTypeCombo_SelectedValueChanged;
            // 
            // ObjectsListbox
            // 
            ObjectsListbox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            ObjectsListbox.FormattingEnabled = true;
            ObjectsListbox.Location = new Point(28, 52);
            ObjectsListbox.Name = "ObjectsListbox";
            ObjectsListbox.Size = new Size(339, 469);
            ObjectsListbox.TabIndex = 1;
            ObjectsListbox.MouseDoubleClick += ObjectsListbox_MouseDoubleClick;
            // 
            // OKButton
            // 
            OKButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            OKButton.Location = new Point(28, 534);
            OKButton.Name = "OKButton";
            OKButton.Size = new Size(75, 23);
            OKButton.TabIndex = 2;
            OKButton.Text = "OK";
            OKButton.UseVisualStyleBackColor = true;
            OKButton.Click += OKButton_Click;
            // 
            // RefreshButton
            // 
            RefreshButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            RefreshButton.Location = new Point(292, 534);
            RefreshButton.Name = "RefreshButton";
            RefreshButton.Size = new Size(75, 23);
            RefreshButton.TabIndex = 4;
            RefreshButton.Text = "Refresh";
            RefreshButton.UseVisualStyleBackColor = true;
            RefreshButton.Click += RefreshButton_Click;
            // 
            // SelectObjectUI
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(398, 578);
            Controls.Add(RefreshButton);
            Controls.Add(OKButton);
            Controls.Add(ObjectsListbox);
            Controls.Add(ObjectTypeCombo);
            MaximizeBox = false;
            Name = "SelectObjectUI";
            SizeGripStyle = SizeGripStyle.Show;
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Select View Object";
            ResumeLayout(false);
        }

        #endregion

        private ComboBox ObjectTypeCombo;
        private ListBox ObjectsListbox;
        private Button OKButton;
        private Button RefreshButton;
    }
}