using System;
using System.Xml;
using System.Collections.Generic;
using System.Text;

using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Rectangles;
using Play.Edit;
using Play.ImageViewer;
using Play.Sound;

namespace Play.SSTV {
	/// <summary>
	/// A little subclass of the editwindow to turn on the check marks. turn on readonly and have multiline.
	/// </summary>
	public class CheckList : EditWindow2 {
		public CheckList( IPgViewSite oSite, Editor oEditor ) : base( oSite, oEditor, fReadOnly:true, fSingleLine:false ) {
			_fCheckMarks = true;
			ToolSelect   = 2; // BUG: change this to an enum in the future.
		}
	}

	/// <summary>
	/// This is a old view so we can select a transmit image. Basically a slightly
	/// motified directory viewer. I'm going to leave it, in case I need a simplified
	/// veiwer for some reason.
	/// </summary>
	public class ViewTransmitSolo: 
		WindowSoloImageNav 
	{
		public static Guid GUID { get; } = new Guid( "{5BC25D2B-3F4E-4339-935C-CFADC2650B35}" );

        public override Guid   Catagory => GUID;

        public override string Banner {
			get { 
				StringBuilder sbBanner = new StringBuilder();

				sbBanner.Append("MySSTV Transmit");
				if( _oDocSSTV.PortTxList.CheckedLine is Line oLine ) {
					sbBanner.Append( " : " );
					sbBanner.Append( oLine.ToString() );
				}

				return sbBanner.ToString();
			} 
		}

        DocSSTV _oDocSSTV;

		public ViewTransmitSolo( IPgViewSite oSiteBase, DocSSTV oDocSSTV ) : base( oSiteBase, oDocSSTV.TxImageList ) {
			_oDocSSTV = oDocSSTV ?? throw new ArgumentNullException( "oDocSSTV must not be null." );
		}

        protected override void Dispose( bool fDisposing )
        {
			if( fDisposing && !_fDisposed ) {
				_oDocSSTV.PropertyChange -= ListenDoc_PropertyChange;
			}
			base.Dispose( fDisposing );
        }

        public override bool InitNew()
        {
            if( !base.InitNew() )
				return false;

			ToolSelect = 0; // Crude but should be sufficient to freeze Skywalker...
            _oDocSSTV.PropertyChange += ListenDoc_PropertyChange;

			Aspect   = _oDocSSTV.Resolution;
			DragMode = DragMode.FixedRatio;

			return true;
        }

        private void ListenDoc_PropertyChange( SSTVEvents eProp )
        {
            switch( eProp ) {
				case SSTVEvents.DownLoadTime:
					Invalidate();
					break;
				case SSTVEvents.SSTVMode:
					Aspect = _oDocSSTV.Resolution;
					break;
			}
        }

        public override bool Execute( Guid sGuid ) {
			if( sGuid == GlobalCommands.Play ) {
				throw new NotImplementedException();
				// Need to update to new Deluxe window way of setting up the transmit.
			    _oDocSSTV.TransmitBegin( null ); 
			}
			if( sGuid == GlobalCommands.Stop ) {
				_oDocSSTV.TransmitStop();
			}

            return base.Execute( sGuid );
        }

        public override object Decorate( IPgViewSite oBaseSite, Guid sGuid ) {
			if( sGuid.Equals(GlobalDecorations.Properties) ) {
				return new ViewSSTVProperties( oBaseSite, _oDocSSTV.TxProperties );
			}
			if( sGuid.Equals( GlobalDecorations.Outline ) ) {
				return new CheckList( oBaseSite, _oDocSSTV.TxModeList );
			}
			if( sGuid.Equals( GlobalDecorations.Options ) ) {
				return new ImageViewIcons( oBaseSite, _oDocSSTV.TxImageList );
			}
            return base.Decorate( oBaseSite, sGuid );
        }
    }

