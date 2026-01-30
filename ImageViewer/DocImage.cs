using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;

using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Rectangles;
using Play.Edit;

namespace Play.ImageViewer {

    public abstract class Block : SmartRect {
        public LOCUS    Locus  { get; }
        public SKPointI Origin { get; }
        public double   Scale  { get; }

        public SKColor  Color  { get; set; }

        public Block( LOCUS eOrigin, int iX, int iY, double dblScale ) {
            if( dblScale < 0 || dblScale > 100 )
                throw new ArgumentOutOfRangeException( "Values 0 through 100" );

            Locus  = eOrigin;
            Origin = new( iX, iY );
            Scale  = dblScale / 100;
        }

        /// <remarks>
        /// b/c we could potentially have multiple Fonts/Faces on a single
        /// line, it doesn't make sense to pass a single render but the
        /// whole font engine so you can do whatever you want.
        /// </remarks>
        /// <param name="oStdUI"></param>
        public abstract void Update( IPgStandardUI2 oStdUI, Size szExtent );
    }

    public abstract class ImageBlock : Block {
        public ImageBlock( LOCUS eOrigin, int iX, int iY, double dblScale ) : base( eOrigin, iX, iY, dblScale ) {
        }

        /// <summary>
        /// Set our rectangle to be located in one of the corners of the canvas.
        /// This is the target ouf our blit operation.
        /// </summary>
        /// <remarks>Note, we no nolonger use the ViewPortSizeMax in an attempt
        /// to preserve the aspect ratio of the original image. It's best to
        /// just squash it in, and the use the image selector in order to
        /// select the source for the bitmap blit.</remarks>
        /// <param name="oStdUI">Baggage for this call, just ignore.</param>
        /// <param name="szTrgExtent">The size of the composite image</param>
        /// <seealso cref="LayoutImageReference.ViewPortSizeMax"/>
        public override void Update( IPgStandardUI2 oStdUI, Size szTrgExtent ) {
            SmartRect rcCanvas = new SmartRect( 0, 0, szTrgExtent.Width, szTrgExtent.Height );
            SKPointI  pnOrigin = rcCanvas.GetPoint( Locus );
            Size      szTarget = new Size((int)(szTrgExtent.Width * Scale), (int)(szTrgExtent.Height * Scale) );
            SmartRect rcViewPt = new();

            if( ( Locus & LOCUS.RIGHT ) != 0 )
                pnOrigin.X -= Origin.X;
            else
                pnOrigin.X += Origin.X;

            if( ( Locus & LOCUS.BOTTOM ) != 0 )
                pnOrigin.Y -= Origin.Y;
            else
                pnOrigin.Y += Origin.Y;

            // Move the origin to the correct corner.
            rcViewPt.SetRect( Locus, pnOrigin.X, pnOrigin.Y, szTarget.Width, szTarget.Height );
            
            this.Copy = rcViewPt;
        }

        abstract protected Size SrcExtent { get; }
    }

    public class SoloImgBlock : ImageBlock {
        public readonly SKBitmap _oSoloImg;
        public readonly SKRectI  _rcWorld;
        public SoloImgBlock( LOCUS eOrigin, int iX, int iY, double dblScale, 
                             SKBitmap oSoloImg, SmartRect rcWorldSelection ) : 
            base( eOrigin, iX, iY, dblScale ) 
        {
            _oSoloImg = oSoloImg; // We check for null
            _rcWorld  = rcWorldSelection.SKRect;
        }

        protected override Size SrcExtent {
            get {
                // This can happen if we go to a directory with no images.
                if( _oSoloImg == null )
                    return new Size( 0, 0 );
                    
                return new Size( _rcWorld.Width, _rcWorld.Height );
            }
        }

