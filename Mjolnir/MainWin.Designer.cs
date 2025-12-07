using System;
using Play.Edit;

namespace Mjolnir {
    partial class MainWin {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing) {
                Document.EventUpdateTitles -= UpdateAllTitlesFor;
                if(components != null) {
                    components.Dispose();
                }
			    DecorFont       .Dispose();
                ToolsBrushActive.Dispose();
            }

            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this.components = new System.ComponentModel.Container();
            //this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Text = "mainwin";
        }

        #endregion
    }
}