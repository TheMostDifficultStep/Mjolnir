using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Reflection;
using System.Linq;
using System.Web;
using System.Text;

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

    public class ImageBlock : Block {
        public readonly ImageSoloDoc _oDocSoloImg;
        public ImageBlock( LOCUS eOrigin, int iX, int iY, double dblScale, ImageSoloDoc oDocSoloImg ) : base( eOrigin, iX, iY, dblScale ) {
            _oDocSoloImg = oDocSoloImg ?? throw new ArgumentNullException( nameof( oDocSoloImg ) );
        }
        public override void Update( IPgStandardUI2 oStdUI, Size szExtent ) {
            SmartRect  rcScratch = new SmartRect( LOCUS.UPPERLEFT, 0, 0, szExtent.Width, szExtent.Height );
            SKPointI   pnOrigin  = rcScratch.GetPoint( Locus );

            pnOrigin.X += Origin.X;
            pnOrigin.Y += Origin.Y;

            rcScratch.SetRect( Locus, pnOrigin.X, pnOrigin.Y, (int)(szExtent.Width * Scale), (int)(szExtent.Height * Scale) );
            
            this.Copy = rcScratch;
        }

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
    public class TextBlock : Block {
        public uint   FontID { get; protected set; }
        public ushort FaceID { get; set; }
        public FTCacheWrap   CacheElem  { get; }

        public TextBlock( LOCUS eOrigin, int iX, int iY, double dblScale, int iId ) : base( eOrigin, iX, iY, dblScale ) {
            CacheElem = new FTCacheWrap( new TextLine( iId, string.Empty ) );
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

            // BUG: We're not subtracting the origin offset from the width/height.
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
    /// This is an image compositor object. You will add items to it's
    /// collection and it will render them to the main image on this
    /// document.
    /// </summary>
    public class DocImageEdit : 
        ImageSoloDoc,
        IEnumerable<SmartRect>
    {
        protected          bool            _fDisposed = false;
        protected readonly List<Block>     _rgChildren = new ();
        protected int                      _iID = 0;
        protected readonly IPgStandardUI2  _oStdUI;
        protected readonly SKPaint         _skPaint = new SKPaint();
        public ushort StdFace { get; protected set; }

        public DocImageEdit(IPgBaseSite oSiteBase) : base(oSiteBase) {
            _oStdUI = (IPgStandardUI2)Services;
        }

        public override bool Initialize() {
            if( !base.Initialize() )
                return false;

            //StdFace = _oStdUI.FaceCache(  @"C:\windows\fonts\consola.ttf" );
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

        public int Count => _rgChildren.Count;

        public bool IsReadOnly => false;

        /// <summary>
        /// TODO: change the bitmap to a ImageSoloDoc.
        /// </summary>
        /// <param name="oRectDest"></param>
        /// <param name="skBitmap"></param>
        public void AddImage( LOCUS eOrigin, int iX, int iY, double dblSize, ImageSoloDoc oSoloBmp ) {
            ImageBlock oBlock = new ImageBlock( eOrigin, iX, iY, dblSize, oSoloBmp );

            _rgChildren.Add( oBlock );
        }

        public TextBlock AddText( LOCUS eOrigin, int iX, int iY, double dblSize, ushort uFaceID, string strText = "" ) {
            TextBlock oNew = new( eOrigin, iX, iY, dblSize, _iID++ ) { Text = strText, FaceID = uFaceID };
            
            _rgChildren.Add( oNew );

            return oNew;
        }

        /// <summary>
        /// If we were a view we would listen in on the ImageUpdated event.
        /// But since we're the owning object, I'll simply use the override.
        /// </summary>
        public override void Raise_ImageUpdated() {
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
        }

        public Block ItemAt( int iIndex ) {
            return _rgChildren[ iIndex ];
        }

        public void RemoveAt( int iIndex ) {
            _rgChildren.RemoveAt( iIndex );
        }

        public IEnumerator<SmartRect> GetEnumerator() {
            return _rgChildren.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
    }
}