	public class ViewTransmitDeluxe: 
		WindowStaggardBase,
		IPgSave<XmlDocumentFragment>,
		IPgLoad<XmlElement>
	{
		public static Guid GUID { get; } = new Guid( "{3D6FF540-C03C-468F-84F9-86E3DE75F6C2}" );

        public    override Guid   Catagory => GUID;
        protected override string IconResource => "Play.SSTV.icons8_camera.png";

		protected readonly WindowSoloImageNav _wmTxImageChoice;
		protected readonly ImageViewSingle    _wmTxImageComposite;
		protected readonly ImageViewIcons     _wmTxViewChoices;

        public override string Banner {
			get { 
				StringBuilder sbBanner = new StringBuilder();

				sbBanner.Append("MySSTV Transmit");
				if( _oDocSSTV.PortTxList.CheckedLine is Line oLine ) {
					sbBanner.Append( " : " );
					sbBanner.Append( oLine.ToString() );
				}

				return sbBanner.ToString();
			} 
		}

        public ViewTransmitDeluxe( IPgViewSite oSiteBase, DocSSTV oDocSSTV ) : base( oSiteBase, oDocSSTV ) {
			_wmTxImageChoice    = new WindowSoloImageNav( new SSTVWinSlot( this, ChildID.TxImage ),        oDocSSTV.TxImageList );
			_wmTxViewChoices    = new ImageViewIcons    ( new SSTVWinSlot( this, ChildID.TxImageChoices ), oDocSSTV.TxImageList );
			_wmTxImageComposite = new ImageViewSingle   ( new SSTVWinSlot( this, ChildID.TxImageSnip ),    oDocSSTV.TxBitmapComp );

			_wmTxImageChoice   .Parent = this;
			_wmTxViewChoices   .Parent = this;
			_wmTxImageComposite.Parent = this;

			_wmTxImageChoice.SetBorderOn();
		}

        protected override void Dispose( bool fDisposing ) {
			if( fDisposing && !_fDisposed ) {
				_oDocSSTV.PropertyChange -= ListenDoc_PropertyChange;

				_wmTxImageChoice   .Dispose();
				_wmTxViewChoices   .Dispose();
				_wmTxImageComposite.Dispose();

				_fDisposed = true;
			}
			base.Dispose( fDisposing );
        }

        public bool InitNew() {
			if( !_wmTxImageChoice   .InitNew() )
				return false;
			if( !_wmTxViewChoices   .InitNew() )
				return false;
			if( !_wmTxImageComposite.InitNew() )
				return false;

            _oDocSSTV.PropertyChange += ListenDoc_PropertyChange;

			_wmTxImageChoice.ToolSelect = 0; 
			_wmTxImageChoice.Aspect     = _oDocSSTV.Resolution;
			_wmTxImageChoice.DragMode   = DragMode.FixedRatio;

			_rgSubLayout.Add(new LayoutControl( _wmTxImageComposite, LayoutRect.CSS.None) );
			_rgSubLayout.Add(new LayoutControl( _wmTxImageChoice,    LayoutRect.CSS.None) );

			_oLayout.Add( _rgSubLayout );
            _oLayout.Add( new LayoutControl( _wmTxViewChoices, LayoutRect.CSS.Pixels, 220 ) );

            OnSizeChanged( new EventArgs() );
			return true;
        }

        private void ListenDoc_PropertyChange( SSTVEvents eProp ) {
            switch( eProp ) {
				case SSTVEvents.DownLoadTime:
					_wmTxImageChoice.Invalidate();
					break;
				case SSTVEvents.SSTVMode:
					_wmTxImageChoice.Aspect = _oDocSSTV.Resolution; // BUG, get this from my TxMode.
					break;
			}
        }

		protected SSTVMode SSTVModeSelection { 
			get {
                if( _oDocSSTV.TxModeList.CheckedLine == null )
                    _oDocSSTV.TxModeList.CheckedLine = _oDocSSTV.TxModeList[_oDocSSTV.RxModeList.CheckedLine.At];

                if( _oDocSSTV.TxModeList.CheckedLine.Extra is SSTVMode oMode )
					return oMode;

				return null;
			}
		}

        public override bool Execute( Guid sGuid ) {
			if( sGuid == GlobalCommands.Play ) {
                if( SSTVModeSelection is SSTVMode oMode ) {
					SKRectI rcComp = new SKRectI( 0, 0, oMode.Resolution.Width, oMode.Resolution.Height);
					SKSizeI ptComp = new SKSizeI( oMode.Resolution.Width, oMode.Resolution.Height );

					_oDocSSTV.TxBitmapSnip.Load( _oDocSSTV.TxBitmap, _wmTxImageChoice.Selection.SKRect, oMode.Resolution );
					_oDocSSTV.TxBitmapComp.Load( _oDocSSTV.TxBitmap, rcComp, ptComp );

					_oDocSSTV.TransmitBegin( oMode ); 
				}
				return true;
			}
			if( sGuid == GlobalCommands.Stop ) {
				_oDocSSTV.TransmitStop();
				return true;
			}

            if( sGuid == GlobalCommands.StepLeft ) {
                _oDocSSTV.TxImageList.Next( -1 );
                return( true );
            }
            if( sGuid == GlobalCommands.StepRight ) {
                _oDocSSTV.TxImageList.Next( +1 );
                return( true );
            }
            if( sGuid == GlobalCommands.JumpParent ) {
                _oDocSSTV.TxImageList.DirectoryNext( 0 );
                return( true );
            }
            if( sGuid == GlobalCommands.JumpPrev ) {
               _oDocSSTV.TxImageList. DirectoryNext( -1 );
                return( true );
            }
            if( sGuid == GlobalCommands.JumpNext ) {
                _oDocSSTV.TxImageList.DirectoryNext( +1 );
                return( true );
            }
			return false;
        }

        public override object Decorate( IPgViewSite oBaseSite, Guid sGuid ) {
			if( sGuid.Equals(GlobalDecorations.Properties) ) {
				return new ViewSSTVProperties( oBaseSite, _oDocSSTV.TxProperties );
			}
			if( sGuid.Equals( GlobalDecorations.Outline ) ) {
				return new CheckList( oBaseSite, _oDocSSTV.TxModeList );
			}
			if( sGuid.Equals( GlobalDecorations.Options ) ) {
				return new CheckList( oBaseSite, _oDocSSTV.TemplateList ) { ReadOnly = false };
			}
			return null;
        }

        public bool Load(XmlElement oStream) {
			if( !InitNew() )
				return false;

            return true;
        }

        public bool Save(XmlDocumentFragment oStream) {
            return true;
        }

        protected override void BringChildToFront(ChildID eID) {
			switch( eID ) {
				case ChildID.TxImageChoices:
					_wmTxImageChoice.BringToFront();
					_wmTxImageChoice.Select();
					break;
				case ChildID.TxImage:
					_wmTxImageChoice.BringToFront();
					break;
				case ChildID.TxImageSnip:
					if( _oDocSSTV.Status == WorkerStatus.FREE ) {

						// Might want to add an window selection changed event to cause this to update...
						if( SSTVModeSelection is SSTVMode oMode ) {
							SKRectI rcComp = new SKRectI( 0, 0, oMode.Resolution.Width, oMode.Resolution.Height);
							SKSizeI ptComp = new SKSizeI( oMode.Resolution.Width, oMode.Resolution.Height );

							_oDocSSTV.TxBitmapSnip.Load( _oDocSSTV.TxBitmap, _wmTxImageChoice.Selection.SKRect, oMode.Resolution ); 
							_oDocSSTV.TxBitmapComp.Load( _oDocSSTV.TxBitmap, rcComp, ptComp ); // Render needs this, for now.
							_oDocSSTV.TxBitmapComp.RenderImage();
						} else {
							_oDocSSTV.TxBitmapSnip.BitmapDispose(); // TODO: I'd really like to have the error image up.
						}
					}
					_wmTxImageComposite.BringToFront();
					break;
			}
        }
    }

}
