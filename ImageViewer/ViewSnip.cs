using System;
using System.Xml;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Security;

using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Rectangles;
using Play.Edit;

namespace Play.ImageViewer {
	// TODO: Convert the left hand column of the properties to be light CacheWrap elements instead
	//       of heavy EditWin's
	// TODO: Would be kewl if I could rotate the image 90 degrees left or right.
	// TODO: Set up tool options window so can set aspect square/arbitrary/free
	// TODO: Add a pen tool so can draw on image!
	public class ViewSnipDialog : Control,
		IPgSave<XmlDocumentFragment>,
		IPgLoad<XmlElement>,
		IPgCommandView,
		IPgParent
	{
		public static Guid Guid { get; } = new Guid("ad862093-fdcf-4642-a43f-f8e1f7b15bd0");

		protected enum Labels : int {
			Name = 0,
			Format,
			Quality,
			Width,
			Height,
			Fixed,
			FilePath,
			FileName,
			Max
		}

		protected class Input {
			public readonly Labels _eLabel;
			public readonly string _strLabel;
			public readonly string _strValue;

			public uint   _uiHeight;
			public Editor _oEditor;

			public Editor Editor { 
				set { 
					_oEditor = value ; 
				}
				get { 
					return _oEditor; 
				}
			}

			public string Text {
				get {
					return _oEditor[0].ToString();
				}
				set {
					using( BaseEditor.Manipulator oManip = _oEditor.CreateManipulator() ) {
						oManip.DeleteAll();
						oManip.LineInsert( 0, value );
					}
				}
			}

			public int Number {
				get { return int.Parse( _oEditor[0].ToString() ); }
				set { Text = value.ToString(); }
			}

			public Input( Labels eLabel, string strLabel, string strValue, uint uiHeight ) {
				_eLabel   = eLabel;
				_strLabel = strLabel;
				_strValue = strValue;
				_uiHeight = uiHeight;
			}
		}

		protected class SnipSlotBase :
			IPgBaseSite
		{
			protected readonly ViewSnipDialog _oHost;

			public SnipSlotBase( ViewSnipDialog oHost ) {
				_oHost = oHost ?? throw new ArgumentNullException();
			}

			public IPgParent Host => _oHost;

			public void LogError(string strMessage, string strDetails, bool fShow=true) {
				_oHost._oSiteBase.LogError( strMessage, strDetails, fShow );
			}

			public void Notify( ShellNotify eEvent ) {
			}
		} // End class

		protected class SnipSlotView :
			SnipSlotBase,
			IPgViewSite
		{
			public SnipSlotView( ViewSnipDialog oHost ) : base( oHost ) {
			}

			public IPgViewNotify EventChain => _oHost._oSiteBase.EventChain;
		} // End class

		protected readonly ImageWalkerDoc            _oDocument;
		protected readonly IPgViewSite               _oSiteBase;
        protected readonly IPgShellSite              _oSiteShell;
        protected readonly IPgViewNotify             _oViewEvents; // From our site, window manager (view interface).
		protected readonly Dictionary<Labels, Input> _rgProperty = new Dictionary<Labels, Input>();

        readonly LayoutStackVertical _rgVertStack = new LayoutStackVertical() { Margin = 5 };
		readonly SmartTable          _rgTable     = new SmartTable( 5, LayoutRect.CSS.Flex );

		protected ImageSoloDoc    SnipDoc { get; }
		protected ImageViewSingle SnipView{ get; }
		protected Input this[Labels eIndex] => _rgProperty[eIndex];

        protected uint _uiReturnID       = 0; // Which view to return to after snip save.
		protected bool _fBlockTextEvents = false;
		protected bool _fDisposed        = false;

        public ViewSnipDialog( IPgViewSite oSiteView, ImageWalkerDoc oDoc ) {
			_oSiteBase   = oSiteView ?? throw new ArgumentNullException( "View Site must not be null." );
            _oSiteShell  = oSiteView as IPgShellSite ?? throw new ArgumentException( "View Site must support IPgShellSite" );
			_oViewEvents = oSiteView.EventChain ?? throw new ArgumentException( "Site.EventChain must support IPgViewSiteEvents" );
			_oDocument   = oDoc ?? throw new ArgumentNullException( "Document must not be null." );
			Icon         = _oDocument.GetResource( "icons8-cut-40.png" );

            uint uiHeight = (uint)(Font.Height * 1.5);
            _rgProperty.Add( Labels.Format,   new Input( Labels.Format,   "Format",   "jpg",   uiHeight ));
			_rgProperty.Add( Labels.Quality,  new Input( Labels.Quality,  "Quality",  "80",    uiHeight ));
			_rgProperty.Add( Labels.Height,   new Input( Labels.Height,   "Height",   "100",   uiHeight ));
			_rgProperty.Add( Labels.Width,    new Input( Labels.Width,    "Width",    "100" ,  uiHeight ));
			_rgProperty.Add( Labels.Fixed,    new Input( Labels.Fixed,    "Fixed",    "Width", uiHeight ));
			_rgProperty.Add( Labels.FilePath, new Input( Labels.FilePath, "Path",     "",      uiHeight ));
			_rgProperty.Add( Labels.FileName, new Input( Labels.FileName, "Filename", "",      uiHeight ));

			SnipDoc  = new ImageSoloDoc   ( new SnipSlotBase( this ) );
			SnipView = new ImageViewSingle( new SnipSlotView( this ), SnipDoc ) {
				Parent = this
			};
		}

		public Guid      Catagory  => Guid;
		public string    Banner    => _oDocument.CurrentFileName + " : Snippet";
		public Image     Iconic    => null;
		public SKBitmap  Icon      { get; }
		public IPgParent Parentage => _oSiteBase.Host;
		public IPgParent Services  => Parentage.Services;

		public bool IsDirty => false;

		/// <summary>This is a little hacky. We're using the full fledged edit box for simple one line
		/// entry. We'll replace with a simpler single line editor in the future. See my property page work.</summary>
		/// <exception cref="InvalidOperationException" />
		protected LayoutControl CreateEditBox( Input oInput, bool fReadOnly ) {
			string strText = fReadOnly ? oInput._strLabel : oInput._strValue;
			try {
				Editor oEdit = new Editor( new SnipSlotBase( this ) );

				if( !oEdit.Load( strText ) ) {
					oEdit.Dispose();
					throw new InvalidOperationException();
				}

				EditWin oControl = new EditWin( new SnipSlotView( this ), oEdit, fReadOnly, fSingleLine:true ){
					ScrollBarWidths = 0F,
					Parent          = this,
					Height          = (int)oInput._uiHeight //Font.Height
				};

				if( !oControl.InitNew() ) {
					oControl.Dispose();
					oEdit   .Dispose();
					throw new InvalidOperationException();
				}

				if( !fReadOnly ) {
					_rgProperty[oInput._eLabel].Editor = oEdit;

					if( oInput._eLabel == Labels.Width ) {
						oEdit.BufferEvent += OnWidthChanged;
					}
					if( oInput._eLabel == Labels.Height ) {
						oEdit.BufferEvent += OnHeightChanged;
					}
				}

				return new LayoutControl( oControl, LayoutRect.CSS.Pixels, (uint)(Font.Height * 1.5) );
			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( InvalidOperationException ),
									typeof( ArgumentException ) };

				if( rgErrors.IsUnhandled( oEx ) )
					throw;

				throw new InvalidOperationException();
			}
		}

