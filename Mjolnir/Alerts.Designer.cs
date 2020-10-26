using Play.Edit;

namespace Mjolnir {
	partial class Alerts {
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing) {
			if (disposing && (components != null)) {
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

            this._oWin_Alerts.Dock = System.Windows.Forms.DockStyle.Fill;
            this._oWin_Alerts.Location = new System.Drawing.Point(0, 0);
            this._oWin_Alerts.Name = "AlertsNotify";
            this._oWin_Alerts.TabIndex = 0;

			// 
			// Alerts
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(412, 252);

            this.Controls.Add(this._oWin_Alerts);

			this.Name = "Alerts";
			this.Text = "Alerts";
			this.ResumeLayout(false);

		}

		#endregion
        private EditWindow2 _oWin_Alerts;
	}
}