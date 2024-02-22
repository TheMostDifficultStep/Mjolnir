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
using Play.Forms;

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

		protected readonly ImageWalkerDoc  _oDocument;
		protected readonly IPgViewSite     _oSiteBase;
        protected readonly IPgShellSite    _oSiteShell;
        protected readonly IPgViewNotify   _oViewEvents; // From our site, window manager (view interface).

		protected WindowStandardProperties PropertiesWin;
		internal  ImageSnipProperties      PropertiesDoc;

        readonly LayoutStackVertical _rgVertStack = new LayoutStackVertical() { Spacing = 5 };

		protected ImageSoloDoc    SnipDoc { get; }
		protected ImageViewSingle SnipView{ get; }

        protected uint _uiReturnID       = 0; // Which view to return to after snip save.
		protected bool _fBlockTextEvents = false;
		protected bool _fDisposed        = false;

        public ViewSnipDialog( IPgViewSite oSiteView, ImageWalkerDoc oDoc ) {
			_oSiteBase   = oSiteView ?? throw new ArgumentNullException( "View Site must not be null." );
            _oSiteShell  = oSiteView as IPgShellSite ?? throw new ArgumentException( "View Site must support IPgShellSite" );
			_oViewEvents = oSiteView.EventChain ?? throw new ArgumentException( "Site.EventChain must support IPgViewSiteEvents" );
			_oDocument   = oDoc ?? throw new ArgumentNullException( "Document must not be null." );
			Icon         = _oDocument.GetResource( "icons8-cut-40.png" );

			SnipDoc  = new ImageSoloDoc   ( new SnipSlotBase( this ) );
			SnipView = new ImageViewSingle( new SnipSlotView( this ), SnipDoc ) {
				Parent = this
			};

			PropertiesDoc = new ImageSnipProperties     ( new SnipSlotBase( this ) );
			PropertiesWin = new WindowStandardProperties( new SnipSlotView( this ), PropertiesDoc );
		}

		public Guid      Catagory  => Guid;
		public string    Banner    => _oDocument.CurrentFileName + " : Snippet";
		public Image     Iconic    => null;
		public SKBitmap  Icon      { get; }
		public IPgParent Parentage => _oSiteBase.Host;
		public IPgParent Services  => Parentage.Services;

		public bool IsDirty => false;

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
				return false;
			}
			if( !SnipDoc.InitNew() ) {
				_oSiteBase.LogError( "Snip", "Couldn't set up Snip Saver." );
				return false;
			}
			if( !PropertiesDoc.InitNew() ) {
				_oSiteBase.LogError( "Snip", "Couldn't set up Snip Properties." );
				return false;
			}
			if( !PropertiesWin.InitNew() ) {
				_oSiteBase.LogError( "Snip", "Couldn't set up Snip Property Viewer." );
				return false;
			}


			PropertiesWin.Parent = this;
			SnipView     .Parent = this;

			try {
                _rgVertStack.Add( new LayoutImageView( SnipView,      LayoutRect.CSS.None ) { TrackMaxPercent = 0.7F } ); 
                _rgVertStack.Add( new LayoutControl  ( PropertiesWin, LayoutRect.CSS.Flex ) { TrackMaxPercent = 0.3F } );
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

				SnipDoc.ImageUpdated -= OnSnipChanged;
				_fDisposed = true;
			}
			base.Dispose( fDisposing );
		}

		public bool Save(XmlDocumentFragment oStream) {
			return true;
		}

		private void OnSnipChanged() {
			PropertiesDoc.ValueUpdate( (int)ImageSnipProperties.Labels.Height, "900", true );

			// In the old days we could track a single event when this changed since each
			// property was a single line editor. 
			OnHeightChanged( BUFFEREVENTS.SINGLELINE );
		}

		protected override void OnSizeChanged( EventArgs e ) {
			base.OnSizeChanged(e);

			if( Width > 0 && Height > 0 ) {
				_rgVertStack.SetRect( 0, 0, Width, Height );
				_rgVertStack.LayoutChildren();
			}
		}

        protected void JumpBack()
        {
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

		/// <summary>Call this if the Height property changes.</summary>
		/// <remarks>Unfortunately, we don't get individual property line change events
		/// and so we can only call this right now when the snip changes. This is ok since
		/// I hardly ever adjust the size of a snip. But it is a BUG.</remarks>
		private void OnHeightChanged(BUFFEREVENTS eEvent) {
			if( ( eEvent & ( BUFFEREVENTS.SINGLELINE | BUFFEREVENTS.MULTILINE )) == 0 )
				return;
			if( _fBlockTextEvents )
				return;

			_fBlockTextEvents = true;

			try {
				int iHeight = PropertiesDoc.ValueAsInt( (int)ImageSnipProperties.Labels.Height, 0 );

                // Limit the height to the actual height, no zooming...
                if( iHeight > SnipView.WorldCoordinates.Height ) {
                    iHeight = SnipView.WorldCoordinates.Height;
                    PropertiesDoc.ValueUpdate( (int)ImageSnipProperties.Labels.Height, iHeight.ToString() );
                }

                float flSlope    = SnipView.WorldCoordinates.GetSlope( LOCUS.UPPERLEFT );
				int   iNewWidth  = (int)Math.Round( iHeight / flSlope );
				
                PropertiesDoc.ValueUpdate( (int)ImageSnipProperties.Labels.Width, iNewWidth.ToString() );
			} catch( Exception oEx ) {
				if( rgAspectErrors.IsUnhandled( oEx ) ) 
					throw;
			} finally {
				_fBlockTextEvents = false;
			}
		}

		/// <summary>Call this if the Width property changes.</summary>
		/// <remarks>Unfortunately, we don't get individual property line change events
		/// and so we can only call this right now when the snip changes. This is ok since
		/// I hardly ever adjust the size of a snip. But it is a BUG.</remarks>
		private void OnWidthChanged(BUFFEREVENTS eEvent) {
			if( ( eEvent & ( BUFFEREVENTS.SINGLELINE | BUFFEREVENTS.MULTILINE )) == 0 )
				return;
			if( _fBlockTextEvents )
				return;

			_fBlockTextEvents = true;

			try {
				int   iWidth  = PropertiesDoc.ValueAsInt( (int)ImageSnipProperties.Labels.Width, 0);

                // Limit the width to the actual width, no zooming...
                if ( iWidth > SnipView.WorldCoordinates.Width ) {
                    iWidth = SnipView.WorldCoordinates.Width;
					PropertiesDoc.ValueUpdate( (int)ImageSnipProperties.Labels.Width, iWidth.ToString() );
                }

                float flSlope = SnipView.WorldCoordinates.GetSlope( LOCUS.UPPERLEFT );
				int   iHeight = (int)Math.Round( iWidth * flSlope );
				
                PropertiesDoc.ValueUpdate( (int)ImageSnipProperties.Labels.Height, iHeight.ToString() );
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
					int iWidth  = PropertiesDoc.ValueAsInt((int)ImageSnipProperties.Labels.Width, 0);
					int iHeight = PropertiesDoc.ValueAsInt((int)ImageSnipProperties.Labels.Height, 0);

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
				PropertiesDoc.ValueUpdate( (int)ImageSnipProperties.Labels.FileName, _oDocument.CurrentFileName );
				PropertiesDoc.ValueUpdate( (int)ImageSnipProperties.Labels.FilePath, Path.GetDirectoryName( _oDocument.CurrentFullPath ), true );

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
				string strInitialDir  = PropertiesDoc[ (int)ImageSnipProperties.Labels.FilePath].ToString();
				string strInitialFile = PropertiesDoc[ (int)ImageSnipProperties.Labels.FileName].ToString();

				if( string.IsNullOrEmpty( strInitialDir ) )
					fSaveAs = true;

				if( fSaveAs == true ) {
					SaveFileDialog oDialog = new SaveFileDialog();

					oDialog.InitialDirectory = strInitialDir;
					oDialog.FileName         = strInitialFile;

					oDialog.ShowDialog();

					if( oDialog.FileName == null || oDialog.FileName.Length == 0 || !oDialog.CheckPathExists ) {
						_oSiteBase.LogError( "ViewSnip", "Please supply a valid file name for your next Save request. ^_^;" );
						return;
					}

					PropertiesDoc.ValueUpdate( (int)ImageSnipProperties.Labels.FilePath, Path.GetDirectoryName( oDialog.FileName ) );
					PropertiesDoc.ValueUpdate( (int)ImageSnipProperties.Labels.FileName, Path.GetFileName     ( oDialog.FileName ) );
				} else {
 					if (File.Exists(Path.Combine( strInitialDir, strInitialFile ) ) ) {
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
                    string strFullName = Path.Combine( PropertiesDoc.ValueGetAsStr( (int)ImageSnipProperties.Labels.FilePath ),
                                                       PropertiesDoc.ValueGetAsStr( (int)ImageSnipProperties.Labels.FileName ) );
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
			if( sGuid == GlobalCommands.JumpParent ) {
				JumpBack();
				return true;
			}

			return false;
		}
	}
}
