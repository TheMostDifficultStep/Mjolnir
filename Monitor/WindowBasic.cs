using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public string   Banner => "Basic Viewer" ; // + Monitor.FileName;
        public SKBitmap Icon => null;
        protected BasicDocument DocMain { get; }

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

            _rgLayout.Add( new LayoutRect( LayoutRect.CSS.Pixels,  80, 1L ) ); // line number
            _rgLayout.Add( new LayoutRect( LayoutRect.CSS.None ) );            // basic code

            for( int i=0; i<BasicRow.ColumnCount; ++i ) {
                _rgColumns.Add( _rgLayout.Item( i+1 ) );
            }

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

        public void DumpBinaryFile() {
            DocMain.DumpBinaryFile( _oSiteView );
        }

        public virtual bool Execute( Guid sCommand ) {
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

            return false;
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
