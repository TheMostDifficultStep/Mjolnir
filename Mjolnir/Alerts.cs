using System;
using System.Windows.Forms;
using Play.Interfaces.Embedding;

using Play.Edit;

namespace Mjolnir {
	public partial class Alerts : Form {
		/// <summary>
		/// An alerts window to pop up when things are going wrong. The user can also use the
		/// adornment, but we still need a non blocking pop up to get their attention, until
		/// I create some sort of menu chevron to blink errors.
		/// </summary>
		/// <remarks>Normally we would create a view slot for the alerts window as a whole
		/// and then it would create view sites for it's children. I haven't set that up yet
		/// and I'll just hack it for the moment.
		/// You know, this would be best implemented in the MainWin, so that I could have
		/// different alerts for tablets, with their smallish real estate and touch.</remarks>
		/// <param name="oProgram">Our main program pointer.</param>
		public Alerts( Program oProgram ) {
			if( oProgram == null )
				throw new ArgumentNullException( "Need MainWin pointer for alerts.");

			// We can get alerts when we're booting and then we're boned...
			BaseEditor oAlertsDoc = (BaseEditor)oProgram.AlertSlot.Document ?? throw new InvalidOperationException("too soon for alerts");

			Program.ViewSlot oAlertSlot = new Program.ViewSlot( oProgram );
			oAlertSlot.Guest = _oWin_Alerts = new EditWin( oAlertSlot, oAlertsDoc, true, false );

			if( !oAlertSlot.InitNew() ) {
				throw new InvalidOperationException( "Coudn't set up Alerts window." );
			}

			InitializeComponent();
		}

		public new void Show() {
			_oWin_Alerts.ScrollTo( EDGE.BOTTOM );
			base.Show();
		}

		/// <summary>
		/// Override the close. We never destroy this window. Just hide it we 
		/// might need it later. 
		/// </summary>
		/// <remarks>It might be nicer to simply override the WM_CLOSE event
		/// when the user presses the X. That's the event we actually want 
		/// to override.</remarks>
		protected override void OnFormClosing(FormClosingEventArgs e) {
			//base.OnFormClosing(e);

			if(   e.CloseReason == CloseReason.WindowsShutDown 
		        ||e.CloseReason == CloseReason.ApplicationExitCall
				||e.CloseReason == CloseReason.TaskManagerClosing ) 
				return;

			e.Cancel = true;
			Hide();
		}
	} // End Class
}
