using Play.Edit;
using Play.Interfaces.Embedding;
using Play.Parse;
using Play.Rectangles;

using SkiaSharp;

namespace Monitor {
    public class MemAddrWindow : EditWindow2 {
        public MemAddrWindow( IPgViewSite oSite, BaseEditor oEdit ) : 
            base( oSite, oEdit ) 
        {
            ToolSelect = 1;
            _rctTextArea.Units = LayoutRect.CSS.Flex;
            _rctTextArea.Track = 200;
        }

        protected readonly LinkedList<Line> _rgHistory = new LinkedList<Line>();

        protected readonly LayoutRect _rctLineNumbers  = new LayoutRect( LayoutRect.CSS.Pixels ) { Track = 60 };
        protected readonly LayoutRect _rctLineComments = new LayoutRect( LayoutRect.CSS.Flex )   { Track = 130 };

        protected override CacheManager2 CreateCacheManager(uint uiStdText) {
            return new CacheManagerAsm( new CacheManSlot(this),
                                        _oStdUI.FontRendererAt(uiStdText),
                                        _rgCacheMap );
        }

        public class CacheManagerAsm : CacheManager2 {
            public CacheManagerAsm( CacheManagerAbstractSite oSite, IPgFontRender oFont, List<SmartRect> rgCols ) :
                base( oSite, oFont, rgCols ) {
            }

            protected override CacheRow CreateRow( Line oLine ) {
                CacheRow oRow = base.CreateRow( oLine );

                // TODO. It's pretty easy to add "Extra" to the text line case
                // I should remove it from the base TextLine and add it to a sub
                // class only where needed... :-/
                if( oLine is AssemblyEditor.AsmLine oAsmLine ) {
                    oRow.CacheList[0].Column = 1; // Gotta override the column!!

                    oRow.CacheList.Add( new FTCacheWrap( oAsmLine.Extra as Line, 0 ) );
                    oRow.CacheList.Add( new FTCacheWrap( oAsmLine.LineComment, 2 ) );
                }

                return oRow;
            }
        }
        protected override void InitColumns() {
            _rgLayout  .Add( _rctLineNumbers  );
            _rgLayout  .Add( _rctTextArea     ); // Main text area.
            _rgLayout  .Add( _rctLineComments );

            _rgCacheMap.Add( _rctTextArea     ); // Text is always the first cache element on a row.
            _rgCacheMap.Add( _rctLineNumbers  ); // Even if later in the layout.
            _rgCacheMap.Add( _rctLineComments );
        }

        protected override bool InitInternal() {
            if( !base.InitInternal() )
                return false;

            // We do a little too much in the base window. So
            // Clear that out and start over.
            HyperLinks.Clear();
            HyperLinks.Add( "url",       OnBrowserLink );
            HyperLinks.Add( "localpath", OnLocalPath );
            HyperLinks.Add( "CpuJump",   OnCpuJump );

            return true;
        }
        /// <remarks>
        /// This won't work for the data look ups atm.
        /// </remarks>
        protected void OnCpuJump(Line oLine, IPgWordRange oRange) {
            try {
                _rgHistory.AddFirst( CaretPos.Line );
                if( _rgHistory.Count > 10 ) {
                    _rgHistory.RemoveLast();
                }
            } catch( Exception oEx ) {
                Type[] rgError = { typeof( NullReferenceException ),
                                   typeof( ArgumentOutOfRangeException ) };
                if( rgError.IsUnhandled( oEx ) )
                    throw;
            }

            string strJumpRaw = oLine.SubString( oRange.Offset, oRange.Length );

            if( !int.TryParse( strJumpRaw, 
                               System.Globalization.NumberStyles.HexNumber, 
                               null, out int iJumpAddr ) )
                return;

            string strJumpX4 = iJumpAddr.ToString( "X4" );

            foreach( Line oTry in _oDocument ) {
                if( oTry.Extra is Line oMemAddr ) {
                    if( oMemAddr.Compare( strJumpX4, IgnoreCase:true ) == 0 ) {
                        CaretPos.Line   = oTry;
                        CaretPos.Offset = 0;
                        ScrollToCaret();
                    }
                }
            }
        }

        public override bool Execute( Guid sGuid ) {
			switch( sGuid ) {
				case var r when r == GlobalCommands.JumpPrev:
                    try {
                        if( _rgHistory.First() is Line oTry ) {
                            _rgHistory.RemoveFirst();

                            CaretPos.Line   = oTry;
                            CaretPos.Offset = 0;
                            ScrollToCaret();
                        }
                    } catch( Exception oEx ) {
                        Type[] rgError = { typeof( NullReferenceException ),
                                           typeof( InvalidOperationException ),
                                           typeof( ArgumentNullException ) };
                        if( rgError.IsUnhandled( oEx ) )
                            throw;
                    }
					return true;
				case var r when r == GlobalCommands.JumpNext:
					return true;
            }

            return base.Execute( sGuid );
        }

    }

    internal class WindowProgramDisplay : 
        WindowMultiColumn,
        IPgCommandView
    {
        public static Guid GUID { get; } = new Guid( "{1DBE2048-619C-44EA-882C-024DF5087743}" );

        public string Banner => "Assembly Monitor";

        public SKBitmap? Icon => null;

        public Guid Catagory => GUID;

        readonly Document_Monitor _oMonDoc;
        public WindowProgramDisplay( 
            IPgViewSite      oSiteView, 
            Document_Monitor p_oDocument, 
            bool             fReadOnly   = false, 
            bool             fSingleLine = false) : 
            base( oSiteView, p_oDocument.Doc_Asm ) 
        {
            _oMonDoc = p_oDocument;
        }

        public bool Execute(Guid sGuid) {
            return false;
        }

        /// <remarks>
        /// This won't work for the data look ups atm.
        /// </remarks>
        protected void OnCpuJump(Line oLine, IPgWordRange oRange) {
            //try {
            //    _rgHistory.AddFirst( CaretPos.Line );
            //    if( _rgHistory.Count > 10 ) {
            //        _rgHistory.RemoveLast();
            //    }
            //} catch( Exception oEx ) {
            //    Type[] rgError = { typeof( NullReferenceException ),
            //                       typeof( ArgumentOutOfRangeException ) };
            //    if( rgError.IsUnhandled( oEx ) )
            //        throw;
            //}

            //string strJumpRaw = oLine.SubString( oRange.Offset, oRange.Length );

            //if( !int.TryParse( strJumpRaw, 
            //                   System.Globalization.NumberStyles.HexNumber, 
            //                   null, out int iJumpAddr ) )
            //    return;

            //string strJumpX4 = iJumpAddr.ToString( "X4" );

            //foreach( Line oTry in _oDocument ) {
            //    if( oTry.Extra is Line oMemAddr ) {
            //        if( oMemAddr.Compare( strJumpX4, IgnoreCase:true ) == 0 ) {
            //            CaretPos.Line   = oTry;
            //            CaretPos.Offset = 0;
            //            ScrollToCaret();
            //        }
            //    }
            //}
        }

        protected override bool Initialize() {
            if( !base.Initialize() )
                return false;

            HyperLinks.Add( "CpuJump", OnCpuJump );

            return true;
        }

        public object? Decorate(IPgViewSite oBaseSite, Guid sGuid) {
            if( sGuid == GlobalDecorations.Outline ) {
                return new EditWindow2( oBaseSite, _oMonDoc.Doc_Outl, fReadOnly:true );
            }
            return null;
        }
    }
}
