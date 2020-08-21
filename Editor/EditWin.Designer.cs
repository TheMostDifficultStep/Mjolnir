using System.Drawing;

namespace Play.Edit {
    partial class EditWin {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
			if( disposing ) {
                HyperLinks.Clear();

				_oDocument.ListenerRemove(this);
				_oDocument.CaretRemove(_oCaretPos); 
 				_oDocument.HilightEvent -= OnHighLightChanged; // TODO: Check if intent matches action.

				if( _oMorse != null )
					_oMorse.Stop();
				if( _oIcon != null )
					_oIcon.Dispose();
				if( components != null )
					components.Dispose();
			}
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this.SuspendLayout();
            // 
            // Form1
            // 
          //this.ClientSize = new System.Drawing.Size(692, 579);
            this.Name = "Form1";
            this.BackColor = Color.White;
            this.ResumeLayout(false);
        }

        #endregion
    }
}

