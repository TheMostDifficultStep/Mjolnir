using System;
using System.Drawing;
using System.Windows.Forms;
using System.Xml;
using System.Collections.Generic;

using Play.Interfaces.Embedding;
using Play.Sound;
using Play.Sound.FFT;

namespace Play.MusicWalker {
	/// <remarks>This might be best in the Integration directory.</remarks>
	public class FourierVisualizer : Fourier {
		public FourierVisualizer() : base() {}

		public void Paint( Graphics oG, Size oSize ) {
			oG.FillRectangle( Brushes.White, 0, 0, oSize.Width, oSize.Height );

			if( oSize.Width > Results.Count ) {
				// There are less samples than pixels wide...
				int iIncr = (int)Math.Round( oSize.Width / (double)Results.Count );

				for( int i=0, j=0; i < oSize.Width && j<Results.Count; i += iIncr, j++ ) {
					int iHeight = (int)(oSize.Height * Results[j] );
					oG.FillRectangle( Brushes.Aqua, i, 0, iIncr, iHeight );
				}
			} else {
				// We can move this to the OnSizeChanged() event.
				int      iBucketPixelWidth = 5;
				int      iBuckets          = oSize.Width / iBucketPixelWidth;
				int      iResultsPerBucket = ( Results.Count / iBuckets ) + 1;
				double[] rgBuckets         = new double[iBuckets];

				for( int i=0, iBucket=0, iCollect=1; i<Results.Count; i++ ) {
					if( Results[i] > rgBuckets[iBucket] )
						rgBuckets[iBucket] = Results[i];

					if( ++iCollect > iResultsPerBucket ) {
						iBucket++;
						iCollect = 1;
					}
				}
				for( int i=0, j=0; i<rgBuckets.Length; ++i, j+=iBucketPixelWidth ) {
					int iHeight = (int)(oSize.Height * rgBuckets[i] );
					oG.FillRectangle( Brushes.Aqua, j, 0, iBucketPixelWidth, iHeight );
				}
			}
		}
	}

	/// <summary>
	/// A little view for showing my fourier transform experiments. I just tacked it onto my
	/// music object in the hopes of showing frequence responce when I get a faster transform.
	/// </summary>
	public class VisualizeWindow:
		Control,
		IPgParent,
		IPgLoad<XmlElement>,
		IPgSave<XmlDocumentFragment>,
		IPgCommandView 
	{
		public static readonly Guid _gViewType = new Guid( "{EF0DB19C-CEB3-47CC-A52B-9E6DBD389485}" );

		IPgViewSite       _oViewSite;
		//FourierVisualizer _oSpectrum = new FourierVisualizer();
		readonly CFFT         _oFFT        = new CFFT( FFTControlValues.FindMode( 8000 ) );
		readonly List<double> _rgFFTData   = new List<double>();
		         int[]        _rgFFTResult;

		public VisualizeWindow( IPgViewSite oViewSite, MusicCollection oDocument ) {
			_oViewSite = oViewSite ?? throw new ArgumentNullException();
		}

		public IPgParent Parentage => throw new NotImplementedException();
		public IPgParent Services  => Parentage.Services;
		public bool      IsDirty   => false;
		public string    Banner    => "Frequency Space";
		public Image     Iconic    => null;
		public Guid      Catagory  => _gViewType;

        protected static void LoadData( FFTControlValues oCtrl, List<double> rgData ) {
            // We need FFTSize number of samples.
			rgData.Clear();
            for( double t = 0; rgData.Count < oCtrl.FFTSize; t += 1 / oCtrl.SampBase ) {
                double dbSample = 0;
                    
                dbSample += 80 * Math.Sin( Math.PI * 2 *  400 * t);
                dbSample += 20 * Math.Sin( Math.PI * 2 *  200 * t);
                dbSample += 20 * Math.Sin( Math.PI * 2 * 1200 * t);

                rgData.Add(dbSample);
            }
        }

		public bool InitNew() {
			LoadData( _oFFT.Mode, _rgFFTData );
			_rgFFTResult = new int[_oFFT.Mode.TopBucket];
			_oFFT.Calc( _rgFFTData.ToArray(), 30, 0, _rgFFTResult );
			return true;
		}

		public bool Load(XmlElement oStream) {
			InitNew();
			return true;
		}

		public bool Save(XmlDocumentFragment oStream) {
			return true;
		}

		protected override void OnPaint(PaintEventArgs e) {
			base.OnPaint(e);

			e.Graphics.TranslateTransform( 0, +Height );
			e.Graphics.ScaleTransform    ( 1, -1 );

			PaintMe( e.Graphics, new Size( Width, Height ) );
		}

		public void PaintMe( Graphics oG, Size oSize ) {
			oG.FillRectangle( Brushes.White, 0, 0, oSize.Width, oSize.Height );
			double   dbMaxSignal       = 0;

			if( oSize.Width > _rgFFTResult.Length ) {
				// There are less FFT results than pixels wide...
				int iIncr = (int)Math.Round( oSize.Width / (double)_rgFFTResult.Length );

				for( int iResult =0; iResult < _rgFFTResult.Length; ++iResult ) {
					// Save the max signal for scaling.
					if( dbMaxSignal < _rgFFTResult[iResult] )
						dbMaxSignal = _rgFFTResult[iResult];
				}

				for( int i=0, j=0; i < oSize.Width && j<_rgFFTResult.Length; i += iIncr, j++ ) {
					int y = (int)( oSize.Height * _rgFFTResult[j] / dbMaxSignal );
					oG.FillRectangle( Brushes.Aqua, i, 0, iIncr, y );
				}
			} else {
				// There are more FFT results than pixels wide...
				// We can move some of this to the OnSizeChanged() event.
				int      iBucketPixelWidth = 1;
				int      iResultsPerBucket = (int)Math.Round( _rgFFTResult.Length / (double)oSize.Width );
				double[] rgBuckets         = new double[oSize.Width];

				for( int iBucket=0, iResult=0, iDrop=0; iResult<_rgFFTResult.Length; iResult++) {
					// Save the max signal for scaling.
					if( dbMaxSignal < _rgFFTResult[iResult] )
						dbMaxSignal = _rgFFTResult[iResult];

					// Find max result for the bucket.
					if( rgBuckets[iBucket] < _rgFFTResult[iResult])
						rgBuckets[iBucket] = _rgFFTResult[iResult];

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
			return null;
		}

		public bool Execute(Guid sGuid) {
			return false;
		}
	}
}
