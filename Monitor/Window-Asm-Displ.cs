using Play.Edit;
using Play.Interfaces.Embedding;
using Play.Parse;
using Play.Rectangles;

using SkiaSharp;

namespace Monitor {

    /// <summary>
    /// This is the multicolumn display for the AsmEditor.
    /// </summary>
    /// <seealso cref="AsmEditor"/>
    internal class ViewProgramDisplay : 
        WindowMultiColumn,
        IPgCommandView
    {
        public static Guid GUID { get; } = new Guid( "{1DBE2048-619C-44EA-882C-024DF5087743}" );

        public string Banner => "Assembly Monitor";

        public SKBitmap? Icon => null;

        public Guid Catagory => GUID;

        readonly Document_Monitor _oMonDoc;
        public ViewProgramDisplay( 
            IPgViewSite      oSiteView, 
            Document_Monitor p_oDocument, 
            bool             fReadOnly   = false 
        ) : 
            base( oSiteView, p_oDocument.Doc_Asm ) 
        {
            _oMonDoc = p_oDocument;
        }

        /// <remarks>
        /// This won't work for the data look ups atm.
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

            HyperLinks.Add( "CpuJump", OnCpuJump );

            return true;
        }

        public object? Decorate(IPgViewSite oBaseSite, Guid sGuid) {
            if( sGuid == GlobalDecorations.Outline ) {
                return new EditWindow2( oBaseSite, _oMonDoc.Doc_Outl, fReadOnly:true );
            }
            return null;
        }

        public bool Execute(Guid sGuid) {
            if( sGuid == GlobalCommands.JumpParent ) {
                _oMonDoc.Dissassemble();
                return true;
            }
            if( sGuid == GlobalCommands.JumpNext ) {
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
