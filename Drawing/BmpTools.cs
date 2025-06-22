using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Linq;

using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Rectangles;
using System.Net.Http;

namespace Play.Drawing {
	public class ImageHelpers {
		/// <summary>
		/// Use this function to scale an image respecting the aspect ratio of the source and target.
		/// </summary>
		/// <param name="szBorder">A border to add to the image.</param>
		/// <param name="szWindow">Size of presentation space.</param>
		/// <param name="rctBitmap">Portion of the bitmap to show.</param>
		/// <param name="rctViewPort">Computed rectangle to place the image in the target window. (side effect)</param>
        public static void ViewPortSizeMax( Size szBorder, Size szWindow, SmartRect rctBitmap, SmartRect rctViewPort ) 
        {
			Size  szWinSize      = new Size( szWindow.Width - szBorder.Width, szWindow.Height - szBorder.Height );
            float flImageAspect  = rctBitmap.Width / (float)rctBitmap.Height;
            float flWindowAspect = szWinSize.Width / (float)szWinSize.Height;

			// Calculate the new image viewport.
			Size pntViewSize = new Size( rctBitmap.Width, rctBitmap.Height );

			if ( flWindowAspect > flImageAspect ) {
                // Window is wide and squat compared to bitmap.
                // image takes up entire height.
                pntViewSize.Height = szWinSize.Height;
                pntViewSize.Width  = (int)(szWinSize.Height * flImageAspect);
            } else {
                // Window is tall and narrow compared to bitmap.
                // image takes up entire width.
                pntViewSize.Width  = szWinSize.Width;
                pntViewSize.Height = (int)(szWinSize.Width / flImageAspect);
            }

			Point pntUpperLeft = new Point {
				X = ((szWinSize.Width  - pntViewSize.Width  + szBorder.Width  ) / 2 ),
				Y = ((szWinSize.Height - pntViewSize.Height + szBorder.Height ) / 2 )
			};

			rctViewPort.SetRect( LOCUS.UPPERLEFT, pntUpperLeft.X, pntUpperLeft.Y, pntViewSize.Width, pntViewSize.Height );
        }

		/// <summary>
		/// Given a particular width. Use this to calculate the viewport height honoring
		/// the bitmap aspect ratio.
		/// </summary>
		/// <param name="szBorder">Any border around the image.</param>
		/// <param name="iWinWidth">Available width for the view.</param>
		/// <param name="rctBitmap">The bitmap world view.</param>
		/// <param name="rctViewPort">Resulting viewport.</param>
        public static void ViewPortFitWidth( Size szBorder, int iWinWidth, SmartRect rctBitmap, SmartRect rctViewPort ) 
        {
			int   iWidth         = iWinWidth - szBorder.Width;
            float flImageAspect  = rctBitmap.Width / (float)rctBitmap.Height;

			// Calculate the new image viewport.
			Size pntViewSize = new Size( rctBitmap.Width, rctBitmap.Height );

            // Take whatever the width is and find the matching height.
            pntViewSize.Width  = iWinWidth - 2 * szBorder.Width;
            pntViewSize.Height = (int)(pntViewSize.Width / flImageAspect) + 2 * szBorder.Height;

			Point pntUpperLeft = new Point( szBorder.Width, szBorder.Height );

			rctViewPort.SetRect( LOCUS.UPPERLEFT, pntUpperLeft.X, pntUpperLeft.Y, pntViewSize.Width, pntViewSize.Height );
        }
	}

    public delegate void ImageUpdatedEvent();

