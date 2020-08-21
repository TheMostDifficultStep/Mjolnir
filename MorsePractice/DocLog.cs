using System;
using System.Collections.Generic;

using Play.Interfaces.Embedding;
using Play.Edit;
using System.Collections;

namespace Play.MorsePractice {
	public class CallData {
		Dictionary<int, Line> rgLines;
		//Line CallSign { get; } = 
		//Line Time     { get; }
		//Line Date     { get; }
		//Line Qso      { get; }
		//Line Rst      { get; }
		//Line Remarks  { get; }

		bool	 _fQso;
		int		 _iCallsign;
		DateTime _dtContact;

		public void AddLine( int iLine, string strValue ) {
			rgLines.Add( iLine, new TextLine( 0, strValue ) );
		}

		public CallData() {
			AddLine( 0, "ag7ym" );
			AddLine( 1, DateTime.Now.TimeOfDay.ToString() );
			AddLine( 2, DateTime.Now.Date.ToString() );
			AddLine( 5, "Blah Blah" );
		}
	}

	public class RadioLogItem {
		Line Frequency { get; }
		Line Time      { get; } // Get this value from the first station logged.
		Line Date      { get; } // ditto.
		Line Station   { get; }
		Line Qso       { get; } // aggregated value from call data.
		Line Remarks   { get; }

		int	_iStation;

		List< CallData > CallsList{ get; }

		public RadioLogItem( string strFreq, string strStation, string strRemarks ) {
			Frequency.TryAppend( strFreq );
			Station  .TryAppend( strStation );
			Remarks  .TryAppend( strRemarks );

			for( int i=0; i<3; ++i ) {
				CallData oCall = new CallData();

				CallsList.Add( oCall );
			}
		}
	}

    public class DocLog : 
		IPgParent,
		IEnumerable<RadioLogItem>,
		IPgLoad,
		IDisposable
	{
		readonly IPgBaseSite _oSiteBase;

		List< RadioLogItem > _rgLogs;

		public IPgParent Parentage => _oSiteBase.Host;
		public IPgParent Services  => Parentage.Services;

		public bool InitNew() {
			for( int i = 0; i<3; ++i ) {
				RadioLogItem oLog = new RadioLogItem( "14.22o", "kfs", "noon net" );
				_rgLogs.Add( oLog );
			}
			return true;
		}

		#region IDisposable Support
		private bool disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing) {
			if (!disposedValue) {
				if (disposing) {
					// TODO: dispose managed state (managed objects).
				}

				// TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
				// TODO: set large fields to null.

				disposedValue = true;
			}
		}

		// TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
		// ~DocLog()
		// {
		//   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
		//   Dispose(false);
		// }

		// This code added to correctly implement the disposable pattern.
		public void Dispose() {
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
			// TODO: uncomment the following line if the finalizer is overridden above.
			// GC.SuppressFinalize(this);
		}

		#endregion

		#region IEnumerator<RadioLogItem> Support

		public IEnumerator<RadioLogItem> GetEnumerator() {
			throw new NotImplementedException();
		}

		IEnumerator IEnumerable.GetEnumerator() {
			return GetEnumerator();
		}

		#endregion
	}
}
