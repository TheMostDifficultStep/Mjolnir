using Play.Drawing;
using Play.ImageViewer;
using Play.Interfaces.Embedding;

using SkiaSharp;
using System.Reflection;
using System.Xml;

namespace Play.Spectrum {
    public class Attribute {
        public bool _fFlash;
        public bool _fBright;
        public byte _bInk;
        public byte _bPaper;

        public Attribute( byte iAttr ) {
            Value = iAttr;
        }

        public byte Value { 
            set {
                _fFlash  = ( value & 0x80 ) > 0;
                _fBright = ( value & 0x40 ) > 0;
                _bPaper  = (byte)(( value & 0x38 ) >> 3 );
                _bInk    = (byte)(value & 0x7 );
            }
        }
    }

    public class Block {
        public Block( int iImg, Attribute oAttr ) {
            Img  = iImg;
            Attr = oAttr;
        }

        public Attribute Attr { get; set; }
        public int       Img  { get; set; }
    }
    public class SpectrumGraphics :
        DocSurfaceBase,
        IPgLoad
    {
        public Block[,]  Screen  { get; } // Speccy 32,24 ascii display.
        public SKImage[] Images  { get; } // Our constructed UDG's

        public Attribute Attr    { get;set; } = new Attribute(0);

        public SpectrumGraphics( IPgBaseSite oSite, string strMode ) : base( oSite ) {
            if( string.Compare( strMode, "std" ) != 0 ) {
                throw new ArgumentOutOfRangeException();
            }
            Screen  = new Block  [32,24];
            Images  = new SKImage[256];
            Surface = SKSurface.Create( new SKImageInfo( 256, 192, SKColorType.Bgra8888 ) );
        }

        /// <summary>
        /// Fill out our screen black.
        /// </summary>
        public virtual bool InitNew() {
            SetGraphic2( 0, [0,0,0,0,0,0,0,0] ); // Gives us a solid block.

            for( int iY = 0; iY< Screen.GetLength(1); ++iY ) {
                for( int iX = 0; iX < Screen.GetLength(0); ++iX ) {
                    // Attribute 0 is black foreground and background.
                    Screen[iX, iY] = new Block(0, new Attribute( 0 ) );
                }
            }

            return true;
        }

        public void LogError( string strMessage ) {
            _oSiteBase.LogError( "Spectrum", strMessage );
        }

        /// <summary>Create the Image that backs the Graphics 
        /// block. I'll set them in flat and you as the programmer
        /// will set the UDG's starting at 0x90</summary>
        /// <remarks>
        /// In the future, I'll make a bulk loader so I can just
        /// create the Scratch surface during the load.
        /// I've depricated this code. But it might work with the
        /// new rendering code by set the color as 
        /// SKColor( FF, FF, FF, bByteValue );
        /// </remarks>
        public void SetBWGraphic( SKSurface oSurface, int i, byte[] rgUdg ) {
            ArgumentNullException.ThrowIfNull( rgUdg ); 

            for( int iY = 0; iY<8; ++iY ) {
                byte bRow = rgUdg[iY];
                for( int iX = 0; iX < 8; ++iX ) {
                    // Highest bit is the lowest X value...
                    SKColor sColor = ( bRow & 1<<(7-iX) ) > 0 ? SKColors.White : SKColors.Black;

                    Surface.Canvas.DrawPoint( iX, iY, sColor );
                }
            }
            Images[i] = oSurface.Snapshot();
        }

        public void SetGraphic2( int i, byte[] rgUdg ) {
            using SKBitmap skBitmap = new SKBitmap( 8, 8, SKColorType.Alpha8, SKAlphaType.Opaque );

            for( int iY = 0; iY<8; ++iY ) {
                byte bRow = rgUdg[iY];
                for( int iX = 0; iX < 8; ++iX ) {
                    // Highest bit is the lowest X value...
                    byte bByteValue = ( bRow & 1<<(7-iX) ) == 0 ? (byte)0 : (byte)255; 

                    skBitmap.SetPixel( iX, iY, new SKColor( 0, 0, 0, bByteValue ));
                }
            }
            Images[i] = SKImage.FromBitmap( skBitmap );
        }

        /// <summary>
        /// Set's a udg character on screen. 
        /// </summary>
        /// <param name="iRow"></param>
        /// <param name="iCol"></param>
        /// <param name="cUdg">Graphic block starting at 'A'</param>
        public void PutUDGAt( int iRow, int iCol, char cUdg ) {
            int iUdg = (byte)( (Int16)cUdg - 'A' + 0x90);

            PutChar( iRow, iCol, (char)iUdg );
        }

        public void PutChar( int iRow, int iCol, char cChar ) {
            try {
                Block oBlock = Screen[iCol, iRow];

                oBlock.Img  = cChar;
                oBlock.Attr = Attr;
            } catch( Exception oEx ) {
                Type[] rgErrors = [ 
                    typeof( NullReferenceException ),
                    typeof( IndexOutOfRangeException ) ];

                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                LogError( "Couldn't set UDG on screen." );
            }
        }

        protected SKColor GetColor( int iR, int iG, int iB ) {
            return new SKColor( (byte)iR, (byte)iG, (byte)iB );
        }

        /// <summary>
        /// The spectrum zx is a GRB 1 bit color palette!
        /// </summary>
        /// <param name="iCode">0-7</param>
        private SKColor DecodeColor( bool iIntensity, byte iCode)
        {
            int iMono  = iIntensity ? 0xFF : 0xCD; // Either bright or normal.

            int g = ( ( iCode & 0x04 ) != 0 ) ? iMono : 0;
            int r = ( ( iCode & 0x02 ) != 0 ) ? iMono : 0;
            int b = ( ( iCode & 0x01 ) != 0 ) ? iMono : 0;

            // Special case: true black when all RGB bits are 0
            if (r == 0 && g == 0 && b == 0) 
                return SKColors.Black;

            return GetColor( r, g, b );
        }

        /// <summary>
        /// The image must be an Alpha8 with the bits 1 to show and the
        /// alpha channel 1 to show 0 not to show.
        /// </summary>
        /// <param name="oCanvas"></param>
        /// <param name="oPaint">Set color attribute you want for the image.</param>
        /// <param name="oRect">X, Y position of image. W&H s/b same as the image.</param>
        /// <param name="oImage">the image to draw.</param>
        protected virtual void DrawImage( 
            SKCanvas oCanvas, 
            SKPaint  oPaint, 
            SKRect   oRect, 
            SKImage  oImage
        ) {
            //Not sure how to use this yet.
            SKSamplingOptions oOptions = new SKSamplingOptions( SKFilterMode.Linear );

            // So XOR only works with alpha, which explains why my
            // Alpha8 bitmap works with this.
            oPaint .BlendMode = SKBlendMode.Xor;
            oCanvas.DrawImage( oImage, oRect.Left, oRect.Top, oOptions, oPaint );

            // So the BG is already the color we wanted, it get's XOR'd and
            // has a transparency set, then we draw our text colored rect...
            oPaint .BlendMode = SKBlendMode.DstOver;
            oCanvas.DrawRect( oRect, oPaint );
        }

        /// <summary>
        /// After you have loaded your UDG's, you may attempt
        /// to refresh and draw the screen.
        /// </summary>
        public void Refresh() {
            SKPaint  oPaint  = new SKPaint();
            SKCanvas oCanvas = Surface.Canvas;
            try {
                for( int iY = 0; iY<Screen.GetLength(1); ++iY ) {
                    for( int iX = 0; iX <Screen.GetLength(0); ++iX ) {
                        SKPoint pntLoc = new( iX*8, iY*8 );
                        Block   oBlock = Screen[iX, iY];
                        SKImage oUdg   = Images[oBlock.Img];

                        if( oUdg is null ) {
                            oUdg = Images[0];
                        }
                        //Surface.Canvas.DrawImage( oUdg, pntLoc );

                        SKRect skRect  = new SKRect( pntLoc.X, pntLoc.Y, 
                                                     pntLoc.X + oUdg.Width, 
                                                     pntLoc.Y + oUdg.Height );

                        // This sets our background image.
                        oPaint .BlendMode = SKBlendMode.Src;
                        oPaint .Color     = DecodeColor( oBlock.Attr._fBright, oBlock.Attr._bPaper );
                        oCanvas.DrawRect( skRect, oPaint );

                        // This does the XOR blit to display.
                        oPaint.Color = DecodeColor( oBlock.Attr._fBright, oBlock.Attr._bInk );
                        DrawImage( oCanvas, oPaint, skRect, oUdg );
                    }
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = [ 
                    typeof( NullReferenceException ),
                    typeof( IndexOutOfRangeException ) ];

                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                LogError( "Couldn't Refresh Screen." );
            }
        }
    }

    public class DocumentTutTut :
        IPgParent,
        IPgLoad<TextReader>,
        IPgSave,
		IDisposable
    {
        protected readonly IPgBaseSite       _oBaseSite;
        protected readonly IPgRoundRobinWork _oWorkPlace; 
        public SpectrumGraphics Speccy { get; }
        public IPgParent Parentage => _oBaseSite.Host;
        public IPgParent Services  => Parentage.Services;
        public bool      IsDirty   => false;

        public class DocSlot :
            IPgBaseSite
        {
            protected readonly DocumentTutTut _oHost;

            public DocSlot( DocumentTutTut oHost ) {
                _oHost = oHost;
            }
            public IPgParent Host => _oHost;

            public void LogError(string strMessage, string strDetails, bool fShow = true) {
                _oHost._oBaseSite.LogError(strMessage, strDetails, fShow);
            }

            public void Notify(ShellNotify eEvent) {
                _oHost._oBaseSite.Notify( eEvent );
            }
        }

        Attribute Attr {
            get { return Speccy.Attr; }
            set { Speccy.Attr = value; }
        }

        const    int   iU      = 3;
        readonly int[] rgMummy = new int[4];
        protected SKPointI Explorer { get; set; }
        protected SKPointI PrevExpl { get; set; }


        public DocumentTutTut( IPgBaseSite oBaseSite ) {
            _oBaseSite  = oBaseSite ?? throw new ArgumentNullException( nameof( oBaseSite ) );
            _oWorkPlace = ((IPgScheduler)Services).CreateWorkPlace() ?? throw new InvalidProgramException();
            Speccy      = new SpectrumGraphics( new DocSlot( this ), "std" );
        }

        public void Dispose() {
        }

        public static SKImage GetResource( string strName ) {
			Assembly oAsm   = Assembly.GetExecutingAssembly();
            string   strRes = "Play.Games.Content." + strName;

			return SKImageResourceHelper.GetImageResource( oAsm, strRes );
		}

        public bool InitNew() {
            if( !Initialize() ) 
                return false;

            return true;
        }

        public bool Load(TextReader oStream) {
            if( !Initialize() ) 
                return false;

            return true;
        }

        protected virtual bool Initialize() {
            InitUDG  ();

            LoadGrid (0);
            LoadKAndM(0);
            LoadStuff(0);
            LoadExplorer(0);

            Speccy.Refresh();

            return true;
        }

        public void InitUDG() {
            byte[][] rgUdg = [
                [127,65, 91, 67, 109, 111, 127, 0], // 'A'
                [24, 216, 76, 62, 7, 12, 20, 50],   // 'B'...
                [24, 24, 18, 126, 176, 24, 52, 38],
                [60, 36, 44, 60, 24, 16, 28, 28],
                [127, 103, 71, 107, 109, 115, 127, 0],
                [127, 99, 93, 93, 99, 65, 127, 0],
                [0, 0, 24, 36, 86, 60, 24, 0],
                [24, 60, 24, 72, 126, 24, 44, 32],
                [24, 60, 24, 19, 126, 24, 52, 4],
                [0, 8, 20, 20, 36, 34, 66, 78],
                [0, 126, 68, 64, 64, 64, 70, 126],
                [0, 126, 68, 32, 28, 32, 70, 126],
                [0, 78, 36, 40, 48, 40, 36, 66],
                [64, 48, 16, 16, 16, 16, 48, 62],
                [0, 126, 66, 66, 94, 80, 76, 66],
                [0, 126, 68, 32, 16, 8, 100, 126],
                [0, 126, 20, 16, 16, 16, 48, 56],
                [4, 66, 68, 36, 36, 40, 40, 16],
                [4, 66, 68, 68, 68, 68, 68, 124],
                [4, 66, 36, 24, 16, 16, 48, 56],
                [204, 102, 51, 153, 204, 102, 51, 153]
            ];

            Speccy.InitNew();

            for( int i=0; i<rgUdg.GetLength(0); ++i ) {
                Speccy.SetGraphic2( i+0x90, rgUdg[i] );
            }
        }

        /// <summary>
        /// Calculate the remainder (modulus)
        /// </summary>
        public static int R( int a, int b ) {
            return a - (a/b)*b;
        }

        /// <summary>
        /// Calculate the row...
        /// </summary>
        public static int M( int a, int b ) {
            return a/b;
        }

        protected void LoadGrid( int _ ) {
            string   strT    = "BABAAABBBAA";
            string[] rgGrid = [
                "44444444","67777778","45444544","45444544","45444444",
                "67777778","44444544","44444544","44444544","67777778",
                "45444444","45444444","45444544","67777778","44444444" ];
            // Fill in 15 rows where each row is iZ. Line 690
            for( int iZ = 0; iZ < rgGrid.Length ; iZ++ ) {
                string strC = string.Empty;
                string strZ = rgGrid[iZ];

                // Create a 24 character long string.
                for( int iC=0; iC < strZ.Length; ++iC ) { 
                    int iOffs = strZ[iC] - '0' - 1; // b/c because basic...
                    strC += strT[iOffs..(iOffs+3)]; // fill in 3 chars from T
                }
                // Load up that string on the screen.
                Attr = new Attribute( 114 );
                UdgAt( iZ+iU, iU+1, strC );

                // Now go back and add all the tunnles.
                Attr = new Attribute(0);
                for( int iV = 0; iV < strC.Length; ++iV ) {
                    if( strC[iV] == 'B' ) {
                        char cTmp = (char)( 0x91 + R(iV + iZ, 2) );

                        At(iZ + iU, iU + iV + 1, cTmp);
                    }
                }
            }
        }

        protected void LoadKAndM( int _ ) {
            int[] rgKeyMum = [
                0, 0, 0, 438, 50, 167, 300, 418
            ]; // 0-3 keys, 4-7 mummies
            Attr = new Attribute(0); // line 750
            for( int iZ =0; iZ < 8; ++iZ ) {
                int iC = rgKeyMum[iZ];
                // First 4 are keys, set if not zero.
                if( iZ < 4 && iC > 0 ) {
                    // Oh! Col...Row calculation!! TODO: Use r/c in future. 
                    int iV = R(iC,32) + iU + 1;
                    int iB = M(iC,32) + iU;

                    Attr = new Attribute( (byte)(iZ+0x40+1) ); // bright,paper:0,ink:z+1
                    At( iB, iV, 'D' );
                }
                // Next 4 are mummies. 2d pos encoded in one number!!
                if( iZ > 3 ) {
                    // Address 22528 marks the exact start of the attribute
                    // file (color RAM), located right at the top-left corner
                    // of the screen grid
                    rgMummy[iZ-4] = iC+22528+iU*32+iU+1-1; // b/c basic 
                    Poke( rgMummy[iZ-4], 71 ); // 0x47 bright bg:black, fg:white
                    
                    // SetAttr( iU, iU+iZ, 71 ); use this once we're running.
                    // Interesting that the UDG isn't set tho...
                }
            }

        }

        /// <summary>
        /// Adds the TV and rocks.
        /// </summary>
        /// <param name="_"></param>
        protected void LoadStuff( int _ ) {
            int[] rgItems = [
                0,  0,   0,   0,   0,
                0,  0,   0,   0,   0,
                0,  0,  33,   0,   0,
                0, 54, 161, 310, 417 
            ];

            Attr   = new Attribute(0);
            Bright = true;

            int iIndex = 0;

            for( int iZ=0; iZ<5; ++iZ ) {
                for( int iB=0; iB<4; ++iB ) {
                    int iC   = rgItems[iIndex++];
                    int iCol = R( iC, 32 )+iU+1; // modulus, %
                    int iRow = M( iC, 32 )+iU;

                    if( iC > 0 && iZ<4 ) { // line 790
                        Paper = 7;
                        Ink   = (byte)(iZ+1); //+1 b/c basic.

                        At( iRow, iCol, 'E' );

                        if( iZ == 3 ) {
                            At( iRow, iCol, 'F' ); // Green tv/white bg
                        }
                    }
                    if( iC > 0 && iZ > 3 ) {
                        Attr = new Attribute( 6 );

                        At( iRow, iCol, 'G' );
                    }
                }
            }
        }

        protected void LoadExplorer( int _ ) {
            int    iVal       = 113;
            string strMessage = @"H\tPOG\l\sM";

            Explorer = new SKPointI( R(iVal,32)+iU+1, M(iVal,32)+iU );
            PrevExpl = Explorer;
            //int iQ = iY;
            //int iW = iX;

            DrawExplorer(); // Call 225

            // This is the first place we have a mixed string of UDG's
            // and standard ASCII... I think!
            List<char> rgTx = new List<char>();
            for( int i=0; i< strMessage.Length; ++i ) {
                if( strMessage[i] == '\\' ) {
                    ++i;
                    rgTx.Add( (char)(0x90 + char.ToUpper( strMessage[i] ) ) );
                } else {
                    rgTx.Add( strMessage[i] );
                }
            }

            // Put something in the middle of the screen.
            Attr = new Attribute( 66 );
            int iHalf = rgTx.Count / 2;
            for( int i=0; i< rgTx.Count; ++i ) {
                Speccy.PutChar( iU+16,iU+12-iHalf+i, rgTx[i] );
            }
        }

        protected void DrawExplorer( ) {
            Attr = new Attribute(0);

            // I don't think I need to keep this persistant, it's
            // just being used to determine the direction the char
            // is going.
            int C = R( PrevExpl.X + PrevExpl.Y, 2 );

            if( C != 0 ) {
                At( PrevExpl, 'B' );
                Attr.Value = 69;
                At( Explorer, 'I' );
            } else {
                At( PrevExpl, 'C' );
                Attr.Value = 69;
                At( Explorer, 'H' );
            }

            PrevExpl = Explorer;
        }

        const int iAttrRam = 22528; // on the speccy.

        protected void Poke( int iAddr, byte bValue ) {
            if( iAddr >= iAttrRam && iAddr < iAttrRam + 32*24 ) {
                SetAttr( iAddr, bValue );
            } else {
                throw new ArgumentOutOfRangeException();
            }
        }

        protected void SetAttr( int iRow, int iCol, byte iAttr ) {
            Block oBlock = Speccy.Screen[iCol,iRow];

            oBlock.Attr = new Attribute( iAttr );
        }

        protected void SetAttr( int iOffset, byte bAttr ) {
            iOffset -= 22528;
            int iRow = iOffset / 32;
            int iCol = iOffset % 32;

            SetAttr( iRow, iCol, bAttr );
        }

        /// <summary>
        /// these can cause side effects on following attributes. Be careful!
        /// Make sure you assign a new attribute if you don't want following
        /// assignments to affect the current attribute.
        /// </summary>
        protected bool Bright {
            set { 
                Speccy.Attr._fBright = value;
            }
        }

        protected byte Paper {
            set { 
                Speccy.Attr._bPaper = value;
            }
        }

        protected byte Ink {
            set { 
                Speccy.Attr._bInk = value;
            }
        }

        public void At( int iRow, int iCol, char cUDG ) {
            Speccy.PutUDGAt( iRow, iCol, cUDG );
        }

        protected void At( SKPointI pntLoc, char cUDG ) {
            At( pntLoc.Y, pntLoc.X, cUDG );
        }

        public void UdgAt( int iRow, int iCol, string strV ) {
            foreach( char cV in strV ) {
                Speccy.PutUDGAt( iRow, iCol++, cV );
            }
        }

        /// <summary>
        /// Let's take a look at all the UDG's.
        /// </summary>
        public void TestGrid() {
            for( int iRow=0; iRow<24; ++iRow ) {
                for( int iCol=0; iCol<32; ++iCol ) {
                    At( iRow, iCol, (char)( iCol%21 + 'A' ) );
                }
            }

            Speccy.Refresh();
        }
    } // End doc

    public class ViewSpeccy :
        ViewSurface,
        IPgCommandView,
        IPgSave<XmlDocumentFragment>,
        IPgLoad<XmlElement>
    {
        public static readonly Guid GUID = new Guid( "{B85FA035-1ACB-46E0-84D9-A53A2C5AA8FB}" );

        DocumentTutTut Tut { get; }

        public string Banner => "Speccy ZX";

        public SKImage? Icon { get; protected set; }

        public Guid Catagory => GUID;

        public bool IsDirty => false;

        public ViewSpeccy(IPgViewSite oBaseSite, DocumentTutTut oTut ) : 
            base(oBaseSite, oTut.Speccy ) 
        {
            FilterMode = SKFilterMode.Nearest;
            Tut        = oTut ?? throw new ArgumentNullException( "Tut document" );

			try {
				Icon = DocumentTutTut.GetResource( "icons8-video-game-64.png" );
			} catch( InvalidOperationException ) {
                _oViewSite.LogError( "Spectrum Viewer", "Couldn't find screen icon" );
			}
        }

        public virtual bool Execute( Guid sGuid ) {
            return false;
        }

        public object? Decorate(IPgViewSite oBaseSite, Guid sGuid) {
            return null;
        }

        public bool Save(XmlDocumentFragment oStream) {
            return true;
        }

        public bool Load(XmlElement oStream) {
            return true;
        }
    }
}
