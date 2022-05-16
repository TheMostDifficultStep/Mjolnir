using System;
using System.IO;

using SkiaSharp;

using Play.Interfaces.Embedding;

namespace Play.Rectangles {
    public class LayoutPattern : LayoutRect {
        Func< object, SKColor> _fnStatus;
        object                 _oID;
        public LayoutPattern(CSS eLayout, int iTrack, object oID, Func<object, SKColor> fnStatus ) : base(eLayout) {
            _oID      = oID;
            _fnStatus = fnStatus ?? throw new ArgumentNullException(nameof(fnStatus));
            if( iTrack < 0 )
                throw new ArgumentOutOfRangeException( nameof( iTrack ) );

            Track = (uint)iTrack;
        }

        public override void Paint(SKCanvas skCanvas) {
            base.Paint(skCanvas);

            SKColor clrStat = _fnStatus( _oID );
            if( clrStat != SKColors.Transparent ) {
                using SKPaint skPaint = new SKPaint() { Color = clrStat };

                // Little experiment for span of pattern.
                int iRail = 30;
                int iTop  = Top + Height / 2 - iRail / 2;

                skCanvas.DrawRect( this.Left, iTop, this.Width, iRail, skPaint );
            }
        }
    }

    /// <summary>
    /// Split this behavior out, since I need a dynamic image layout. This
    /// class gives you basic image aspect management.
    /// </summary>
    public class LayoutImageAbstract : LayoutRect {
        public LayoutImageAbstract( LayoutRect.CSS eLayout ) : base(eLayout) {
        }

        // Where to show the image in our layout.
		public SmartRect ViewPort { get; } = new SmartRect();
        // What part of the image to show.
		public SmartRect World    { get; } = new SmartRect();

        public override uint TrackDesired( TRACK eParentAxis, int iRail ) {
            float flImageAspect  = World.Width / (float)World.Height;
			int   iReturn;

			if( eParentAxis == TRACK.HORIZ ) {
                iReturn = (int)(iRail * flImageAspect);
            } else {
                iReturn = (int)(iRail / flImageAspect);
            }

			try { 
				return (uint)iReturn;
			} catch( InvalidCastException ) {
				return (uint)Math.Abs( iReturn );
			}
		}

		/// <remarks>This code is sprinkled around. We might want to see if we
		/// can centralize the implementation.</remarks>
        public void ViewPortSizeMax( SmartRect oViewPort ) {
			try {
				SKSizeI szWinSize   = new SKSizeI( this.Width, this.Height );
				SKSizeI szBmpSize   = new SKSizeI( World.Width, World.Height );
				float   flBmpAspect = szBmpSize.Width / (float)szBmpSize.Height;
				float   flWinAspect = szWinSize.Width / (float)szWinSize.Height;

				if ( flWinAspect > flBmpAspect ) {
					// Window is wide and squat compared to bitmap.
					if( szBmpSize.Height > szWinSize.Height ) {
						// image takes up entire height.
						szBmpSize.Height = szWinSize.Height;
						szBmpSize.Width  = (int)(szWinSize.Height * flBmpAspect);
					}
				} else {
					// Window is tall and narrow compared to bitmap.
					if( szBmpSize.Width > szWinSize.Width ) {
						// image takes up entire width.
						szBmpSize.Width  = szWinSize.Width;
						szBmpSize.Height = (int)(szWinSize.Width / flBmpAspect);
					}
				}

				SKPointI pntCenter = this.GetPoint( LOCUS.CENTER );

				oViewPort.SetRect( LOCUS.CENTER, pntCenter.X, pntCenter.Y, szBmpSize.Width, szBmpSize.Height );
			} catch( NullReferenceException ) {
				oViewPort.SetRect( 0, 0, Width, Height );
			}
		}