        public override void Paint( SKCanvas skCanvas ) {
            base.Paint(skCanvas);

            using SKPaint skPaint = new() { BlendMode = SKBlendMode.SrcATop, IsAntialias = true };
            try {
                // Would be nice if we could tell if the image was disposed too,
                // we're holding references to external bitmaps.
                if( _oSoloImg != null ) {
                    skCanvas.DrawBitmap( _oSoloImg, _rcWorld, SKRect, skPaint );
                }
                // else draw an error image.
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentNullException ),
									typeof( ArgumentException ),
									typeof( NullReferenceException ),
									typeof( OverflowException ),
									typeof( AccessViolationException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
            }
        }
    }

    /// <summary>
    /// Makes a bar of color on the left, right, top or bottom portion of the composited image.
    /// </summary>
    public class GradientBlock : Block {
        SKColor[] _rgColors;
        float  [] _rgPositions;

        public GradientBlock( LOCUS eOrigin, double dblScale, SKColor clrFrom, SKColor clrTo ) : base( eOrigin, 0, 0, dblScale ) {
            _rgColors    = new SKColor[] { clrFrom, clrTo };
            _rgPositions = new float  [] { 0, 1 };
        }

        public override void Update(IPgStandardUI2 oStdUI, Size szCanvas) {
            SmartRect rcCanvas = new SmartRect( 0, 0, szCanvas.Width, szCanvas.Height );
            SKPointI  pnOrigin = rcCanvas.GetPoint( Locus );
            Size      szTarget = new Size((int)(szCanvas.Width * Scale), (int)(szCanvas.Height * Scale) );
            SmartRect rcViewPt = new();

            switch( Locus ) {
                case LOCUS.LEFT:
                    rcViewPt.SetRect( LOCUS.LEFT  | LOCUS.TOP,    0, 0, szTarget.Width, szCanvas.Height );
                    break;
                case LOCUS.RIGHT:
                    rcViewPt.SetRect( LOCUS.RIGHT | LOCUS.TOP,    szCanvas.Width, 0, szTarget.Width, szCanvas.Height );
                    break;
                case LOCUS.TOP:
                    rcViewPt.SetRect( LOCUS.LEFT  | LOCUS.TOP,    0, 0, szCanvas.Width, szTarget.Height );
                    break;
                case LOCUS.BOTTOM:
                    rcViewPt.SetRect( LOCUS.LEFT  | LOCUS.BOTTOM, 0, szCanvas.Height, szCanvas.Width, szTarget.Height );
                    break;
            }
            
            this.Copy = rcViewPt;
        }

        public override void Paint( SKCanvas skCanvas ) {
            using SKPaint skPaint = new() { BlendMode = SKBlendMode.SrcATop, IsAntialias = true };

            // Create linear gradient from left to Right
            skPaint.Shader = SKShader.CreateLinearGradient(
                                new SKPoint( Left,  Top),
                                new SKPoint( Right, Bottom),
                                _rgColors,
                                _rgPositions,
                                SKShaderTileMode.Repeat );

            try {
                skCanvas.DrawRect( Left, Top, Width, Height, skPaint );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentNullException ),
									typeof( ArgumentException ),
									typeof( NullReferenceException ),
									typeof( OverflowException ),
									typeof( AccessViolationException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
            }
        }
    }

    public class TextBlock : Block {
        public uint   FontID { get; protected set; }
        public ushort FaceID { get; set; }
        public FTCacheWrap   CacheElem  { get; }

        public TextBlock( LOCUS eOrigin, int iX, int iY, double dblScale, Line oLine ) : base( eOrigin, iX, iY, dblScale ) {
            CacheElem = new FTCacheWrap( oLine );
        }

        protected override void OnSize() {
            base.OnSize();
            CacheElem.OnChangeSize( Width );
        }

        /// <summary>
        /// Whenever the text changes, you need to call this function to
        /// re-measure the text elements.
        /// </summary>
        public override void Update( IPgStandardUI2 oStdUI, Size szExtent ) {
            SmartRect  rcScratch = new SmartRect( LOCUS.UPPERLEFT, 0, 0, szExtent.Width, szExtent.Height );

            rcScratch.SetPoint( SET.STRETCH, Locus, Origin.X, Origin.Y );
            
            this.Copy = rcScratch;

            // The height of the text is about the height of the box.
            SKPoint sRez     = new SKPoint( 96, 96 ); 
            uint    uiHeight = (uint)(szExtent.Height / sRez.Y * 72 * Scale );

            FontID = oStdUI.FontCacheNew( FaceID, uiHeight, sRez );

            CacheElem.Measure( oStdUI.FontRendererAt( FontID ) );
            OnSize();
        }

