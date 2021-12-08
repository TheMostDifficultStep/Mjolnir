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

        public override void Update( IPgStandardUI2 oStdUI, Size szTrgExtent ) {
            SmartRect rcCanvas = new SmartRect( 0, 0, szTrgExtent.Width, szTrgExtent.Height );
            SKPointI  pnOrigin = rcCanvas.GetPoint( Locus );
            SmartRect rcBitmap = new SmartRect( 0, 0, SrcExtent.Width, SrcExtent.Height );
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

            ImageHelpers.ViewPortSizeMax( new Size( 0, 0 ), szTarget, rcBitmap, rcViewPt );

            // Move the origin to the correct corner.
            rcViewPt.SetRect( Locus, pnOrigin.X, pnOrigin.Y, rcViewPt.Width, rcViewPt.Height );
            
            this.Copy = rcViewPt;
        }

        abstract protected Size SrcExtent { get; }
    }

    public class SoloImgBlock : ImageBlock {
        public readonly ImageSoloDoc _oDocSoloImg;
        public SoloImgBlock( LOCUS eOrigin, int iX, int iY, double dblScale, ImageSoloDoc oDocSoloImg ) : base( eOrigin, iX, iY, dblScale ) {
            _oDocSoloImg = oDocSoloImg ?? throw new ArgumentNullException( nameof( oDocSoloImg ) );
        }

        protected override Size SrcExtent => new Size( _oDocSoloImg.Bitmap.Width, _oDocSoloImg.Bitmap.Height );

        public override void Paint( SKCanvas skCanvas ) {
            base.Paint(skCanvas);

            using SKPaint skPaint = new() { BlendMode = SKBlendMode.SrcATop, IsAntialias = true };

            try {
                skCanvas.DrawBitmap( _oDocSoloImg.Bitmap,
									 new SKRect( 0, 0, _oDocSoloImg.Bitmap.Width, _oDocSoloImg.Bitmap.Height ),
									 SKRect,
									 skPaint );
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
            CacheElem.WrapSegments( Width );
        }

        /// <summary>
        /// Whenever the line length / Container width changes
        /// you need to call this function to re-wrap the characters.
        /// Not really needed if the text does not wrap. But most text does.
        /// </summary>
        /// <returns></returns>
        public override bool LayoutChildren() {
            CacheElem.WrapSegments( Width );

            return true;
        }

        public string Text { 
            set { CacheElem.Line.TryAppend( value ); }
        }

        /// <summary>
        /// Whenever the text changes, you need to call this function to
        /// re-measure the text elements.
        /// </summary>
        public override void Update( IPgStandardUI2 oStdUI, Size szExtent ) {
            SmartRect  rcScratch = new SmartRect( LOCUS.UPPERLEFT, 0, 0, szExtent.Width, szExtent.Height );
            SKPointI   pnOrigin  = rcScratch.GetPoint( Locus );

            pnOrigin.X += Origin.X;
            pnOrigin.Y += Origin.Y;

            rcScratch.SetRect( Locus, pnOrigin.X, pnOrigin.Y, szExtent.Width, szExtent.Height );
            
            this.Copy = rcScratch;

            SKSize    sResolution = new SKSize(72, 72); 
            uint      uiHeight    = (uint)(szExtent.Height * Scale );

            FontID = oStdUI.FontCache( FaceID, uiHeight, sResolution);

            CacheElem.Update( oStdUI.FontRendererAt( FontID ) );
            LayoutChildren();
        }

        public override void Paint( SKCanvas skCanvas ) {
            using SKPaint skPaint = new SKPaint() { Color = SKColors.Red };

            CacheElem.Render( skCanvas, skPaint, new PointF( Left, Top ) );
        }
    }

    /// <summary>
    /// This is an image compositor object. Add items to the
    /// collection and it will render them to the main bitmap on this
    /// document.
    /// </summary>
    public class DocImageEdit : 
        ImageSoloDoc
    {
        protected          bool            _fDisposed  = false;
        protected readonly List<Block>     _rgChildren = new ();
        protected readonly IPgStandardUI2  _oStdUI;
        protected readonly SKPaint         _skPaint = new SKPaint();
        public ushort StdFace { get; protected set; }

        public Editor Text { get; }

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
            _oStdUI = (IPgStandardUI2)Services;
            Text    = new Editor( new DocSite( this ) );
        }

        protected override bool Initialize() {
            if( !base.Initialize() )
                return false;

            StdFace = _oStdUI.FaceCache(  @"C:\windows\fonts\impact.ttf" );

            return true;
        }

        public override void Dispose() {
            if( !_fDisposed ) {
                _skPaint.Dispose();
                _fDisposed = true;
            }
            base.Dispose();
        }

        protected void LogError( string strMessage, string strDetails, bool fShow=true ) {
            _oSiteBase.LogError( strMessage, strDetails, fShow );
        }


        public void AddImage( LOCUS eOrigin, int iX, int iY, double dblSize, ImageSoloDoc oSoloBmp ) {
            SoloImgBlock oBlock = new( eOrigin, iX, iY, dblSize, oSoloBmp );

            _rgChildren.Add( oBlock );
        }

        public void AddGradient( LOCUS eOrigin, double dblSize, SKColor clrFrom, SKColor clrTo ) {
            GradientBlock oBlock = new( eOrigin, dblSize, clrFrom, clrTo );

            _rgChildren.Add( oBlock );
        }

        public void AddText( LOCUS eOrigin, int iX, int iY, double dblSize, ushort uFaceID, string strText = "" ) {
            Line      oLine  = Text.LineAppend( strText, fUndoable:false );
            TextBlock oBlock = new( eOrigin, iX, iY, dblSize, oLine ) { FaceID = uFaceID };
            
            _rgChildren.Add( oBlock );
        }

        /// <summary>
        /// If we were a view we would listen in on the ImageUpdated event.
        /// But since we're the owning object, I'll simply use the override.
        /// </summary>
        protected override void Raise_ImageUpdated() {
            LayoutChildren();

            base.Raise_ImageUpdated();
        }

        public void LayoutChildren() {
            foreach( Block oBlock in _rgChildren ) {
                oBlock.LayoutChildren();
            }
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

                foreach( Block oBlock in _rgChildren ) {
                    oBlock.Update( _oStdUI, szExtent );
                    oBlock.Paint ( skCanvas );
                }
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

        public void Clear() {
            _rgChildren.Clear();
            Text       .Clear();
        }
    }
}
