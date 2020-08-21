using System;
using System.Collections.Generic;
using System.Threading;

using Play.Interfaces.Embedding; 

namespace Mjolnir {
    partial class Program {
        private class RoundRobinWorkPlace : IPgRoundRobinWork {
            readonly Program _oHost;
            IEnumerator<int> _oWorker;
            long             _iStartTick = Timeout.Infinite; // The time when the user wants to start.

            public RoundRobinWorkPlace( Program oHost ) {
				_oHost = oHost ?? throw new ArgumentNullException();
            }

            public long Appointment {
                get {
                    return( _iStartTick );
                }
            }

            public void Queue( IEnumerator<int> oWorker, long iWaitInMilliSecs ) {
				Stop();

                _oWorker = oWorker ?? throw new ArgumentNullException();

                _iStartTick = DateTime.Now.AddMilliseconds( iWaitInMilliSecs ).Ticks;

                _oHost.WorkerPlaceQue( this );
            }

            public void Stop() {
				_iStartTick = Timeout.Infinite;

				if( _oWorker != null ) {
					_oHost.WorkerPlaceRemove( this );
					_oWorker.Dispose();
					_oWorker = null;
				}
            }

			public void Start( long iWaitInMilliSec ) {
				if( _oWorker != null ) {
					_iStartTick = DateTime.Now.AddMilliseconds( iWaitInMilliSec ).Ticks;
					_oHost.TimerStart();
				}
			}

			public void Pause() {
				_iStartTick = Timeout.Infinite;
			}

			public bool Execute( Guid guidCommand ) {
				bool fReturn = true;

				switch( guidCommand ) {
					case var r when ( r == GlobalCommands.Play ):
						Start( 0 );
						break;
					case var r when ( r == GlobalCommands.Pause ):
						Pause();
						break;
					case var r when ( r == GlobalCommands.Stop ):
						Stop();
						break;
					default:
						fReturn = false;
						break;
				}

				return fReturn;
			}

			public WorkerStatus Status {
				get { 
					if( _oWorker != null ) {
						if( _iStartTick == Timeout.Infinite )
							return( WorkerStatus.PAUSED );
						return( WorkerStatus.BUSY );
					}
					return( WorkerStatus.FREE );
				}
			}

            public bool DoWork( ref uint uiWaitInMS ) {
                try {
                    if( _oWorker.MoveNext() ) {
						uiWaitInMS = (uint)_oWorker.Current;
						_iStartTick = DateTime.Now.AddMilliseconds( uiWaitInMS ).Ticks;
						return( true );
					}
                } catch( NullReferenceException ) {
                    Stop();
                }
                return( false );
            }
		} // End Class
    } // End Class
}