		public bool InitNew() {
			void LogError( Exception oEx ) {
				Type[] rgErrors = { typeof( ArgumentException ),
									typeof( ArgumentNullException ),
									typeof( NullReferenceException ),
									typeof( KeyNotFoundException ) };
				if( rgErrors.IsUnhandled( oEx ) )
					throw oEx;

				_oSiteBase.LogError( "Snip", "Couldn't set up View" );
			}

			if( !SnipView.InitNew() ) {
				_oSiteBase.LogError( "Snip", "Couldn't set up Preview." );
			}
			if( !SnipDoc.InitNew() ) {
				_oSiteBase.LogError( "Snip", "Couldn't set up Snip Saver." );
			}

			try {
                _rgVertStack.Add( new LayoutImageView(SnipView) ); // LayoutRect( LayoutRect.CSS.None )

                // We're close. Flex doesn't quite work with the default editor.
                // I've got timing issues for measuring the text.
                _rgTable.Add( new LayoutRect( LayoutRect.CSS.Percent, 20, 30 ) );
				_rgTable.Add( new LayoutRect( LayoutRect.CSS.Percent, 80, 70 ) );

				List<LayoutRect> rgRow = new List<LayoutRect>(1);

				foreach( KeyValuePair<Labels, Input> oPair in _rgProperty ) {
					rgRow = new List<LayoutRect>(2) {
						CreateEditBox(oPair.Value, true ),
						CreateEditBox(oPair.Value,false )
					};
					_rgTable.AddRow( rgRow );
				}
                _rgVertStack.Add( _rgTable );
			} catch( Exception oEx ) {
				LogError( oEx );
				return false;
			}

			SnipDoc.ImageUpdated += OnSnipChanged;

			// TODO: We'll add a setting to determine the aspect affinity.
			OnHeightChanged( BUFFEREVENTS.SINGLELINE );

			return true;
		}

