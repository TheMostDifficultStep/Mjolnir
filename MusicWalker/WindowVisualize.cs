using System;
using System.Drawing;
using System.Windows.Forms;
using System.Xml;

using Play.Interfaces.Embedding;
using Play.Sound;

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
		FourierVisualizer _oSpectrum = new FourierVisualizer();

		public VisualizeWindow( IPgViewSite oViewSite, MusicCollection oDocument ) {
			_oViewSite = oViewSite ?? throw new ArgumentNullException();
		}

		public IPgParent Parentage => throw new NotImplementedException();
		public IPgParent Services  => Parentage.Services;
		public bool      IsDirty   => false;
		public string    Banner    => "Frequency Space";
		public Image     Iconic    => null;
		public Guid      Catagory  => _gViewType;

		public bool InitNew() {
			_oSpectrum.Sum();
			return true;
		}

		public bool Load(XmlElement oStream) {
			_oSpectrum.Sum();
			return true;
		}

		public bool Save(XmlDocumentFragment oStream) {
			return true;
		}

		protected override void OnPaint(PaintEventArgs e) {
			base.OnPaint(e);

			e.Graphics.TranslateTransform( 0, +Height );
			e.Graphics.ScaleTransform    ( 1, -1 );

			_oSpectrum.Paint( e.Graphics, new Size( Width, Height ) );
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