	public class ImageBaseDoc :
		IPgParent,
		IDisposable
	{
        protected readonly IPgBaseSite _oSiteBase;
        protected          Bitmap      _oBitmapUnknown; // An error bitmap.
        protected          SKBitmap    _oSKBmpError;    // New error bitmap. >_<;;
		protected readonly string      _strUnknownImage = @"Drawing.Content.icons8-error-48.png";

        public static readonly Type[] _rgBmpLoadErrs = { 
			typeof( NullReferenceException ),
            typeof( ArgumentNullException ),
            typeof( ArgumentException ),
            typeof( System.Security.SecurityException ),
            typeof( FileNotFoundException ), // Handles bum entry in bag case.
            typeof( PathTooLongException ),
            typeof( DirectoryNotFoundException ),
            typeof( UnauthorizedAccessException ),
            typeof( NotSupportedException ),
            typeof( PlatformNotSupportedException ),
          //typeof( FileFormatException ),
            typeof( FileNotFoundException ), 
            typeof( IOException ),
            typeof( NotSupportedException ),
            typeof( AggregateException ),
            typeof( HttpRequestException ),
            typeof( System.Net.Sockets.SocketException )
		};

		public IPgParent Parentage   => _oSiteBase.Host;
		public IPgParent Services    => Parentage.Services;

        /// <summary>
        /// Set the bitmap to display. NOTE: Previous bitmap will be Disposed!!
        /// (If it exists and is not the same bitmap as present)
        /// </summary>
		public SKBitmap Bitmap { 
            get { return _skBitmap; }
            set { 
                if( value != _skBitmap ) {
                    if( _skBitmap != null ) {
                        _skBitmap.Dispose();
                    }

                    _skBitmap = value;

                    if( _skBitmap != null ) {
                        WorldDisplay = new SKRectI( 0, 0, _skBitmap.Width, _skBitmap.Height );
                    } else {
                        WorldDisplay = new SKRectI( 0, 0, 0, 0 );
                    }
                }
		    }
        }

        /// <summary>
        /// The portion of the bitmap we want to show.
        /// </summary>
        public SKRectI WorldDisplay { 
            get { return _skWorldDisplay; } 
            set { _skWorldDisplay = value; Raise_ImageUpdated(); } 
        }

        /// <summary>
        /// Size of the world display of the contained bitmap.
        /// </summary>
        public SKSizeI Size {
            get { return _skWorldDisplay.Size; }
        }
        
        public Bitmap    ErrorBitmap => _oBitmapUnknown; // TODO: Remove this...
        public SKBitmap  ErrorBmp    => _oSKBmpError;

        private   SKBitmap _skBitmap;
        protected SKRectI  _skWorldDisplay;

        public event ImageUpdatedEvent ImageUpdated;

        public ImageBaseDoc( IPgBaseSite oSiteBase ) {
            _oSiteBase = oSiteBase ?? throw new ArgumentNullException();

            try {
				_oBitmapUnknown = ImageResourceHelper.GetImageResource(  Assembly.GetExecutingAssembly(), _strUnknownImage );
				_oSKBmpError    = GetSKBitmapResource(  Assembly.GetExecutingAssembly(), _strUnknownImage ) ?? throw new InvalidOperationException( "Couldn't Load Error SKBitmap" );
            } catch( InvalidOperationException ) {
                Type[] rgErrors = { typeof( InvalidOperationException ),
                                    typeof( FileNotFoundException ) };
                _oSiteBase.LogError( "Image Base Constructor", "Having trouble finding error bitmap resource." );
            }
		}

        /// <summary>
        /// We'll pack this out to the embedding interfaces after I get it going in this project.
        /// </summary>
        /// <param name="oAssembly"></param>
        /// <param name="strResourceName"></param>
        /// <returns></returns>
        /// <exception cref="ApplicationException" />
		public static SKBitmap GetSKBitmapResource( Assembly oAssembly, string strResourceName ) {
			try {
                // Let's you peep in on all of them! ^_^
                // string[] rgStuff = oAssembly.GetManifestResourceNames();

				using( Stream oStream = oAssembly.GetManifestResourceStream( strResourceName )) {
					return SKBitmap.Decode( oStream );
				}
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ), 
									typeof( ArgumentNullException ),
									typeof( ArgumentException ),
									typeof( NotImplementedException ) };
				if( !rgErrors.Contains( oEx.GetType() ) )
					throw;

				throw new ApplicationException( "Could not retrieve given image resource : " + strResourceName );
			}
		}

		public virtual void Dispose() {
            if( Bitmap != null ) {
                Bitmap.Dispose();
                Bitmap = null;
            }
			if( _oBitmapUnknown != null ) {
				_oBitmapUnknown.Dispose();
				_oBitmapUnknown = null;
			}
		}

		protected virtual bool Initialize() {
			return true;
		}

		public virtual void Raise_ImageUpdated() {
            ImageUpdated?.Invoke();
        }

		public virtual bool Execute( Guid sGuid ) {
			return( false );
		}

        /// <summary>
        /// Use Assembly.GetExecutingAssembly() in order to get the assembly
        /// of the running code where the resource you want lives.
        /// </summary>
        /// <param name="strName">Name of the resource, expected in a "Content" directory.</param>
        /// <param name="oAsm">Your assembly executing this code.</param>
        /// <remarks>.net core has new behavior for embedded resources. It is not the
        ///           namespace name, but the assembly name (at last)
        /// </remarks>
		public SKBitmap GetResource( string strName, Assembly oAsm ) {
            string strRes = oAsm.GetName().Name + ".Content." + strName;

			return SKImageResourceHelper.GetImageResource( oAsm, strRes );
		}
	}

	public class SKImageResourceHelper {
		/// <summary>
		/// Get the specified resource from the currently executing assembly.
		/// </summary>
		/// <exception cref="InvalidOperationException" />
        /// <remarks>Consider changing the exception to ApplicationException</remarks>
		public static SKBitmap GetImageResource( Assembly oAssembly, string strResourceName ) {
			try {
                // Let's you peep in on all of them! ^_^
                // string[] rgStuff = oAssembly.GetManifestResourceNames();

				using( Stream oStream = oAssembly.GetManifestResourceStream( strResourceName )) {
					return SKBitmap.Decode( oStream );
				}
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( NullReferenceException ), 
									typeof( ArgumentNullException ),
									typeof( ArgumentException ),
									typeof( FileLoadException ),
									typeof( BadImageFormatException ),
									typeof( NotImplementedException ) };
				if( !rgErrors.Contains( oEx.GetType() ) )
					throw;

				throw new InvalidOperationException( "Could not retrieve given image resource : " + strResourceName );
			}
		}

	}

    //FloodFill oFlood = new FloodFill(oView.Document.Bitmap);

    //oFlood.OldColor = Color.White;
    //oFlood.NewColor = Color.BlanchedAlmond;
    //oFlood.Tolerance = 160;
    //oFlood.Fill( oView.ToWorld( e.X, e.Y ));

    //Invalidate(oView.Rect); 

	/// <summary>
	/// This is an old FloodFill I've had lying around for ages. One of these days
	/// I think it'll be useful so I hang on to it.
	/// </summary>
    public class FloodFill
    {
        Bitmap m_oSource = null;
        byte[,] m_rgFlood = null;

        uint m_uiTolerance = 0;
        uint[] m_rgColorNew = new uint[4];
        uint[] m_rgColorOld = new uint[4]; // The picked color.
        uint[] m_rgColorLo  = new uint[4]; // old color - tolerance
        uint[] m_rgColorHi  = new uint[4]; // old color + tolerance

        public FloodFill(Bitmap p_oBmp)
        {
            m_oSource = p_oBmp;
            m_rgFlood = new byte[m_oSource.Width, m_oSource.Height];
            NewColor = Color.Azure;
        }

        // Take the color and generate the bounding color based on the tolerance. iMul is
        // either 1 or -1. We use this function to calculate the color above and below the
        // picked color given the tolerance.
        private static void SetTolerance(uint[] rgColor, uint iTol, int iMul, uint[] rgResult)
        {
            for (int i = 0; i < rgColor.Length; ++i)
            {
                uint iTemp = (uint)(rgColor[i] + (iTol * iMul));

                if (iTemp < 0)
                    iTemp = 0;
                else if (iTemp > 255)
                    iTemp = 255;

                rgResult[i] = iTemp;
            }
        }

        public uint Tolerance
        {
            get
            {
                return (m_uiTolerance);
            }
            set
            {
                m_uiTolerance = value;

                SetTolerance(m_rgColorOld, m_uiTolerance, -1, m_rgColorLo);
                SetTolerance(m_rgColorOld, m_uiTolerance, 1, m_rgColorHi);
            }
        }

        public Color OldColor
        {
            get
            {
                return Color.FromArgb((int)m_rgColorOld[3], (int)m_rgColorOld[0], (int)m_rgColorOld[1], (int)m_rgColorOld[2]);
            }

            set
            {
                m_rgColorOld[0] = value.B;
                m_rgColorOld[1] = value.G;
                m_rgColorOld[2] = value.R;
                m_rgColorOld[3] = value.A;

                Tolerance = Tolerance;
            }
        }

        public Color NewColor
        {
            get
            {
                return Color.FromArgb((int)m_rgColorNew[3], (int)m_rgColorNew[0], (int)m_rgColorNew[1], (int)m_rgColorNew[2]);
            }
            set
            {
                m_rgColorNew[0] = value.B;
                m_rgColorNew[1] = value.G;
                m_rgColorNew[2] = value.R;
                m_rgColorNew[3] = value.A;
            }
        }

        class Iterator :
            IEnumerator<Point>
        {
            FloodFill    _oOwner;
            Stack<Point> _oStack;
            BitmapData   _oData;
            unsafe byte* _pSource  = null;
            Point[]      _rgPoints = new Point[4];

            public Iterator(FloodFill oOwner, Point pntSeed)
            {
                _oOwner = oOwner ?? throw (new ArgumentNullException());

                ClearMask();

                Rectangle rcArea = new Rectangle(0, 0, _oOwner.m_oSource.Width, _oOwner.m_oSource.Height);

                _oData = _oOwner.m_oSource.LockBits(rcArea, ImageLockMode.ReadWrite, PixelFormat.Format32bppRgb);
                unsafe
                {
                    _pSource = (byte*)_oData.Scan0.ToPointer();
                }
                _oStack = new Stack<Point>(rcArea.Width * rcArea.Height);
                _oStack.Push(pntSeed);
            }

			bool _fDisposed = false;

            public void Dispose()
            {
				if( !_fDisposed ) {
					_fDisposed = true;

					ReplaceBits();
					_oOwner.m_oSource.UnlockBits(_oData);
					unsafe { _pSource = null; }
				}
            }

            private void ClearMask()
            {
                for (int i = 0; i < _oOwner.m_oSource.Height; ++i)
                {
                    for (int j = 0; j < _oOwner.m_oSource.Width; ++j)
                    {
                        _oOwner.m_rgFlood[j, i] = 0;
                    }
                }
            }

            public bool MoveNext()
            {
                try {
                    Point pntCurrent = Point.Empty;

                    for (int i = 0; i < 10000; ++i) {
                        pntCurrent = _oStack.Pop();
                        DoFill(pntCurrent.X, pntCurrent.Y);
                    }
                } catch (NullReferenceException) {
					return false;
                } catch (InvalidOperationException) {
                    // When the stack is empty it throws this exception.
                    return false;
                }

                return true;
            }

            public void Reset()
            {
                throw (new NotImplementedException());
            }

            // This is the new generic implementation.
            Point IEnumerator<Point>.Current
            {
                get
                {
                    return _oStack.Peek();
                }
            }

            // This is the original IEnumerator implementation.
            public object Current
            {
                get {
                    return (_oStack.Peek());
                }
            }

            // We've finished our walk. Now let's set the bits based on what
            // we filled in for the mask.
            private void ReplaceBits()
            {
				try {
					for (int iY = 0; iY < _oData.Height; ++iY)
					{
						int iRowStride = iY * _oData.Stride;

						for (int iX = 0; iX < _oData.Width; ++iX)
						{
							if (_oOwner.m_rgFlood[iX, iY] != 0)
							{
								unsafe
								{
									int iIndex = iRowStride + (iX * 4);

									for (int k = 0; k < 4; ++k)
									{
										_pSource[iIndex + k] = (byte)_oOwner.m_rgColorNew[k];
									}
								}
							}
						}
					}
				} catch( NullReferenceException ) {
				}
            } // End ReplaceBits

            bool IsHit(int p_iX, int p_iY)
            {
                if (p_iX >= 0 && p_iX < _oData.Width &&
                    p_iY >= 0 && p_iY < _oData.Height &&
                    _oOwner.m_rgFlood[p_iX, p_iY] == 0)
                {
                    unsafe
                    {
                        int index = p_iY * _oData.Stride + (p_iX * 4);

                        for (int i = 0; i < 4; ++i)
                        {
                            if (_pSource[index + i] < _oOwner.m_rgColorLo[i] ||
                                _pSource[index + i] > _oOwner.m_rgColorHi[i])
                                return (false);
                        }
                        return (true);
                    }
                }
                return (false);
            } // End IsHit

            void DoFill(int p_iX, int p_iY)
            {
                if (IsHit(p_iX, p_iY))
                {
                    int[] rgAddX = { -1, 1, 0, 0 };
                    int[] rgAddY = { 0, 0, -1, 1 };

                    // If it's a hit then set the pixel in the mask.
                    _oOwner.m_rgFlood[p_iX, p_iY] = 1;

                    // Look up the top bottom left right points.
                    for (int i = 0; i < _rgPoints.Length; ++i)
                    {
                        _rgPoints[i].X = p_iX + rgAddX[i];
                        _rgPoints[i].Y = p_iY + rgAddY[i];
                    }

                    // Now copy our four points to the array.
                    for (int i = 0; i < _rgPoints.Length; ++i)
                    {
                        _oStack.Push(_rgPoints[i]);
                    }
                }
            } // End DoFill
        } // end class iterator

        public IEnumerator<Point> GetEnumerator(Point p_pntSeed)
        {
            return (new Iterator(this, p_pntSeed));
        }

        public void Fill(Point p_pntSeed)
        {
            using( IEnumerator<Point> oIter = GetEnumerator(p_pntSeed) ) {
	            while (oIter.MoveNext()) ;
			}
        } // End Fill
    } // End class FloodFill

	/// <summary>
	/// A histogram is a great way to determine how clean a sketch is. Something
	/// else I'll want in a drawing document eventually.
	/// </summary>
	public class HistoGram 
	{
		float[] m_rgGrey = new float[256];
		float   m_flMax  = 0;

		public HistoGram() 
		{
			Clear();
		}

		public void Clear()
		{
			for( int i=0; i<m_rgGrey.Length; ++i )
			{
				m_rgGrey[i] = 0;
			}
			m_flMax = 0;
		}

		/// <summary>
		/// Take the given image and generate a greyscale histogram from it.
		/// </summary>
		public void Compute( Bitmap p_oBitmap )
		{
			Point l_oPoint = new Point();
			int   l_iSum   = p_oBitmap.Width * p_oBitmap.Height;

			Clear();

			for( l_oPoint.X=0; l_oPoint.X < p_oBitmap.Width; ++l_oPoint.X )
			{
				for( l_oPoint.Y=0; l_oPoint.Y < p_oBitmap.Height; ++l_oPoint.Y ) 
				{
					Color l_oColor = p_oBitmap.GetPixel( l_oPoint.X, l_oPoint.Y );
					int   l_iGrey = ( l_oColor.R + l_oColor.G + l_oColor.B ) / 3;

					m_rgGrey[l_iGrey]++;
				}
			}

			for( int i = 0; i< m_rgGrey.Length; ++i )
			{
				m_rgGrey[i] /= l_iSum;

				if( m_rgGrey[i] > m_flMax )
					m_flMax = m_rgGrey[i];
			}
		}

		public Bitmap Chart( int p_iHeight ) 
		{
			Bitmap   l_oChart = new Bitmap( m_rgGrey.Length, p_iHeight );
			Graphics l_oGraph = Graphics.FromImage( l_oChart );
			Brush    l_oBrush = new SolidBrush( Color.Black );
			Pen      l_oPen   = new Pen( l_oBrush, 1 );
			float    l_flMultiplyer = p_iHeight / m_flMax;

			l_oGraph.FillRectangle( new SolidBrush( Color.White ), 0, 0, l_oChart.Width, l_oChart.Height );

			for( int x=0; x<m_rgGrey.Length; ++x ) 
			{
				int y = (int)(m_rgGrey[x] * l_flMultiplyer);

				l_oGraph.DrawLine( l_oPen, x, p_iHeight, x, p_iHeight - y );
			}

			return( l_oChart );
		}
	} // End Histogram.
}