        /// <summary>
        /// Load up our image from the given file path.
        /// </summary>
        /// <remarks>Normally we use this object to load thumbnails, however in order to 
        /// generate the thumbnail for the first time, we'll load it into a temporary
        /// image rect.</remarks>
        public static SKBitmap LoadImage( string strFilePath ) {
            try {
                using( Stream oStream = File.OpenRead( strFilePath )) {
                    return SKBitmap.Decode( oStream );
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentException ),
                                    typeof( ArgumentNullException ),
                                    typeof( PathTooLongException ),
                                    typeof( DirectoryNotFoundException ),
                                    typeof( UnauthorizedAccessException ),
                                    typeof( FileNotFoundException ),
                                    typeof( NotSupportedException ),
									typeof( IOException ) };
                if( rgErrors.IsUnhandled( oEx ))
                    throw;
                
                // used to throw, but I'm using this in an enumerator, so...
                return null;
            }
        }
    }

    /// <summary>
    /// Use this for static images. That is the image you want to display is
    /// allocated and you just want to show a portion of it. This class has
    /// members to create the image you want to display.
    /// </summary>
    public class LayoutImageReference : LayoutImageAbstract {
		public virtual SKBitmap  Picture { get; }

        public bool Stretch { get; set; } = false;

		public LayoutImageReference( SKSize szSize, CSS eLayout = CSS.None ) : base( eLayout ) {
			World.SetRect( 0, 0, (int)szSize.Width, (int)szSize.Height );

            this.SetRect( LOCUS.UPPERLEFT, 0, 0, (int)szSize.Width, (int)szSize.Height );
		}

        /// <summary>
        /// Create a rescaled image based on this object's dimensions, preserving
        /// aspect ratio of the original image.
        /// </summary>
        public SKBitmap CreateReScaledImage( SKBitmap bmpSource ) {
            if( bmpSource == null )
                return null;

            try {
                World.SetRect( 0, 0, bmpSource.Width, bmpSource.Height );

                ViewPortSizeMax( ViewPort );

                SKBitmap oReSized = new SKBitmap( ViewPort.Width, ViewPort.Height );

                using( SKCanvas skCanvas = new SKCanvas( oReSized ) ) {
                    using( SKPaint skPaint = new SKPaint() ) {
                        skPaint .FilterQuality = SKFilterQuality.High;
                        skCanvas.DrawBitmap( bmpSource, 
                                             new SKRect( 0, 0, World.Width,    World.Height ),    // Source.
                                             new SKRect( 0, 0, oReSized.Width, oReSized.Height ), // Destination
                                             skPaint );
                    }
                }
                return oReSized;
            } catch( NullReferenceException ) {
                return null;
            }
        }

		protected void PaintBackground( SKCanvas skCanvas, SKPoint pntTopLeft ) {
			try {
				using( SKPaint skPaint = new SKPaint() ) {
					skPaint .Color = SKColors.White;
					skCanvas.DrawRect( new SKRect( ViewPort.Left, ViewPort.Top - pntTopLeft.Y, ViewPort.Right, ViewPort.Bottom - pntTopLeft.Y ), skPaint ); 
				}
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( ArgumentNullException ),
									typeof( ArgumentException ),
									typeof( NullReferenceException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;
			}
		}

		/// <summary>
		/// This is the normal case paint operation.
		/// </summary>
		/// <param name="skCanvas"></param>
		public override void Paint( SKCanvas skCanvas ) {
            if( Picture == null )
                return;

            if( !Stretch ) {
                ViewPortSizeMax( ViewPort ); // TODO: Need to look at placement of this call...
            } else {
                ViewPort.Copy = this;
            }

            try {
				skCanvas.DrawBitmap( Picture, 
									 new SKRect(    World.Left,    World.Top,    World.Right,    World.Bottom ),
									 new SKRect( ViewPort.Left, ViewPort.Top, ViewPort.Right, ViewPort.Bottom ) );
            } catch( NullReferenceException ) {
            }
		}
    } // End Class

    /// <summary>
    /// Use this where the bitmap is owned by another object. We
    /// don't attempt to delete the bitmap when we are "disposed"
    /// </summary>
    public class LayoutIcon : LayoutImageReference {
		public override SKBitmap Picture { get; }
        public LayoutIcon( SKBitmap skBmp, CSS eLayout = CSS.None) : 
            base(new SKSize( skBmp.Width, skBmp.Height), eLayout) 
        {
            Picture = skBmp;
        }
    }

	public class LayoutImage : LayoutImageReference, IDisposable {
		public override SKBitmap Picture { get; }

        private bool disposedValue;

        public LayoutImage( SKBitmap oImage, CSS eLayout = CSS.None ) : base( new SKSize( oImage.Width, oImage.Height ), eLayout )
		{
		    Picture  = oImage ?? throw new ArgumentNullException(); // DirectoryRect doesn't have an image.
			World.SetRect( 0, 0, (int)oImage.Width, (int)oImage.Height );
		}

		public LayoutImage( SKSize szSize ) : base( szSize ) {
			World.SetRect( 0, 0, (int)szSize.Width, (int)szSize.Height );
		}

        protected virtual void Dispose( bool disposing ) {
            if (!disposedValue) {
                if (disposing) {
					if( Picture != null )
						Picture.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~LayoutImage()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose() {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    } // End Class
}
