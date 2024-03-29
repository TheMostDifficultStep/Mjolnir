﻿using System;
using System.Drawing;
using System.Xml;
using System.Windows.Forms;

using SkiaSharp;

using Play.Interfaces.Embedding;

namespace Play.SSTV {
	/// <summary>
	/// A little view for showing my fourier transform experiments. I just tacked it onto my
	/// music object in the hopes of showing frequence responce when I get a faster transform.
	/// </summary>
	public class ViewFFT:
		Control,
		IPgParent,
		IPgLoad<XmlElement>,
		IPgSave<XmlDocumentFragment>,
		IPgCommandView,
		IDisposable
	{
		public static Guid GUID {get;} = new Guid( "{EF0DB19C-CEB3-47CC-A52B-9E6DBD389485}" );

		protected IPgViewSite _oViewSite;
		protected DocSSTV     _oDocSSTV;

		public ViewFFT( IPgViewSite oViewSite, DocSSTV oDocument ) {
			_oViewSite = oViewSite ?? throw new ArgumentNullException( "View requires a view site." );
			_oDocSSTV  = oDocument ?? throw new ArgumentNullException( "View requires a document." );

		}

		public IPgParent Parentage => _oViewSite.Host;
		public IPgParent Services  => Parentage.Services;
		public bool      IsDirty   => false;
		public string    Banner    => "Frequency Space";
		public SKBitmap  Icon      => null;
		public Guid      Catagory  => GUID;

		public void LogError( string strMessage ) {
			_oViewSite.LogError( "SSTV View", strMessage );
		}

		protected override void Dispose(  bool disposing ) {
			_oDocSSTV.PropertyChange -= OnPropertyChange_DocSSTV;

			base.Dispose( disposing );
		}

		public bool InitNew() {
            _oDocSSTV.PropertyChange += OnPropertyChange_DocSSTV;

			return true;
		}

        /// <summary>
        /// This is our event sink for property changes on the SSTV document.
        /// </summary>
        /// <remarks>Right now just update all, but we can just update the
        /// specific property in the future. You know, a color coded property, 
        /// light red or yellow on change would be a cool feature.</remarks>
        private void OnPropertyChange_DocSSTV( SSTVEvents eProp ) {
			if( eProp == SSTVEvents.FFT ) {
				Invalidate();
			}
        }

        public bool Load( XmlElement oStream ) {
			InitNew();
			return true;
		}

		public bool Save( XmlDocumentFragment oStream ) {
			return true;
		}

		protected override void OnPaint( PaintEventArgs e ) {
			base.OnPaint(e);

			e.Graphics.TranslateTransform( 0, +Height );
			e.Graphics.ScaleTransform    ( 1, -1 );

			PaintMe( e.Graphics, new Size( Width, Height ) );
		}

		public void PaintMe( Graphics oG, Size oSize ) {
			oG.FillRectangle( Brushes.White, 0, 0, oSize.Width, oSize.Height );

			double   dbMaxSignal   = 0;
			double   dbMinSignal   = 0;
			int[]    rgFFTResult   = _oDocSSTV.FFTResult;
			int      iFFTResultLen = _oDocSSTV.FFTResultSize;

			if( iFFTResultLen == 0 )
				return;

			if( oSize.Width > iFFTResultLen ) {
				// There are less FFT results than pixels wide...
				double dbStep = oSize.Width / (double)iFFTResultLen;
				int iIncr = (int)Math.Round( dbStep );

				for( int iResult =0; iResult < iFFTResultLen; ++iResult ) {
					// Save the max signal for scaling.
					if( dbMaxSignal < rgFFTResult[iResult] )
						dbMaxSignal = rgFFTResult[iResult];
					if( dbMinSignal > rgFFTResult[iResult] )
						dbMinSignal = rgFFTResult[iResult];
				}

				for( int i=0; i < oSize.Width; ++i ) {
					int iSample = (int)( i / dbStep );
					int y = (int)( oSize.Height * rgFFTResult[iSample] / dbMaxSignal );
					oG.FillRectangle( Brushes.Aqua, i, 0, iIncr, y );
				}
			} else {
				// There are more FFT results than pixels wide...
				// We can move some of this to the OnSizeChanged() event.
				int      iBucketPixelWidth = 1;
				int      iResultsPerBucket = (int)Math.Round( iFFTResultLen / (double)oSize.Width );
				double[] rgBuckets         = new double[oSize.Width];

				for( int iBucket=0, iResult=0, iDrop=0; iResult<iFFTResultLen; iResult++) {
					// Save the max signal for scaling.
					if( dbMaxSignal < rgFFTResult[iResult] )
						dbMaxSignal = rgFFTResult[iResult];

					// Find max result for the bucket.
					if( rgBuckets[iBucket] < rgFFTResult[iResult])
						rgBuckets[iBucket] = rgFFTResult[iResult];

					if( ++iDrop > iResultsPerBucket ) {
						iBucket++;
						iDrop = 0;
					}
				}
				for( int i=0, x=0; i<rgBuckets.Length; ++i, x+=iBucketPixelWidth ) {
					int y = (int)( oSize.Height * rgBuckets[i] / dbMaxSignal );
					oG.FillRectangle( Brushes.Aqua, x, 0, iBucketPixelWidth, y );
				}
			}
		}

		protected override void OnSizeChanged(EventArgs e) {
			base.OnSizeChanged(e);
			Invalidate();
		}

		public object Decorate(IPgViewSite oBaseSite,Guid sGuid) {
            return( null );
		}

		public bool Execute(Guid sGuid) {
			if( sGuid == GlobalCommands.Play ) {
				//_oDocSSTV.PlayBegin( );
				return true;
			}
			if( sGuid == GlobalCommands.Stop ) {
				_oDocSSTV.TransmitStop();
				return true;
			}

			return false;
		}

        protected override void OnKeyDown(KeyEventArgs e) {
            if( this.IsDisposed )
                return;

            e.Handled = true;

            switch( e.KeyCode ) {
                case Keys.Right:
                case Keys.Enter:
                    _oDocSSTV.PlaySegment();
                    break;
            }
        }
    }
}
