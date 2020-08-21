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
            this.button1 = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.oGoto = new System.Windows.Forms.LinkLabel();
            this.oResults = new System.Windows.Forms.LinkLabel();
            this.oSearchType = new System.Windows.Forms.ComboBox();

			// Lay them out.
            this.SuspendLayout();
            // 
            // _oTextBox1
            // 
            //this._oWin_SearchKey.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            //            | System.Windows.Forms.AnchorStyles.Right)));
            //this._oWin_SearchKey.Location = new System.Drawing.Point(3, 12);
            this._oWin_SearchKey.Name = "_oTextBox1";
            this._oWin_SearchKey.Size = new System.Drawing.Size(324, 20);
            this._oWin_SearchKey.TabIndex = 0;
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
            this.oSearchType.SelectedIndexChanged += Regex_SelectedIndexChanged;
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
            this.button1.Name = "button1";
            //this.button1.Size = new System.Drawing.Size(40, 23);
            this.button1.TabIndex = 3;
            this.button1.Text = "Next";
            this.button1.UseVisualStyleBackColor = true;
            // 
            // All
            // 
            //this.button2.Location = new System.Drawing.Point(47, 64);
            this.button2.Name = "button2";
            //this.button2.Size = new System.Drawing.Size(40, 23);
            this.button2.TabIndex = 4;
            this.button2.Text = "All";
            this.button2.UseVisualStyleBackColor = true;
            // 
            // Edit
            // 
            this.oGoto.AutoSize = true;
            //this.oGoto.Location = new System.Drawing.Point(89, 69);
            this.oGoto.Name = "oGoto";
            //this.oGoto.Size = new System.Drawing.Size(25, 13);
            this.oGoto.TabIndex = 5;
            this.oGoto.TabStop = true;
            this.oGoto.Text = "edit"; //"\xe16d"
            this.oGoto.LinkClicked += this.Goto_LinkClicked;
            // 
            // Results
            // 
            this.oResults.AutoSize = true;
            //this.oResults.Location = new System.Drawing.Point(118, 69);
            this.oResults.Name = "oResults";
            //this.oResults.Size = new System.Drawing.Size(25, 13);
            this.oResults.TabIndex = 6;
            this.oResults.TabStop = true;
            this.oResults.Text = this.ResultsTitle;
            this.oResults.LinkClicked += this.Results_LinkClicked;
			// 
			// FindWindow
			// 
			//this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			//this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.Controls.Add(this.oResults);
			this.Controls.Add(this.oGoto);
			this.Controls.Add(this.oSearchType);
			this.Controls.Add(this.button2);
			this.Controls.Add(this.button1);
			this.Controls.Add(this.oMatchCase);
			this.Controls.Add(this._oWin_SearchKey);
			this.Margin = new System.Windows.Forms.Padding(10);
            this.Name = "FindWindow";
            this.Size = new System.Drawing.Size(330, 117);
            this.ResumeLayout(false);
            //this.PerformLayout();

        }

        #endregion

		private Button    button1;
        private Button    button2;
        private ComboBox  oSearchType;
		private CheckBox  oMatchCase;
        private LinkLabel oGoto;
        private LinkLabel oResults;
        private Control   _oWin_SearchKey;
    }
}
