using Play.Drawing;
using Play.ImageViewer;
using Play.Interfaces.Embedding;

using SkiaSharp;

using System.Reflection;
using System.Security.Policy;
using System.Xml;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
        public SKSurface Scratch { get; } // Make a bulk loader in the future...
        public SKImage[] Images  { get; } // Our constructed UDG's

        public Attribute Attr    { get;set; } = new Attribute(0);

        public SpectrumGraphics( IPgBaseSite oSite, string strMode ) : base( oSite ) {
            if( string.Compare( strMode, "std" ) != 0 ) {
                throw new ArgumentOutOfRangeException();
            }
            Screen  = new Block[32,24];
            Images  = new SKImage[256];
            Scratch = SKSurface.Create( new SKImageInfo( 8,     8, SKColorType.Bgra8888 ) );
            Surface = SKSurface.Create( new SKImageInfo( 256, 192, SKColorType.Bgra8888 ) );
        }

        /// <summary>
        /// Fill out our screen black.
        /// </summary>
        public virtual bool InitNew() {
            SetGraphic( 0, [0,0,0,0,0,0,0,0] ); // Gives us a solid block.

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
        /// </remarks>
        public void SetGraphic( int i, byte[] rgUdg ) {
            ArgumentNullException.ThrowIfNull( rgUdg ); 

            for( int iY = 0; iY<8; ++iY ) {
                byte bRow = rgUdg[iY];
                for( int iX = 0; iX < 8; ++iX ) {
                    // Highest bit is the lowest X value...
                    SKColor sColor = ( bRow & 1<<(7-iX) ) > 0 ? SKColors.White : SKColors.Black;

                    Scratch.Canvas.DrawPoint( iX, iY, sColor );
                }
            }
            Images[i] = Scratch.Snapshot();
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

        /// <summary>
        /// After you have loaded your UDG's, you may attempt
        /// to refresh and draw the screen.
        /// </summary>
        public void Refresh() {
            try {
                for( int iY = 0; iY<Screen.GetLength(1); ++iY ) {
                    for( int iX = 0; iX <Screen.GetLength(0); ++iX ) {
                        SKPoint pntLoc = new( iX*8, iY*8 );
                        Block   oBlock = Screen[iX, iY];
                        SKImage oUdg   = Images[oBlock.Img];

                        if( oUdg is null ) {
                            oUdg = Images[0];
                        }

                        Surface.Canvas.DrawImage( oUdg, pntLoc );
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
            InitUDG ();
            LoadGrid(0);
            LoadKeysAndMummy(0);

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
                Speccy.SetGraphic( i+0x90, rgUdg[i] );
            }
        }

        public static int R( int a, int b ) {
            return a - (a/b)*b;
        }
        public static int M( int a, int b ) {
            return a/b;
        }

        public void LoadGrid( int _ ) {
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

        public void LoadKeysAndMummy( int _ ) {
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

                    Attr = new Attribute( (byte)(iZ+64+1) ); // 0x41-0x44
                    At( iB, iV, 'D' );
                }
                // Next 4 are mummies.
                if( iZ > 3 ) {
                    // Address 22528 marks the exact start of the attribute
                    // file (color RAM), located right at the top-left corner
                    // of the screen grid
                    rgMummy[iZ-4] = iZ+22528+iU*32+iU+1-1; // b/c basic 
                    Poke( rgMummy[iZ-4], 71 ); // 0x47 bright bg:black, fg:white
                    
                    // SetAttr( iU, iU+iZ, 71 ); use this once we're running.
                }
            }

        }

        public void LoadStuff( int _ ) {
            int[][] rgStuff = [
                [ 0, 0, 0, 0, 0 ],
                [ 0, 0, 0, 0, 0 ],
                [ 0, 0, 33, 0, 0 ],
                [ 0, 54, 161, 310, 417 ]
            ];

            for( int iZ=0; iZ<rgStuff.GetLength(0); iZ++ ) {
                for( int iB=0; iB<rgStuff.GetLength(1); iB++ ) {
                }
            }
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

            oBlock.Attr.Value = iAttr;
        }

        protected void SetAttr( int iOffset, byte bAttr ) {
            iOffset -= 22528;
            int iRow = iOffset / 32;
            int iCol = iOffset % 32;

            SetAttr( iRow, iCol, bAttr );
        }

        protected void Bright( bool fBright ) {
            Speccy.Attr._fBright = fBright;
        }

        public void At( int iRow, int iCol, char cUDG ) {
            Speccy.PutUDGAt( iRow, iCol, cUDG );
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
