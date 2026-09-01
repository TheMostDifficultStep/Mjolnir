using Play.Drawing;
using Play.Edit;
using Play.ImageViewer;
using Play.Interfaces.Embedding;
using Play.Spectrum;

using SkiaSharp;

using System.Reflection;
using System.Windows.Forms;
using System.Xml;

namespace Play.Games {

    public enum GameState {
        Menu1,
        Menu200,
        Menu400,
        Playing,
        Goto480
    }

    public class Mummy {
        public int Pos { get; set; }
        public int Dir { get; set; }
    }

    public class DocumentTutTut :
        IPgParent,
        IPgLoad<TextReader>,
        IPgSave,
		IDisposable
    {
        protected readonly IPgBaseSite       _oBaseSite;
        protected readonly IPgRoundRobinWork _oWorkPlace; 
        protected readonly IPgStandardUI2    _oStdUI;

        public SpectrumGraphics Speccy { get; }
        public IPgParent Parentage => _oBaseSite.Host;
        public IPgParent Services  => Parentage.Services;
        public bool      IsDirty   => false;
        protected IPgFontRender Font { get; }


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

        SpectrumAttrib Attr {
            get { return Speccy.Attr; }
            set { Speccy.Attr = value; }
        }

        const    int   _iU        = 3;
                 int   _iG        = 0; // probably score.
                 bool  _fParity   = false;
                 int   _iAirCount = 0;
                 int   _iS        = 0;
                 int   _iLevel    = 0; /* T in the prog */
        public GameState State {get; set;} = GameState.Playing; // menu1
        public Keys      LastKey { get; set;} = Keys.None;
        readonly Mummy[] _rgMummy = new Mummy[4];
        protected SKPointI _pntExplorer;
        protected SKPointI _pntPrevExpl;

        const byte ClrMummy = 71;  // bg:black, fg:white
        const byte ClrWall  = 114; // bg:red,   fg:yellow
        const byte ClrExpl  = 69;  // bg black, fg:blue

        readonly uint _uiFont = 0;

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

        public DocumentTutTut( IPgBaseSite oBaseSite ) {
            _oBaseSite  = oBaseSite ?? throw new ArgumentNullException( nameof( oBaseSite ) );
            _oWorkPlace = ((IPgScheduler)Services).CreateWorkPlace() ?? throw new InvalidProgramException();
            Speccy      = new SpectrumGraphics( new DocSlot( this ), "std" );
 			_oStdUI     = _oBaseSite.Host.Services as IPgStandardUI2 ?? throw new ArgumentException( "Parent view must provide IPgStandardUI service" );

            for( int i = 0;i<_rgMummy.Length; ++i ) {
                _rgMummy[i] = new Mummy();
            }

            // Sort of overkill, using to generate the numbers at present.
            _uiFont = _oStdUI.FontCacheNew  ( _oStdUI.StdFaceAt( StdUIFaces.Retro ), 6, new SKPoint( 96, 96 ) );
            Font    = _oStdUI.FontRendererAt( _uiFont );
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
            LoadFont ();
            LoadUDG  ();  // We override some of the font.

            LoadGrid (0);
            LoadKandM(0);
            LoadStuff(0);
            LoadExplorer(0);
            DrawBorder();

            DrawStatus550();

            Speccy.Refresh();

            _oWorkPlace.Queue( GameLoop(), 0 );

            return true;
        }

        /// <summary>
        /// Load up the numbers. The lower case characters are not ansii,
        /// in the PressStart2P-Regular font, or might even be completely 
        /// missing. Haven't checked.
        /// </summary>
        /// <remarks>
        /// Need to blit opaque bg and then image will set that to 0XFF
        /// for the bit to be seen.
        /// </remarks>
        protected bool LoadFont() {
            using SKSurface oSurface = SKSurface.Create( new SKImageInfo( 8, 8, SKColorType.Bgra8888 ) );
            using SKPaint   oPaint   = new SKPaint() { Color = new SKColor( 0, 0, 0, 0X0 ) };

            SKCanvas oCanvas = oSurface.Canvas;

            for( uint uI = 0x30; uI < 0x40; ++uI ) {
                IPgGlyph oGlyph = Font.GetGlyph( uI );

                oCanvas.DrawRect ( new SKRect( 0, 0, 8, 8 ), oPaint );
                oCanvas.DrawImage( oGlyph.Image, 0, 7 - oGlyph.Image.Height + 1 );

                Speccy.Images[uI] = oSurface.Snapshot();
            }

            return true;
        }

        /// <summary>
        /// Read in all the user defined characters. Line 645;
        /// </summary>
        public void LoadUDG() {
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

            // These are missing characters from the retro
            // font I'm using. If I add numbers I can even
            // skip the LoadFont() call.
            byte[] rgColon = [0, 0, 24, 24, 0, 24, 24, 0];
            Speccy.SetGraphic2( ':', rgColon );
            byte[] rgDash  = [0, 0, 0, 0, 60, 0, 0, 0];
            Speccy.SetGraphic2( '-', rgDash );
            byte[] rgLwrO  = [0, 124, 134, 130, 130, 130, 194, 124];
            Speccy.SetGraphic2( 'o', rgLwrO );
            byte[] rgLwrH  = [0, 70, 70, 66, 126, 66, 98, 98];
            Speccy.SetGraphic2( 'h', rgLwrH );
            byte[] rgLwrF  = [0, 60, 68, 64, 120, 64, 64, 96];
            Speccy.SetGraphic2( 'f', rgLwrF );
            byte[] rgLwrI  = [0, 124, 20, 16, 16, 16, 16, 124];
            Speccy.SetGraphic2( 'i', rgLwrI );
        }

        readonly string[] _rgGrid = [
            "44444444","67777778","45444544","45444544","45444444",
            "67777778","44444544","44444544","44444544","67777778",
            "45444444","45444444","45444544","67777778","44444444" ];


        protected void LoadGrid( int _ ) {
            string   strT    = "BABAAABBBAA";
            // Fill in 15 rows where each row is iZ. Line 690
            for( int iZ = 0; iZ < _rgGrid.Length ; iZ++ ) {
                string strC = string.Empty;
                string strZ = _rgGrid[iZ];

                // Create a 24 character long string.
                for( int iC=0; iC < strZ.Length; ++iC ) { 
                    int iOffs = strZ[iC] - '0' - 1; // b/c because basic...
                    strC += strT[iOffs..(iOffs+3)]; // fill in 3 chars from T
                }
                // Load up that string on the screen.
                Attr = new SpectrumAttrib( ClrWall );
                UdgAt( iZ+_iU, _iU+1, strC );

                // Now go back and add all the tunnles.
                Attr = new SpectrumAttrib(0);
                for( int iV = 0; iV < strC.Length; ++iV ) {
                    if( strC[iV] == 'B' ) {
                        char cTmp = (char)( 0x91 + R(iV + iZ, 2) );

                        UdgAt(iZ + _iU, _iU + iV + 1, cTmp);
                    }
                }
            }
        }

        protected void LoadKandM( int _ ) {
            int[] rgKeyMum = [
                0, 0, 0, 438, 50, 167, 300, 418
            ]; // 0-3 keys, 4-7 mummies
            Attr = new SpectrumAttrib(0); // line 750
            for( int iZ =0; iZ < 8; ++iZ ) {
                int iC = rgKeyMum[iZ];
                // First 4 are keys, set if not zero.
                if( iZ < 4 && iC > 0 ) {
                    // Oh! Col...Row calculation!! TODO: Use r/c in future. 
                    int iV = R(iC,32) + _iU + 1;
                    int iB = M(iC,32) + _iU;

                    Attr = new SpectrumAttrib( (byte)(iZ+0x40+1) ); // bright,paper:0,ink:z+1
                    UdgAt( iB, iV, 'D' );
                }
                // Next 4 are mummies. 2d pos encoded in one number!!
                if( iZ > 3 ) {
                    // Address 22528 marks the exact start of the attribute
                    // file (color RAM), located right at the top-left corner
                    // of the screen grid
                    _rgMummy[iZ-4].Pos = iC+iAttrRam+_iU*32+_iU+1-1; // b/c basic 
                    Poke( _rgMummy[iZ-4].Pos, 71 ); // 0x47 bright bg:black, fg:white
                    
                    // SetAttr( iU, iU+iZ, 71 ); use this once we're running.
                    // Interesting that the UDG isn't set tho...
                }
            }

        }

        /// <summary>
        /// Adds the Exits, Gems, and other goodies..
        /// </summary>
        /// <param name="_"></param>
        protected void LoadStuff( int _ ) {
            int[] rgItems = [
                0,  0,   0,   0,
                0,  0,   0,   0,
                0,  0,   0,   0,
               33,  0,   0,   0,  // z=3, exits 
               54, 161, 310, 417  // z=4, gems
            ];

            Attr   = new SpectrumAttrib(0);
            Bright = true;

            int iIndex = 0;

            for( int iZ=0; iZ<5; ++iZ ) {
                for( int iB=0; iB<4; ++iB ) {
                    int iC   = rgItems[iIndex++];
                    int iCol = R( iC, 32 )+_iU+1; // modulus, %
                    int iRow = M( iC, 32 )+_iU;

                    if( iC > 0 && iZ<4 ) { // line 790
                        Paper = 7;
                        Ink   = (byte)(iZ+1); //+1 b/c basic.

                        UdgAt( iRow, iCol, 'E' );

                        if( iZ == 3 ) {
                            UdgAt( iRow, iCol, 'F' ); // Green tv/white bg
                        }
                    }
                    if( iC > 0 && iZ > 3 ) {
                        Attr = new SpectrumAttrib( 6 );

                        UdgAt( iRow, iCol, 'G' );
                    }
                }
            }
        }

        protected void LoadExplorer( int _ ) {
            int    iVal       = 113;
            string strMessage = @"LTL of PSQLMh";

            _pntExplorer = new SKPointI( R(iVal,32)+_iU+1, M(iVal,32)+_iU );
            _pntPrevExpl = _pntExplorer;
            //int iQ = iY;
            //int iW = iX;

            DrawExplorer225(); // Call 225

            // This is the first place we have a mixed string of UDG's
            // and standard ASCII... I think!
            List<char> rgTx = new List<char>();
            for( int i=0; i< strMessage.Length; ++i ) {
                if( char.IsUpper( strMessage[i] ) ) {
                    rgTx.Add( (char)( 0x90 + strMessage[i] - 'A' ) );
                } else {
                    rgTx.Add( strMessage[i] );
                }
            }

            // Put something in the middle of the screen.
            Attr = new SpectrumAttrib( 66 );
            int iHalf = rgTx.Count / 2;
            for( int i=0; i< rgTx.Count; ++i ) {
                Speccy.PutChrAt( _iU+16,_iU+12-iHalf+i, rgTx[i] );
            }
        }

        protected void DrawExplorer225( ) {
            Attr = new SpectrumAttrib(0);

            // I don't think I need to keep this persistant, it's
            // just being used to determine the direction the char
            // is going.
            int C = R( _pntPrevExpl.X + _pntPrevExpl.Y, 2 );

            if( C != 0 ) {
                UdgAt( _pntPrevExpl, 'B' );
                Attr.Value = ClrExpl;
                UdgAt( _pntExplorer, 'I' );
            } else {
                UdgAt( _pntPrevExpl, 'C' );
                Attr.Value = ClrExpl;
                UdgAt( _pntExplorer, 'H' );
            }

            _pntPrevExpl = _pntExplorer;
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
            ScreenBlock oBlock = Speccy.Screen[iCol,iRow];

            oBlock.Attr = new SpectrumAttrib( iAttr );
        }

        protected void SetAttr( int iOffset, byte bAttr ) {
            iOffset -= iAttrRam;
            int iRow = iOffset / 32;
            int iCol = iOffset % 32;

            SetAttr( iRow, iCol, bAttr );
        }

        protected byte AttrAt( int iOffset ) {
            iOffset -= iAttrRam;
            int iRow = iOffset / 32;
            int iCol = iOffset % 32;

            try {
                return Speccy.Screen[iCol,iRow].Attr.Value;
            } catch( IndexOutOfRangeException ) {
                throw;
            }
        }

        public byte AttrAt( SKPointI pntLoc ) {
            try {
                return Speccy.Screen[pntLoc.X, pntLoc.Y].Attr.Value;
            } catch( IndexOutOfRangeException ) {
                throw;
            }
        }

        public byte AttrAt( int iRow, int iCol ) {
            try {
                return Speccy.Screen[iCol,iRow].Attr.Value;
            } catch( IndexOutOfRangeException ) {
                throw;
            }
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

        public void UdgAt( int iRow, int iCol, char cUDG ) {
            Speccy.PutUDGAt( iRow, iCol, cUDG );
        }

        protected void UdgAt( SKPointI pntLoc, char cUDG ) {
            UdgAt( pntLoc.Y, pntLoc.X, cUDG );
        }

        public void UdgAt( int iRow, int iCol, string strUdg ) {
            foreach( char cV in strUdg ) {
                if( cV >= 0x41 && cV <= 0x5a ) {
                    Speccy.PutUDGAt( iRow, iCol++, cV );
                } else {
                    Speccy.PutChrAt( iRow, iCol++, cV );
                }
            }
        }

        public void ChrAt( int iRow, int iCol, string strChar ) {
            foreach( char cV in strChar ) {
                Speccy.PutChrAt( iRow, iCol++, cV );
            }
        }

        /// <summary>
        /// Let's take a look at all the UDG's.
        /// </summary>
        public void TestGrid() {
            for( int iRow=0; iRow<24; ++iRow ) {
                for( int iCol=0; iCol<32; ++iCol ) {
                    UdgAt( iRow, iCol, (char)( iCol%21 + 'A' ) );
                }
            }

            Speccy.Refresh();
        }
        public IEnumerator<int> GameLoop() {
            while( true ) {
                switch( State ) {
                    case GameState.Playing:
                        Play();
                        break;
                }

                Speccy.Refresh();
                yield return 250; // time in ms.
            }
        }

        protected void Play() {
            // check if explorer hit mummy.
            if( AttrAt( _pntExplorer) == ClrMummy ) {
                //Do275(); Do255(); 
                DrawExplorer225();
            }
            switch( LastKey ) {
                case Keys.Up:
                    _pntExplorer.Y -= 1;
                    break;
                case Keys.Down:
                    _pntExplorer.Y += 1;
                    break;
                case Keys.Left:
                    _pntExplorer.X -= 1;
                    break;
                case Keys.Right:
                    _pntExplorer.X += 1;
                    break;
            }
            LastKey = Keys.None;

            byte bC = AttrAt( _pntExplorer );
            if( _pntExplorer.Y - _pntPrevExpl.Y +
                _pntExplorer.X - _pntPrevExpl.X != 0 &&
                bC != ClrWall ) 
            {
                    MoveExplorer180( bC );
            }
            // if explorer moved then prev=expl
            // if not we reset the explorer pos.
            _pntExplorer = _pntPrevExpl;

            // Mummies move every other cycle.
            int iZ = _fParity ? 1 : 0; 
            for(int iV=iZ; iV<iZ+2; iV+=2 ) {
                int  iMummy  = _rgMummy[iV].Pos+_rgMummy[iV].Dir;
                byte bScreen = AttrAt( iMummy );

                if( bScreen != 0 && bScreen != ClrExpl ) {
                    SetMummyDirection125( iV );
                } else {
                    // Clear old mummy pos and set new.
                    Poke( _rgMummy[iV].Pos, 0 );
                    _rgMummy[iV].Pos = iMummy;
                    Poke( iMummy, 71 );
                }
            }

            _fParity = !_fParity;

            // This is counting down the air supply.
            if( --_iAirCount < 0 ) {
                _iS += 1;
                Poke( 23229-_iS, 16 );
                if( AttrAt( 23208 ) == 16 ) {
                    // Game over...
                    State = GameState.Goto480;
                }
                _iAirCount = 50;
            }
        }

        protected void MoveExplorer180( byte bC ) {
            if( bC == 0 ) { // free space
                MoveExplorer225();
                return;
            }
            if( bC == ClrMummy ) {
                // 275, 255
                MoveExplorer225();
                return;
            }
            if( bC == 6 ) {
                _iG += 25;
                // 255
                MoveExplorer225();
                return;
            }
            if( bC >64 && bC < 69 ) {
                // grabbing keys I'll guess.
                // k(c-64)=c
                _iG += 10;
                // 250
                MoveExplorer225();
                return;
            }
            if( bC == 16 ) {
                return;
            }
            if( /* k(c-120) == 0 OR */ bC < 120 ) {
                return;
            }
            if( bC == 124 ) {
                _iLevel += 1;
                _iG     += 24 * ( _iLevel + 1 ) - R( _iS * (_iLevel+1 ), 5 );
                // 255
                // 310
                // goto 610
                return;
            }
            // God knows what this is doing?
            int iV = _pntExplorer.Y * 2 - _pntPrevExpl.Y;
            int iB = _pntExplorer.X * 2 - _pntPrevExpl.X;

            if( AttrAt( iV, iB ) != 0 ) {
                return;
            }

            // This brings a bird up!
            Attr = new SpectrumAttrib( bC );
            UdgAt( iV, iB, 'E' );
            MoveExplorer225();
        }

        /// <summary>
        /// Gives us the left right motion of explorer,
        /// and set's the previous position to the current.
        /// </summary>
        protected void MoveExplorer225() {
            Attr = new SpectrumAttrib(0);
            int iC = R( _pntPrevExpl.X + _pntPrevExpl.Y, 2 );

            if( iC == 0 ) {
                UdgAt( _pntPrevExpl, 'B' );
                Attr = new SpectrumAttrib(ClrExpl);
                UdgAt( _pntExplorer, 'I' );
            } else {
                UdgAt( _pntPrevExpl, 'C' );
                Attr = new SpectrumAttrib(ClrExpl);
                UdgAt( _pntExplorer, 'H' );
            }
            _pntPrevExpl = _pntExplorer;
        }

        /// <summary>
        /// Set mummy direction.
        /// </summary>
        /// <param name="iV"></param>
        protected void SetMummyDirection125( int iV ) {
            if( iV >= _rgMummy.Length )
                throw new ArgumentOutOfRangeException();

            int iC = _pntExplorer.Y*32+
                     _pntExplorer.X+iAttrRam-
                     _rgMummy[iV].Pos;
            int iB = _rgMummy[iV].Pos;

            if( iC > 16 && AttrAt( iB+32) == 0) {
                _rgMummy[iV].Dir = 32;
                return;
            }
            if( iC > 0 && AttrAt( iB+1 ) == 0 ) {
                _rgMummy[iV].Dir = 1;
                return;
            }
            if( iC < -16 && AttrAt( iB-32 ) == 0 ) {
                _rgMummy[iV].Dir = -32;
                return;
            }
            if( iC < 0 && AttrAt( iB - 1 ) == 0 ) {
                _rgMummy[iV].Dir = -1;
                return;
            }

            iB = _rgMummy[iV].Dir;
            if( iB == 1 ) {
                _rgMummy[iV].Dir = -1;
                return;
            }
            if( iB == -1 ) {
                _rgMummy[iV].Dir = -32;
                return;
            }
            if( iB == -32 ) {
                _rgMummy[iV].Dir = 32;
                return;
            }
            _rgMummy[iV].Dir = 1;
        }

        /// <summary>
        /// Draw border. Line 530
        /// </summary>
        public void DrawBorder() {
            Attr = new SpectrumAttrib( 0x10 ); // bg:?, fg:black
            // Top border...
            UdgAt( _iU-1, _iU, 'M' );
            for( int iCol=1+_iU; iCol <25+_iU; ++iCol ) {
                UdgAt( _iU-1, iCol, 'U' );
            }
            UdgAt( _iU-1, _iU+25, 'Q' );

            // Bottom border...
            UdgAt( _iU+15, _iU, 'Q' );
            for( int iCol=1+_iU; iCol <25+_iU; ++iCol ) {
                UdgAt( _iU+15, iCol, 'U' );
            }
            UdgAt( _iU+15, _iU+25, 'M' );

            // Verticals...
            for( int iRow=_iU; iRow < 15+_iU; ++iRow ) {
                UdgAt( iRow, _iU,    'U' );
                UdgAt( iRow, 25+_iU, 'U' );
            }
        }

        /// <summary>
        /// Alas, I don't have the regular character site AND
        /// my routine won't print mixed udg's AND text yet.
        /// This is line 550
        /// </summary>
        public void DrawStatus550() {
            Attr = new SpectrumAttrib( 71 );
            UdgAt( _iU-3,_iU+9, "QSQ-QSQ" );   // Tut-Tut, but '-' broken >_K;;

            UdgAt( _iU+17, _iU, "MLTP:" );     // keys...
            //Attr = new SpectrumAttrib( 0x0 );
            //At( _iU+17, _iU+4, "DDDD" );    // CLear keys. Probably uneeded
            // Set the keys.

            Attr = new SpectrumAttrib( 71 );
            UdgAt( _iU+17, _iU+14, "PKoOL:" ); // score
            Attr = new SpectrumAttrib( ClrExpl );
            ChrAt( _iU+17, _iU+20, "000000" );
            for( int iZ=1; iZ < 5; ++iZ ) {
                Attr = new SpectrumAttrib( (byte)iZ );
                UdgAt( _iU+17, _iU+4+iZ, "D" );
            }

            DrawScore255();

            Attr = new SpectrumAttrib( 71 );
            UdgAt( _iU+18, _iU, "JiO :" );     // Air
            Attr = new SpectrumAttrib( 0x85 );
            for( int iAir=0; iAir < 21; ++iAir ) {
                UdgAt( _iU+18, _iU+iAir+5, "U" );
            }
        }

        protected void DrawScore255() {
            if( _iG < 0 )
                _iG = 0;

            Attr = new SpectrumAttrib( ClrExpl );
            string strScore = _iG.ToString();
            UdgAt( _iU+17, _iU+26-strScore.Length, strScore );
        }

    } // End doc

    public class ViewTut :
        ViewSurface,
        IPgCommandView,
        IPgSave<XmlDocumentFragment>,
        IPgLoad<XmlElement>
    {
        public static readonly Guid GUID = new Guid( "{B85FA035-1ACB-46E0-84D9-A53A2C5AA8FB}" );

        DocumentTutTut Tut { get; }

        public string Banner => "Tut Tut";

        public SKImage? Icon { get; protected set; }

        public Guid Catagory => GUID;

        public bool IsDirty => false;

        public ViewTut(IPgViewSite oBaseSite, DocumentTutTut oTut ) : 
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

        protected override void OnKeyDown(KeyEventArgs e) {
            switch( e.KeyCode ) {
                case Keys.Left:
                case Keys.Right:
                case Keys.Up:
                case Keys.Down:
                    Tut.LastKey = e.KeyCode;
                    break;
                case Keys.R:
                    Tut.State = GameState.Goto480;
                    break;
            }
        }
    }
}
