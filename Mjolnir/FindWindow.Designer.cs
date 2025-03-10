using System.Windows.Forms;

namespace Mjolnir {
    partial class FindWindow {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        //private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose( bool fManagedDel ) {
            if( fManagedDel /* && ( components != null ) */ ) {
                //components.Dispose();
                _oWinMain.ViewChanged -= _oViewChangedHandler;
            }
            base.Dispose(fManagedDel);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
			// Create the children.
            this.oMatchCase = new System.Windows.Forms.CheckBox();
            this.NextBtn = new System.Windows.Forms.Button();
            this.AllBtn = new System.Windows.Forms.Button();
            this.oSearchType = new System.Windows.Forms.ComboBox();

			// Lay them out.
            this.SuspendLayout();
            // 
            // _oTextBox1
            // 
            //this._oWin_SearchKey.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            //            | System.Windows.Forms.AnchorStyles.Right)));
            //this._oWin_SearchKey.Location = new System.Drawing.Point(3, 12);
            //this._oWin_SearchKey.Name = "_oTextBox1";
            //this._oWin_SearchKey.Size = new System.Drawing.Size(324, 20);
            //this._oWin_SearchKey.TabIndex = 0;
            // 
            // oRegExpr
            // 
            string[] rgSearchTypes = { "Text", "Regex", "Line" };
            this.oSearchType.AutoSize = true;
            //this.oSearchType.Location = new System.Drawing.Point( 3, 38);
            //this.oSearchType.Margin = new System.Windows.Forms.Padding(2);
            this.oSearchType.Name = "oSearchType";
            //this.oSearchType.Size = new System.Drawing.Size(83, 13);
            this.oSearchType.TabIndex = 1;
            this.oSearchType.Items.AddRange( rgSearchTypes );
            this.oSearchType.SelectedIndex = 0;
            this.oSearchType.DropDownStyle = ComboBoxStyle.DropDownList;
            // 
            // oMatchCase
            // 
            this.oMatchCase.AutoSize = true;
            //this.oMatchCase.Location = new System.Drawing.Point( 92, 40);
            this.oMatchCase.Name = "oMatchCase";
            //this.oMatchCase.Size = new System.Drawing.Size(83, 17);
            this.oMatchCase.TabIndex = 2;
            this.oMatchCase.Text = "Match Case";
            this.oMatchCase.UseVisualStyleBackColor = true;
            // 
            // Next
            // 
            //this.button1.Location = new System.Drawing.Point(3, 64);
            this.NextBtn.Name = "button1";
            //this.button1.Size = new System.Drawing.Size(40, 23);
            this.NextBtn.TabIndex = 3;
            this.NextBtn.Text = "Next";
            this.NextBtn.UseVisualStyleBackColor = true;
            // 
            // All
            // 
            //this.button2.Location = new System.Drawing.Point(47, 64);
            this.AllBtn.Name = "button2";
            //this.button2.Size = new System.Drawing.Size(40, 23);
            this.AllBtn.TabIndex = 4;
            this.AllBtn.Text = "All";
            this.AllBtn.UseVisualStyleBackColor = true;
			// 
			// FindWindow
			// 
			//this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			//this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.Controls.Add(this.oSearchType);
			this.Controls.Add(this.AllBtn);
			this.Controls.Add(this.NextBtn);
			this.Controls.Add(this.oMatchCase);
			//this.Controls.Add(this._oWin_SearchKey);
			this.Margin = new System.Windows.Forms.Padding(10);
            this.Name = "FindWindow";
            this.Size = new System.Drawing.Size(330, 117);
            this.ResumeLayout(false);
            //this.PerformLayout();

        }

        #endregion

		private Button    NextBtn;
        private Button    AllBtn;
        private ComboBox  oSearchType;
		private CheckBox  oMatchCase;
    }
}
