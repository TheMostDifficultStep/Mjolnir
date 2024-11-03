using System.Drawing;
using System.Windows.Forms;

using Play.Edit;
using Play.Interfaces.Embedding;
using Play.Rectangles;
using SkiaSharp;

namespace Monitor {
    public class WindowBasic :
        WindowMultiColumn,
        IPgCommandView,
        IPgTools2 // Use "2" so we can have a tool button decoration.
    {
        public static Guid GUID { get; } = new Guid( "{3D3B82AF-49FA-469E-865F-F35DD8CF11FB}" );
        protected List<string> _rgTools = new List<string>();
        public Guid     Catagory => GUID;
        public string   Banner => "Basic Viewer - " + DocMain.FileName;
        public SKBitmap Icon => null;
        protected BasicDocument DocMain { get; }

        protected int _iBasicColumnTop = -1;

        protected class Tool {
            public string Name { get; protected set; }
            public Action Do   { get; protected set; }

            public Tool( string strName, Action pcAction ) {
                Name = strName;
                Do   = pcAction;
            }
        }

        protected List<Tool> _rgTools2 = new();
        protected int        _iToolSelect = 0;

        public event ToolEvent? ToolSelectChanged;

        public WindowBasic( IPgViewSite oSite, BasicDocument oDoc ) : 
            base( oSite, oDoc.BasicDoc ) 
        {
            DocMain = oDoc ?? throw new ArgumentNullException( ); 
            _rgTools.Clear();

            _rgTools2.Add( new Tool( "Renumber",   oDoc.BasicDoc.Renumber ) );
            _rgTools2.Add( new Tool( "Test" ,      oDoc.BasicDoc.Test ) );
            _rgTools2.Add( new Tool( "Dump File",  DumpBinaryFile ) );
          //_rgTools2.Add( new Tool( "Compile",    Compile ) );
          //_rgTools2.Add( new Tool( "Emulate",    oDoc.CallEmulator ) );
        }

        protected override bool Initialize() {
            if( !base.Initialize() )
                return false;

            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.Pixels,  80, 1L ), BasicRow.ColumnNumber ); 
            TextLayoutAdd( new LayoutRect( LayoutRect.CSS.None ),            BasicRow.ColumnText );  

            return true;
        }
        /// <summary>
        /// Override the normal row behavior so we can search for lines
        /// by address!! 
        /// </summary>
        public Row this[int iIndex] {
            get {
                foreach( Row oRow in DocMain.BasicDoc ) {
                    if( oRow is BasicRow oAsm ) {
                        if( int.TryParse( oAsm.Number.AsSpan, out int iBasNum ) &&
                            iBasNum >= iIndex )
                            return oRow;
                    }
                }
                return null;
            }
        }

        protected override void OnKeyPress(KeyPressEventArgs e) {
            if( IsDisposed )
                return;
            if( _oViewEvents.IsCommandPress( e.KeyChar ) )
                return;
            if( IsReadOnly )
                return;

			switch( e.KeyChar ) {
				//case '\t':
				//	int iDir = ModifierKeys == Keys.Shift ? -1 : 1;
				//	_oCacheMan.CaretTab( iDir );
				//	break;
				case '\r':
                    try {
					    // Not likely unset upon key press. But problematic if negative.
					    if( _oCacheMan.CaretAt < 0 )
						    break;

                        if( DocMain.BasicDoc[_oCacheMan.CaretAt] is BasicRow oCaret ) {
                            if( int.TryParse( oCaret.Number.AsSpan, out int iBasNum ) ) {
					            if( DocMain.BasicDoc.InsertRow( _oCacheMan.CaretAt + 1, -1, string.Empty ) is Row oRow ) {
						            _oCacheMan.CaretReset( oRow, iColumn:BasicRow.ColumnText );
					            }
                            }
                        }
                    } catch( Exception oEx ) {
                        if( BasicEditor.IsStdUnhandled( oEx ) )
                            throw;
                        LogError( "Couldn't add a line" );
                    }
					break;
				default:
					base.OnKeyPress( e );
					break;
			}
		}
        public void DumpBinaryFile() {
            DocMain.DumpBinaryFile( _oSiteView );
        }

        public override bool Execute( Guid sCommand ) {
            if( sCommand == GlobalCommands.Insert ) {
                DocMain.SideLoad();
                return true;
            }
            if( sCommand == GlobalCommands.SaveAs ) {
                DocMain.SaveAsDialog();
                return true;
            }
            if( sCommand == GlobalCommands.Insert ) {
                DocMain.SideLoad();
            }
            if( sCommand == GlobalCommands.Play ) {
                DocMain.BasicDoc.Compile();
            }

            return base.Execute( sCommand );
        }

		public int ToolSelect { 
            set { 
                _iToolSelect = value ;
                _rgTools2[value].Do();
				_oSiteView.Notify( ShellNotify.ToolChanged );
				ToolSelectChanged?.Invoke( this, value );
            }

            get {
                return _iToolSelect;
            }
        }

        public string ToolName( int i ) {
            try {
                return _rgTools2[i].Name;
            } catch( ArgumentOutOfRangeException ) {
                return "Unknown Tool";
            }
        }

        public int ToolCount => _rgTools2.Count;

        public Image ToolIcon( int iTool) {
            return null;
        }

        public object Decorate(IPgViewSite oBaseSite, Guid sGuid) {
            return null;
        }
    }
}