        public override void Paint( SKCanvas skCanvas ) {
            using SKPaint skPaint = new SKPaint() { Color = this.Color };

            CacheElem.Render( skCanvas, skPaint, new PointF( Left, Top ) );
        }
    }

    /// <summary>
    /// This is an image compositor object. Add items to the
    /// collection and it will render them to the main bitmap on this
    /// document.
    /// </summary>
    public class DocImageEdit : 
        ImageSoloDoc,
        IEnumerable<SmartRect>
    {
        protected          bool           _fDisposed = false;
        protected readonly IPgStandardUI2 _oStdUI;
        protected readonly SKPaint        _skPaint   = new SKPaint();
        public ushort StdFace { get; protected set; }

        public Editor Text   { get; } // Keep this for the external Templates.
        public Editor Layers { get; }

        public class DocSite : 
			IPgBaseSite
		{
			readonly DocImageEdit _oDoc;

			/// <summary>
			/// This is for our editor instance we are hosting!!
			/// </summary>
			public DocSite( DocImageEdit oDoc ) {
				_oDoc = oDoc ?? throw new ArgumentNullException( "Image document must not be null." );
			}

			public void LogError( string strMessage, string strDetails, bool fShow=true ) {
				_oDoc.LogError( strMessage, "ImageWalker : " + strDetails );
			}

			public void Notify( ShellNotify eEvent ) {
			}

			public IPgParent Host => _oDoc;
		}

        public DocImageEdit(IPgBaseSite oSiteBase) : base(oSiteBase) {
            _oStdUI = (IPgStandardUI2)Services ?? throw new ApplicationException( "Couldn't get StdUI2" );
            Text    = new Editor( new DocSite( this ) );
            Layers  = new Editor( new DocSite( this ) );
        }

        protected override bool Initialize() {
            if( !base.Initialize() )
                return false;

            if( !Text.InitNew() )
                return false;
            if( !Layers.InitNew() )
                return false;

            try {
                StdFace = _oStdUI.FaceCacheNew(  @"C:\windows\fonts\impact.ttf" );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( InvalidOperationException ),
                                    typeof( NullReferenceException ),
                                    typeof( ArgumentNullException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                LogError( "Image composite", "Could not init Text handlers." );
                return false;
            }

            return true;
        }

        public override bool InitNew() {
            return base.InitNew();
        }

        public override void Dispose() {
            if( !_fDisposed ) {
                _skPaint.Dispose();
                _fDisposed = true;
            }
            base.Dispose();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="szDestSize"></param>
        /// <returns></returns>
		public bool Load( SKSizeI szDestSize ) {
            if( Bitmap != null && Bitmap.Width == szDestSize.Width && Bitmap.Height == szDestSize.Height ) {
                // Kind of weird I need to do this since I always render after the bitmap
                // is loaded. Take another look at this some time.
				Raise_ImageUpdated();
                return true;
            }

			BitmapDispose();

			try {
				Bitmap = new SKBitmap( szDestSize.Width, szDestSize.Height, SKColorType.Rgba8888, SKAlphaType.Opaque );
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( ArgumentException ),
									typeof( ArgumentNullException ),
									typeof( NullReferenceException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				return false;
			} finally {
				Raise_ImageUpdated();
			}

			return true;
		}

        protected void LogError( string strMessage, string strDetails, bool fShow=true ) {
            _oSiteBase.LogError( strMessage, strDetails, fShow );
        }

        public void AddImage( LOCUS eOrigin, int iX, int iY, double dblSize, SKBitmap oSoloBmp, 
            SmartRect rcWorldSelect
        ) {
            if( rcWorldSelect == null )
                rcWorldSelect = new SmartRect( 0, 0, oSoloBmp.Width, oSoloBmp.Height );

            SoloImgBlock oBlock = new( eOrigin, iX, iY, dblSize, oSoloBmp, rcWorldSelect );
            Line         oLayer = Layers.LineAppend( "Image" );

            oLayer.Extra = oBlock;
        }

        public void AddGradient( LOCUS eOrigin, double dblSize, SKColor clrFrom, SKColor clrTo ) {
            GradientBlock oBlock = new( eOrigin, dblSize, clrFrom, clrTo );
            Line          oLayer = Layers.LineAppend( "Gradient" );

            oLayer.Extra = oBlock;
        }

        public void AddText( LOCUS eOrigin, int iX, int iY, double dblSize, ushort uFaceID, SKColor skColor, string strText = "" ) {
            Line      oLayer = Layers.LineAppend( strText );
            TextBlock oBlock = new( eOrigin, iX, iY, dblSize, oLayer ) { FaceID = uFaceID, Color = skColor };

            oLayer.Extra = oBlock; // Circular ref, we'll see how the mem man handles this.
        }

        public void AddLayout( LayoutRect oLayout ) {
            Line oLayer = Layers.LineAppend( "Layout" );
            oLayer.Extra = oLayout;
        }

        /// <summary>
        /// If we were a view we would listen in on the ImageUpdated event.
        /// But since we're the owning object, I'll simply use the override.
        /// </summary>
        public override void Raise_ImageUpdated() {
            foreach( SmartRect oRect in this ) {
                oRect.LayoutChildren();
            }

            base.Raise_ImageUpdated();
        }

        /// <summary>
        /// Take all the block objects and paint them onto the bitmap hosted by
        /// this class.
        /// </summary>
        public void RenderImage( ) {
            // TODO: As we are going to be rendering a lot I should probably keep
            // a canvas on the image. But that will add complexity so I'll
            // implement that later.
            using SKCanvas skCanvas = new SKCanvas( Bitmap );
            using SKPaint  skPaint  = new SKPaint() { Color = SKColors.Beige };

            try {
                // Note: My text renderer won't make visible text if the bg is transparent!!
                skCanvas.DrawRect( new SKRect( 0, 0, Bitmap.Width, Bitmap.Height ), skPaint );

                Size szExtent = new Size( Bitmap.Width, Bitmap.Height );

                // Clunky but we won't have a lot of objects in here.
                foreach( SmartRect oRect in this ) {
                    //if( oRect is TextBlock oTextBlock ) {
                    //    Text.WordBreak( oTextBlock.CacheElem.Line, oTextBlock.CacheElem.Words ); 
                    //}
                    if( oRect is Block oBlock ) {
                        oBlock.Update( _oStdUI, szExtent );
                    } else {
                        oRect.SetRect( 0, 0, szExtent.Width, szExtent.Height );
                    }
                    oRect.LayoutChildren();
                }

                foreach( SmartRect oRect in this ) {
                    oRect.Paint ( skCanvas );
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentNullException ),
									typeof( ArgumentException ),
									typeof( NullReferenceException ),
									typeof( OverflowException ),
									typeof( AccessViolationException ),
                                    typeof( InvalidCastException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
            }
        }

        public void Clear() {
            Layers.Clear();
            Text  .Clear();
        }

        /// <summary>
        /// Return the layers in reverse order of the Editor EditWin
        /// display. This iterator is basically for the use of the composite
        /// compiler.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidCastException">Something in the collection is not
        /// derived from Block class.</exception>
        public IEnumerator<SmartRect> GetEnumerator() {
            Line[] _rgLines = new Line[Layers.ElementCount];

            // Make a copy just in case somebody monkies with the Layers mid pass.
            int iLength = _rgLines.Length;
            for( int i = 0; i < iLength; i++ ) {
                _rgLines[iLength-i-1] = Layers[i];
            }
            for( int i = 0; i < _rgLines.Length; i++ ) {
                yield return (SmartRect)_rgLines[i].Extra;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
    }
}