		public bool Load(XmlElement oStream) {
			InitNew();

			return true;
		}

		protected override void Dispose( bool fDisposing ) {
			if( fDisposing && !_fDisposed ) {
				if( Icon != null && Icon is IDisposable oDisp )
					oDisp.Dispose();

				// TODO: Call dispose on the properties and such. Or check
				//       that they are disposing.

				_fDisposed = true;
			}
			base.Dispose( fDisposing );
		}

		public bool Save(XmlDocumentFragment oStream) {
			return true;
		}

		private void OnSnipChanged() {
			_rgProperty[Labels.Height].Number = 900;
		}

		protected override void OnSizeChanged( EventArgs e ) {
			base.OnSizeChanged(e);

            _rgVertStack.SetRect( 0, 0, Width, Height );
            _rgVertStack.LayoutChildren();
		}

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            foreach( IPgCommandView oView in _oSiteShell.EnumerateSiblings ) {
                if( oView is WindowSoloImageNav oViewSolo && oViewSolo.ID == _uiReturnID ) {
                    oViewSolo.FocusMe();
                }
            }
        }

        protected override void OnGotFocus(EventArgs e) {
			base.OnGotFocus(e);

            _oViewEvents.NotifyFocused( true );
		}

		static Type[] rgAspectErrors = { 
			typeof( ArgumentNullException ),
			typeof( ArgumentException ),
			typeof( FormatException ), 
			typeof( OverflowException ),
			typeof( ArgumentOutOfRangeException ),
			typeof( DivideByZeroException ) 
		};

		private void OnHeightChanged(BUFFEREVENTS eEvent) {
			if( ( eEvent & ( BUFFEREVENTS.SINGLELINE | BUFFEREVENTS.MULTILINE )) == 0 )
				return;
			if( _fBlockTextEvents )
				return;

			_fBlockTextEvents = true;

			try {
				int   iHeight    = this[Labels.Height].Number;

                // Limit the height to the actual height, no zooming...
                if( iHeight > SnipView.WorldCoordinates.Height ) {
                    iHeight = SnipView.WorldCoordinates.Height;
                    this[Labels.Height].Number = iHeight;
                }

                float flSlope    = SnipView.WorldCoordinates.GetSlope( LOCUS.UPPERLEFT );
				int   iNewWidth  = (int)Math.Round( iHeight / flSlope );
				
				this[Labels.Width].Number = iNewWidth;
			} catch( Exception oEx ) {
				if( rgAspectErrors.IsUnhandled( oEx ) ) 
					throw;
			} finally {
				_fBlockTextEvents = false;
			}
		}

		private void OnWidthChanged(BUFFEREVENTS eEvent) {
			if( ( eEvent & ( BUFFEREVENTS.SINGLELINE | BUFFEREVENTS.MULTILINE )) == 0 )
				return;
			if( _fBlockTextEvents )
				return;

			_fBlockTextEvents = true;

			try {
				int   iWidth  = this[Labels.Width].Number;

                // Limit the width to the actual width, no zooming...
                if ( iWidth > SnipView.WorldCoordinates.Width ) {
                    iWidth = SnipView.WorldCoordinates.Width;
                    this[Labels.Width].Number = iWidth;
                }

                float flSlope = SnipView.WorldCoordinates.GetSlope( LOCUS.UPPERLEFT );
				int   iHeight = (int)Math.Round( iWidth * flSlope );
				
				this[Labels.Height].Number = iHeight;
			} catch( Exception oEx ) {
				if( rgAspectErrors.IsUnhandled( oEx ) ) 
					throw;
			} finally {
				_fBlockTextEvents = false;
			}
		}

		public object Decorate(IPgViewSite oBaseSite, Guid sGuid) {
			return null;
		}

		/// <summary>Get the size the user would like the snippet to be.</summary>
		/// <exception cref="InvalidDataException" />
		public SKSizeI SnipSize {
			get {
				try {
					int iWidth  = _rgProperty[Labels.Width] .Number;
					int iHeight = _rgProperty[Labels.Height].Number;

					return new SKSizeI(iWidth, iHeight );
				} catch( Exception oEx ) {
					if( rgAspectErrors.IsUnhandled( oEx ))
						throw;

					throw new InvalidDataException( "Width and Height are an invalid format." );
				}
			}
		}

		public bool SnipMake( SmartRect rcSnipRect, uint uiReturnID ) {
			try {
				_rgProperty[Labels.FileName].Text = _oDocument.CurrentFileName;
				_rgProperty[Labels.FilePath].Text = Path.GetDirectoryName( _oDocument.CurrentFullPath );

				// Copy the snip section currently selected. This is the full resolution original.
				if( !SnipDoc.Load( _oDocument.Bitmap, rcSnipRect.SKRect, rcSnipRect.SKRect.Size ) ) {
					_oSiteBase.LogError( "Snip Save", "Could not create the snip copy from source." );
					return false;
				}

                _uiReturnID = uiReturnID;

				return true;
            } catch( Exception oEx ) {
				Type[] rgErrors = { 
					typeof( ArgumentNullException ),
					typeof( ArgumentException ) };

				if( rgErrors.IsUnhandled( oEx ) )
					throw;
				
				_oSiteBase.LogError( "Snip Save", "Could not create the snip copy." );
				return false;
			}
		}

		/// <summary>
		/// Set up a path for the save location of the bitmap. Create the snip and then save it.
		/// </summary>
		/// <remarks>
		/// Hacky blocking save dialog for our snip. In the future we'll make a non modal dialog.
		/// </remarks>
		public void SnipSave( bool fSaveAs ) {
			try {
				// If we've got a filename try that path first. 
				if( string.IsNullOrEmpty( _rgProperty[Labels.FilePath].Text ) )
					fSaveAs = true;

				if( fSaveAs == true ) {
					SaveFileDialog oDialog = new SaveFileDialog();

					oDialog.InitialDirectory = _rgProperty[Labels.FilePath].Text;
					oDialog.FileName         = _rgProperty[Labels.FileName].Text;

					oDialog.ShowDialog();

					if( oDialog.FileName == null || oDialog.FileName.Length == 0 || !oDialog.CheckPathExists ) {
						_oSiteBase.LogError( "ViewSnip", "Please supply a valid file name for your next Save request. ^_^;" );
						return;
					}

					_rgProperty[Labels.FilePath].Text = Path.GetDirectoryName( oDialog.FileName );
					_rgProperty[Labels.FileName].Text = Path.GetFileName     ( oDialog.FileName );
				} else {
 					if (File.Exists(Path.Combine( _rgProperty[Labels.FilePath].Text, _rgProperty[Labels.FileName].Text ) ) ) {
						DialogResult eResult = MessageBox.Show("Do you wish to over write the file.", "File Exists", MessageBoxButtons.YesNo );

						if( eResult != DialogResult.Yes )
							return;
					}
				}

			} catch( Exception oEx ) {
				Type[] rgErrors = { typeof( PathTooLongException ),
									typeof( ArgumentException ) };
				if( rgErrors.IsUnhandled( oEx ))
					throw;

				_oSiteBase.LogError( "ViewSnip", "Problem with file paths given. ");
				return;
			}

			try {
                using (ImageSoloDoc oSnipTemp = new ImageSoloDoc(new SnipSlotBase(this))) {
					// Now we save a resized version of the snip.
                    if (!oSnipTemp.Load(SnipDoc.Bitmap, SnipView.WorldCoordinates.SKRect, SnipSize)) {
                        _oSiteBase.LogError("Snip Save", "Could not create the snip copy, sizing error.");
                        return;
                    }
                    string strFullName = Path.Combine( _rgProperty[Labels.FilePath].Text,
                                                       _rgProperty[Labels.FileName].Text );
                    using( Stream oStream = File.Open( strFullName, FileMode.Create )) {
                        if( !oSnipTemp.Save(oStream)) {
                            _oSiteBase.LogError("Snip Save", "Could not create the snip copy, stream error.");
                            return;
                        }
                        oStream.Flush();
                    }
                }
                _oDocument.LoadAgain( _oDocument.CurrentFullPath );
            } catch( Exception oEx ) {
				Type[] rgErrors = { 
					typeof( ArgumentNullException ),
					typeof( ArgumentException ),
					typeof( DirectoryNotFoundException ),
					typeof( IOException ),
					typeof( UnauthorizedAccessException ),
					typeof( PathTooLongException ),
					typeof( SecurityException ),
					typeof( NullReferenceException ),
					typeof( ApplicationException )};

				if( rgErrors.IsUnhandled( oEx ) )
					throw;
				
				_oSiteBase.LogError( "Snip Save", "Blowing chunks in snip save." );
			}

		}

		public bool Execute(Guid sGuid) {
			if( sGuid == GlobalCommands.Save ) {
				SnipSave( fSaveAs:false );
				return true;
			}
			if( sGuid == GlobalCommands.SaveAs ) {
				SnipSave( fSaveAs:true );
				return true;
			}

			return false;
		}
	}
}
