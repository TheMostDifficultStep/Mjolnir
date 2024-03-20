using Play.Edit;
using Play.Interfaces.Embedding;
using Play.Parse;
using Play.Rectangles;

using SkiaSharp;

using Play.Forms;
using Play.ImageViewer;

namespace Monitor {

    /// <summary>
    /// This shows our dazzler image. Or anything using the display image
    /// I suppose.
    /// </summary>
    internal class ViewEmulatorImage :
        WindowSoloImage 
    {
        DocumentMonitor Mon { get; }
        public ViewEmulatorImage(IPgViewSite oBaseSite, DocumentMonitor oMon ) : 
            base(oBaseSite, oMon.Doc_Display) 
        {
            Mon = oMon ?? throw new ArgumentNullException();

			try {
				Icon = oMon.GetResource( "icons8-monitor-64.png" );
			} catch( InvalidOperationException ) {
			}
        }

        public override bool Execute( Guid sGuid ) {
            if( Mon.Execute( sGuid ) ) {
                return true;
            }

            return base.Execute( sGuid );
        }
    }

    /// <summary>
    /// This is the multicolumn display for the AsmEditor. Also using
    /// it as the main window for the monitor atm.
    /// </summary>
    /// <seealso cref="AsmEditor"/>
    /// <seealso cref="AsmRow"/>
    internal class ViewDisassembly : 
        WindowMultiColumn,
        IPgCommandView,
        IReadableBag<Row>
    {
        public static Guid GUID { get; } = new Guid( "{1DBE2048-619C-44EA-882C-024DF5087743}" );

        public string    Banner => "Assembly Monitor : " + _oMonDoc.FileName;
		public SKBitmap? Icon { get; protected set; }
        public Guid      Catagory => GUID;

        public int ElementCount => _oMonDoc.Z80Memory.Count;

        public Row this[int iIndex] {
            get {
                foreach( Row oRow in _oMonDoc.Doc_Asm ) {
                    if( oRow is AsmRow oAsm ) {
                        if( oAsm.AddressMap >= iIndex )
                            return oRow;
                    }
                }
                return null;
            }
        }

        readonly DocumentMonitor _oMonDoc;
        public ViewDisassembly( 
            IPgViewSite     oSiteView, 
            DocumentMonitor oDocument, 
            bool            fReadOnly   = false 
        ) : 
            base( oSiteView, oDocument.Doc_Asm ) 
        {
            _oMonDoc = oDocument;
			try {
				Icon = _oMonDoc.GetResource( "icons8-script-96.png" );
			} catch( InvalidOperationException ) {
			}
        }

        /// <remarks>
        /// This won't work for the data look ups atm. This executes
        /// the hyperlink jump from the asm params to the target mem location.
        /// </remarks>
        protected void OnCpuJump( Row oRow, int iColumn, IPgWordRange oRange ) {
            try {
                //    _rgHistory.AddFirst( CaretPos.Line );
                //    if( _rgHistory.Count > 10 ) {
                //        _rgHistory.RemoveLast();
                //    }

                Line   oLine      = oRow[iColumn];
                string strJumpRaw = oLine.SubString( oRange.Offset, oRange.Length );

                if( !int.TryParse( strJumpRaw,
                                   System.Globalization.NumberStyles.HexNumber,
                                   null, out int iJumpAddr ) )
                    return;

                string strJumpX4 = iJumpAddr.ToString("X4");

                // Use a bubble search in the future...
                foreach( AsmRow oTry in _oDocEnum ) {
                    if( oTry.AddressMap == iJumpAddr ) {
                        // Jump to column 1. It will have a value even if it
                        // is a data line.
                        if( !_oCacheMan.SetCaretPositionAndScroll( oTry.At, 1, 0 ) )
                            LogError( "Couldn't jump to desired location. :-/" );
                        break;
                    }
                }
            } catch( Exception oEx ) {
                Type[] rgError = { typeof( NullReferenceException ),
                                   typeof( ArgumentOutOfRangeException ) };
                if( rgError.IsUnhandled(oEx) )
                    throw;
            }
        }

        /// <remarks>
        /// I want to push layout to the init phase so we could potentially
        /// load our layout from a file! ^_^
        /// </remarks>
        protected override bool Initialize() {
            if( !base.Initialize() )
                return false;

            _rgLayout.Add( new LayoutRect( LayoutRect.CSS.Pixels,  60, 1L ) ); // Code
            _rgLayout.Add( new LayoutRect( LayoutRect.CSS.Pixels,  15, 1L ) ); // breakpoints
            _rgLayout.Add( new LayoutRect( LayoutRect.CSS.Percent, 20, 1L ) ); // labels
            _rgLayout.Add( new LayoutRect( LayoutRect.CSS.Percent, 10, 1L ) ); // instr
            _rgLayout.Add( new LayoutRect( LayoutRect.CSS.Percent, 20, 1L ) ); // params
            _rgLayout.Add( new LayoutRect( LayoutRect.CSS.None ) );            // comments

            // Need to figure out how to match the columns of the Window vs Document...
            // TODO: I'm going to add the columns to the cache site so I can init the later instead of now.
            _rgColumns.Add( _rgLayout.Item( 1 ) );
            _rgColumns.Add( _rgLayout.Item( 2 ) );
            _rgColumns.Add( _rgLayout.Item( 3 ) );
            _rgColumns.Add( _rgLayout.Item( 4 ) );
            _rgColumns.Add( _rgLayout.Item( 5 ) );
            _rgColumns.Add( _rgLayout.Item( 6 ) );

            HyperLinks.Add( "CpuJump", OnCpuJump );

            return true;
        }

        public object? Decorate(IPgViewSite oBaseSite, Guid sGuid) {
            if( sGuid == GlobalDecorations.Outline ) {
                return new EditWindow2( oBaseSite, _oMonDoc.Doc_Outl, fReadOnly:true );
            }
            if( sGuid == GlobalDecorations.Properties ) {
                return new WindowStandardProperties( oBaseSite, _oMonDoc.Doc_Props );
            }
            return null;
        }

        public bool Execute(Guid sGuid) {
            //let's not allow this until we sort out how the comments don't get lost...
            //if( sGuid == GlobalCommands.JumpParent ) {
            //    _oMonDoc.Dissassemble();
            //    return true;
            //}
            if( sGuid == GlobalCommands.JumpNext ) {
                if( _oMonDoc.Doc_Asm.FindRowAtAddress( _oMonDoc.PC, out AsmRow oAsm ) ) {
                    _oCacheMan.CaretReset( oAsm, AsmRow.ColumnInstr );
                }
                _oMonDoc.CpuStep();
                return true;
            }
            if( sGuid == GlobalCommands.Play ) {
                _oMonDoc.CpuStart();
                return true;
            }
            if( sGuid == GlobalCommands.Stop ) {
                _oMonDoc.CpuStop();
                return true;
            }
            if( sGuid == GlobalCommands.Pause ) {
                _oMonDoc.CpuBreak();
                if( _oMonDoc.Doc_Asm.FindRowAtAddress( _oMonDoc.PC, out AsmRow oAsm ) ) {
                    _oCacheMan.CaretReset( oAsm, AsmRow.ColumnInstr );
                }
                return true;
            }
            if( sGuid == GlobalCommands.Recycle ) {
                _oMonDoc.CpuRecycle();
                return true;
            }

            return false;
        }

    }
}
