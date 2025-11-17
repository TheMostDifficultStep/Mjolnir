using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Xml;
using System.Collections;

using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Rectangles;
using Play.Edit;

namespace Mjolnir {
    /// <summary>
    /// This object holds all the adornments on one side, with spacers between elements.
    /// </summary>
	public class SideRect : 
		LayoutStack,
		IEnumerable<SmartHerderBase>
	{
		readonly List<SmartBinder> _rgSpacers = new List<SmartBinder>();
        public bool IsDragClosed = false;

		public SideRect( TRACK eDir ) : base( eDir, 0 ) {
		}

		public SideRect( TRACK eDir, uint uiTrack, float flMaxPercent ) : 
			base( eDir, uiTrack, flMaxPercent ) 
		{
		}

		IEnumerable<SmartBinder> Spacers => _rgSpacers;

        public int SideInit  { get; set; } = 0;

		public override void Paint(Graphics oGraphics) {
			foreach( SmartRect rcChild in _rgLayout ) {
				rcChild.Paint( oGraphics );
			}
		}

		public override void Paint(SKCanvas skCanvas) {
			foreach( SmartRect rcChild in _rgLayout ) {
				rcChild.Paint( skCanvas );
			}
		}

		public override void Clear() {
			base.Clear();
			_rgSpacers.Clear();
		}

		public LayoutRect Last { get { return _rgLayout[Count-1]; } }

		public int Load( IEnumerable<SmartHerderBase> rgSource ) {
            int iTrack = 0;

			foreach( SmartHerderBase oChild in rgSource ) {
				Add( oChild );
                iTrack += (int)oChild.Track;
			}

            return iTrack;
		}

		/// <exception cref="ArgumentNullException" />
		public override void Add( LayoutRect oNext ) {
			if( oNext == null )
				throw new ArgumentNullException();

			if( Count > 0 ) {
				SmartBinder oSpacer = new SmartSpacer( Direction, Last, oNext, (int)Spacing );

				_rgSpacers.Add( oSpacer );
				base      .Add( oSpacer );
			}
			base.Add( oNext );
		}

		/// <summary>
		/// Return true if mouse within any of the spacers.
		/// </summary>
		/// <param name="fChanged">State of any spacer has changed.</param>
		/// <returns></returns>
		public bool Hover(int iX, int iY, out bool fChanged) {
			bool fInside = false;
			fChanged = false;

			foreach( SmartBinder oGlue in _rgSpacers ) {
				fInside  |= oGlue.Hover( iX, iY, out bool fGlueChanged );
				fChanged |= fGlueChanged;
			}

			return fInside;
		}

		public void HoverStop() {
			foreach( SmartBinder oSpacer in _rgSpacers ) {
				oSpacer.HoverStop();
			}
		}

		/// <summary>
		/// Automatically set the track percentages based on the current child extent in the track direction.
		/// </summary>
		/// <param name="fNormalize">Divide the space equally among children if true.</param>
		/// <remarks>While the current track occupied might be more or less than the future track, our
		/// percentages should add up to 100 and everyone will fit the new track size.</remarks>
		public void PercentReset( bool fNormalize ) {
            int iAdjustables   = 0;
            int iAdjustableExt = 0;

            foreach( LayoutRect oChild in _rgLayout ) {
                if( oChild.Units == CSS.Percent ) {
                    iAdjustableExt += oChild.GetExtent( Direction );
                    iAdjustables++;
                }
            }

			if( iAdjustables == 0 )
				return;
			if( iAdjustableExt <= 0 )
				iAdjustableExt = GetExtent( Direction );
			if( iAdjustableExt <= 0 )
				return;

			foreach( LayoutRect oChild in _rgLayout ) {
				if( oChild.Units == CSS.Percent ) {
					if( fNormalize ) {
						oChild.Track = (uint)( 100 / iAdjustables ); // Evenly distribution.
					} else {
						oChild.Track = (uint)( 100 * oChild.GetExtent( Direction ) / iAdjustableExt);
					}
				}
            }
		}

		protected void SpacerDragFinish( object oSpacer, SKPointI pntLast ) {
            PercentReset( fNormalize:false );
		}

		public SmartGrabDrag SpacerDragTry( int iX, int iY ) {
			foreach( SmartBinder oGlue in _rgSpacers ) {
				if( oGlue.IsInside( iX, iY ) ) {
                    return new SmartSpacerDrag( new DragFinished( SpacerDragFinish ), oGlue, Direction, iX, iY );
				}
			}
			return null;
		}

        /// <summary>
        /// Returns all the SmartHerderBase objects in the layout, the rest are ignored.
        /// </summary>
		public IEnumerator<SmartHerderBase> ChildrenEnumerate() {
			foreach (LayoutRect oRect in _rgLayout) {
				if (oRect is SmartHerderBase oHerder)
					yield return oHerder;
			}
		}

		IEnumerator<SmartHerderBase> IEnumerable<SmartHerderBase>.GetEnumerator() {
			return ChildrenEnumerate();
		}

		// was public IEnumerator GetEnumerator(), but the compiler was whining.
		public override IEnumerator<LayoutRect> GetEnumerator() {
			return ChildrenEnumerate();
		}
	} // End Class

    public partial class MainWin {
        // Look at making this a struct 
        public class MainWinDecorMenus :
            IEnumerable<IPgMenuVisibility> 
        {
            readonly MainWin _oHost;

            public MainWinDecorMenus( MainWin oHost ) {
                _oHost = oHost;
                if( _oHost == null )
                    throw new ArgumentNullException();
            }

            public IEnumerator<IPgMenuVisibility> GetEnumerator() {
                if( _oHost._miDecorMenu == null )
                    yield break;

                foreach( ToolStripItem oCurrentItem in _oHost._miDecorMenu.DropDownItems ) {
                    IPgMenuVisibility oCurrentMenuItem = oCurrentItem as IPgMenuVisibility;
                    if( oCurrentMenuItem != null )
                        yield return( oCurrentMenuItem );
                }
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return( GetEnumerator() );
            }
        }

        protected IEnumerable<IPgMenuVisibility> DecorSettings {
            get { return _rgDecorEnum; }
        } 
        /// <summary>
        /// Do this after all the tools, solo, and document adornments are added.
        /// TODO: The only way to open dockings is via the menu. That means I need to get to the
        ///       Find window menu item to open via CTRL-F from the editor. Need to fix that!
        /// </summary>
        protected void DecorMenuReload() {
            if( _miDecorMenu != null ) {
                _miDecorMenu.DropDownItems.Clear();

                ToolStripMenuItem oItem1 = new ToolStripMenuItem( "Visible", null, new EventHandler( OnDecorToggle ) );
				oItem1.Checked = !_rcFrame.Hidden;
				//( "Visible", new EventHandler( this.OnDecorToggle  ) );
                //ToolStripMenuItem oItem1 = new ToolStripMenuItem( "Show", BitmapCreateFromChar( "\xE12c" ), new EventHandler( this.OnViewRestore  ) );
                _miDecorMenu.DropDownItems.Add( oItem1 );

                //ToolStripMenuItem oItem2 = new ToolStripMenuItem( "Hide", BitmapCreateFromChar( "\xE002" ), new EventHandler( this.OnViewMaximize ) );
                //_miToolsMenu.DropDownItems.Add( oItem2 );
                _miDecorMenu.DropDownItems.Add( new ToolStripSeparator() );

                foreach( SmartHerderBase oShepard in this ) {
				    ToolStripMenuItem oItem = new MenuItemHerder( oShepard, new EventHandler( OnDecorMenuClick ) );
				    oItem.Checked = !oShepard.Hidden;
				    _miDecorMenu.DropDownItems.Add( oItem );
			    }
            }
        }

        protected struct DecorProperties {
            public readonly string File;
            public readonly bool   Solo;
            [Obsolete]public readonly string Name;
            public DecorProperties( string strName, string strResourceName, bool fSolo ) {
                File = "Content." + strResourceName;
                Solo = fSolo;
                Name = strName;
            }
        }

        /// <summary>
        /// Read out the positions of the tools from the config file. This represents the "default" layout
        /// need to consider this in concert with the newer "pvs" session files.
        /// </summary>
        /// <param name="xmlDocument">Config xml file.</param>
        protected void InitializeShepards( XmlDocument xmlDocument ) {
            XmlNodeList  lstTools   = xmlDocument.SelectNodes("config/mainwindow/docking/dock");
            Point        ptOrigin   = new Point();

            // This will be needed for the new smart herder implementation
            // but just leave it for now.
            IPgStandardUI2 oStdUI   = (IPgStandardUI2)Services;
            uint           uStdFont = oStdUI.FontCache( oStdUI.FaceCache(@"C:\windows\fonts\consola.ttf"), 
                                                        10, MainDisplayInfo.pntDpi );
            IPgFontRender  oRender  = oStdUI.FontRendererAt( uStdFont );

            foreach( XmlElement xeType in lstTools ) {
                string strToolEdge = xeType.GetAttribute("edge");
                string strToolDisp = xeType.GetAttribute("display" );
				string strToolVis  = xeType.GetAttribute("visible" );
                string strToolGuid = xeType.GetAttribute("decor" );

                SideIdentify eEdge  = SideIdentify.Bottom;
                Guid         gDecor = Guid.Empty;

                foreach( SideIdentify eSide in _rgSideInfo.Keys ) {
                    if( string.Compare( strToolEdge, eSide.ToString().ToLower() ) == 0 ) {
                        eEdge = eSide;
                        break;
                    }
                }

                try {
                    gDecor = new Guid( strToolGuid );
                } catch ( Exception oEx ) {
                    Type[] rgError = { typeof(ArgumentNullException), 
                                       typeof(FormatException), 
                                       typeof(OverflowException) };
                    if( rgError.IsUnhandled( oEx ) )
                        throw new ApplicationException( "unexpected error reading tool guid", oEx );
                }
                
                try {
                    // A herder is the outer frame holding all the dialogs of a particular
                    // function, like outlines, for all the documents in the system. This
                    // is the piece that gets dragged around if the user want's to see it in
                    // a different place on the edges.
                    SmartHerderBase oShepard;
                    string          strResource = _rgStdDecor[gDecor].File;
                    bool            fSolo       = _rgStdDecor[gDecor].Solo;

                    if( fSolo )
                        oShepard = new SmartHerderSolo( this, strResource, strToolDisp, gDecor /*, oRender */);
                    else
                        oShepard = new SmartHerderClxn( this, strResource, strToolDisp, gDecor /*, oRender */ );

                    // Unfortunately the corner boxes won't be set until we've got our window size.
                    // so just set some arbitray size for now.
                    oShepard.SetRect(LOCUS.UPPERLEFT, ptOrigin.X, ptOrigin.Y, 30, 30 );
                    oShepard.Orientation = eEdge;
                    oShepard.Sizing      = SMARTSIZE.Normal;

                    // NOTE: This isn't the final say. The item must also have content,
                    //       but at this point we don't know... :-/
				    if( strToolVis.ToLower() == "true" )
					    oShepard.Show = SHOWSTATE.Active;
				    else
					    oShepard.Hidden = true;

                    //if( gDecor == GlobalDecor.Outline ) // TODO: Experimental.
                    //    oShepard.HideTitle = true;
                    //if( gDecor == GlobalDecor.Command ) // TODO: More experimental. Don't have a command bar 11/8/2024
                    //    oShepard.Margin = new SmartRect( 5, 5, 5, 5 );

                    _rgShepards.Add( gDecor, oShepard );
                } catch( Exception oEx ) {
                    Type[] rgErrors = { typeof( ArgumentOutOfRangeException ),
                                        typeof( ArgumentNullException ),
                                        typeof( NullReferenceException ),
                                        typeof( InvalidOperationException ) };
                    if( rgErrors.IsUnhandled( oEx ) )
                        throw;

                    LogError( null, "Decor", "Unable to Init a dacor: " + strToolDisp );
                }
                // Stagger all the windows both in x and y so that when we try to arrange them after
                // our main window get's sized they'll be roughly in the order loaded.
                ptOrigin.Offset( 5, 5 );
            }

            DecorMenuReload();               // LayoutLoadShepardsAt depends on this!!
			// loads the shepards into the sides BUT NOT THE decor in the shepard!!
            foreach( SideIdentify eSide in _rgSideInfo.Keys ) {
                LayoutLoadShepardsAt(eSide); // Menu must be loaded by now!!
            }
        }

        /// <summary>
        /// Normally we try read all the adornments we need form the session configuration.
        /// But if something new shows up, I want to make sure we can accept it, tho' the
        /// herder will be hidden.
        /// </summary>
        /// <param name="strName">Name of the herder</param>
        /// <param name="fSolo">Can herder take only one control.</param>
        /// <returns>A new herder.</returns>
        /// <remarks>Noticed that this isn't being used at all. So I'll comment it out until
		/// I find a use for this code.</remarks>
   //     private SmartHerderBase DecorCreateDefaultHerder( string strName, bool fSolo ) {
   //         if( strName == null )
   //             return( null );

   //         SmartHerderBase oHerder  = null;
   //         string          strTitle = System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(strName);


   //         if( fSolo )
   //             oHerder = new SmartHerderSolo( this, null, strName, strTitle, Guid.Empty );
   //         else
   //             oHerder = new SmartHerderClxn( this, null, strName, strTitle, Guid.Empty );

   //         // Unfortunately the corner boxes won't be set until we've read the config template.
   //         // so just set some arbitray size for now.
   //         oHerder.SetRect(POINT.UPPERLEFT, 0, 0, 30, 30 );
   //         oHerder.Orientation = 0;
   //         oHerder.Sizing      = SMARTSIZE.Normal;
			//oHerder.Show        = VIEWSTATE.Hide;

   //         try {
   //             _rgShepards.Add( strName, oHerder );
   //         } catch( Exception oEx ) {
   //             Type[] rgErrors = { typeof( ArgumentNullException ),
   //                                 typeof( ArgumentException ) };
   //             if( !rgErrors.Contains( oEx.GetType() ) ) {
   //                 throw new InvalidProgramException( "Problem adding new ad hock shepard!" );
   //             }
   //         }

   //         LayoutShepards( oHerder.Orientation );

   //         return( oHerder );
   //     }

        private static readonly Type[] _rgDCErrors = { 
			typeof( NullReferenceException ), 
            typeof( ArgumentNullException ),
            typeof( ArgumentOutOfRangeException ),
			typeof( ArgumentException ),
			typeof( InvalidCastException ),
			typeof( InvalidOperationException )
		};

        /// <summary>
        /// Attempt create the associated decor for the given view and shepard.
        /// </summary>
        /// <seealso cref="DecorAddSolo(Guid, Control)"/>
        protected bool DecorCreate( ViewSlot oViewSite, SmartHerderBase oShepard ) {
			IPgCommandView oViewCmmd;
			DecorSlot      oDecorSite;
            Control        oDecor;

			try {
                if( oShepard.IsContained( oViewSite ) )
                    return true; // We're done. All is well.

				oViewCmmd  = (IPgCommandView)oViewSite.Guest;
				oDecorSite = new DecorSlot( this, oViewSite.DocumentSite, oShepard );
            } catch( Exception oEx ) {
                if( _rgDCErrors.IsUnhandled( oEx ) )
                    throw;
				LogError( null, "adornment", "method arguments must not be null!" );
				return false;
			}
            try {
				oDecor = oViewCmmd.Decorate( oDecorSite, oShepard.Guid ) as Control;

				// BUG: Sort of a bummer we did all this work for a control not supported. Probably
				//      a good reason for a IsSupported property on the IPgCommand interface.
				if( oDecor == null ) {
					return false;
				}
            } catch( Exception oEx ) {
                if( _rgDCErrors.IsUnhandled( oEx ) )
                    throw;

				LogError( oViewSite, "decor", "Could not create decor for view : " + oViewSite.Title, false );

				oDecorSite.Dispose();

				return false;
            }

			try {
				oDecorSite.Guest = oDecor;

                if( !oDecorSite.GuestInit() ) {
                    oDecorSite.Dispose();
                    LogError( oViewSite, "adornment", "The decoration, was unable to initialize a new instance." );
					return false;
                }
            } catch( Exception oEx ) {
                if( _rgDCErrors.IsUnhandled( oEx ) )
                    throw;

				LogError( oViewSite, "decor", "Could not load decor for view : " + oViewSite.Title, false );

				return false;
            }

            try {
                oShepard.AdornmentAdd( oViewSite, oDecor );
            } catch( Exception oEx ) {
                if( _rgDCErrors.IsUnhandled( oEx ) )
                    throw;

                oDecorSite.Dispose();
                LogError( oViewSite, "adornment", "Unable to add adornment requested." );
				return false;
            }

			return true;
        }

		/// <remarks>Only use this to load up the internal SOLO adornments: find, menu, alerts, matches.
		/// In the future, we'll try to use a controller on the main program, and the main
		/// window providing adornments to itself, or some such.</remarks>
		/// <exception cref="ArgumentNullException" />
		/// <exception cref="ArgumentException" />
        /// <seealso cref="DecorCreate(ViewSlot, SmartHerderBase)"/>
        protected bool DecorAddSolo( Guid gShepardName, Control oControl ) {
            SmartHerderBase oShepard = Shepardfind( gShepardName );

            if( Shepardfind( gShepardName ) is not SmartHerderSolo oSolo )
                return false;
            
            try {
                oSolo.AdornmentAdd( null, oControl );
            } catch( Exception oEx ) {
                if( _rgDCErrors.IsUnhandled( oEx ) )
                    throw;

                LogError(null, "internal", "Couldn't add adornment" );
                return false;
            }

            oControl.TabStop = false;

            return( true );
        }

		public Control DecorSoloSearch( Guid gDecor ) {
			if( Shepardfind( gDecor ) is SmartHerderSolo oSolo ) {
				return oSolo.AdornmentFind( null );
			}

			return null;
		}

        internal IEnumerator<SmartHerderBase> ShepardEnum() {
            foreach( KeyValuePair<Guid, SmartHerderBase> oPair in _rgShepards ) {
                yield return oPair.Value;
            }
        }

        internal SmartHerderBase Shepardfind( Guid gDecor ) {
            SmartHerderBase oHerder = null;

            try {
                oHerder = _rgShepards[gDecor];
            } catch( KeyNotFoundException ) {
            }

            return oHerder;
        }

        internal SHOWSTATE InsideShow {
            set { _rcFrame.Show = value; }
        }

        /// <summary>
        /// TODO: 2/10/2020, Need to rework the frame layout for the viewslots. There's a bug
        ///       where if the slot doesn't have a icon then the layout gp faults.
        /// </summary>
        /// <param name="oG"></param>
        protected void LayoutPaint( Graphics oG ) {
            switch( _eLayout ) { 
                case TOPLAYOUT.Solo:
                    _oLayoutPrimary.Paint( oG );
                    break;
                case TOPLAYOUT.Multi:
                    _oLayout2.Paint( oG );
                    break;
            }
        }

        protected void LayoutPaintSK( SKCanvas oCanvas ) {
            switch( _eLayout ) { 
                case TOPLAYOUT.Solo:
                    _oLayoutPrimary.Paint( oCanvas );
                    break;
                case TOPLAYOUT.Multi:
                    _oLayout2.Paint( oCanvas );
                    break;
            }
        }

        /// <summary>
        /// When our outside window changes size or any of our sides change extent,
        /// we need to adjust the inside rectangle.
        /// </summary>
        protected void LayoutFrame() {
			try {
				switch( _eLayout ) {
                    case TOPLAYOUT.Solo:
                        _oLayoutPrimary.SetRect( 0, 0, ClientRectangle.Width, ClientRectangle.Height );
                        _oLayoutPrimary.LayoutChildren();

                        // Might have no view loaded, need to check it.
                        if( _oSelectedWinSite != null ) {
                            _oSelectedWinSite.Guest.Bounds = _rcFrame.Rect;
						    _oSelectedWinSite.Guest.Show();
                        }
                        break;

					case TOPLAYOUT.Multi:
						using( IEnumerator<ViewSlot> oEnum = ViewEnumerator() ) {
							while( oEnum.MoveNext() ) {
								oEnum.Current.SetLayout( _eLayout );
							}
						}

						_oLayout2.Copy = _rcFrame;
						_oLayout2.LayoutChildren();

						using( IEnumerator<ViewSlot> oEnum = ViewEnumerator() ) {
							while( oEnum.MoveNext() ) {
								oEnum.Current.Guest.Show();
							}
						}
						break;
				}
			} catch( NullReferenceException ) {
                LogError( null, "Main Window", "Couldn't resize guest because viewsite is null" );
			}

            Invalidate();
        }

        class CompareVertical : IComparer<SmartRect>
        {
            public int Compare(SmartRect rcA, SmartRect rcB)
            {
                int iACenter, iBCenter;

                iACenter = rcA[(int)SmartRect.SIDE.TOP] + rcA.GetScalar(SCALAR.HEIGHT) / 2;
                iBCenter = rcB[(int)SmartRect.SIDE.TOP] + rcB.GetScalar(SCALAR.HEIGHT) / 2;

                return ( iACenter - iBCenter );
            }
        }

        class CompareHorizontal : IComparer<SmartRect>
        {
            public int Compare(SmartRect rcA, SmartRect rcB)
            {
                int iACenter, iBCenter;

                iACenter = rcA[(int)SmartRect.SIDE.LEFT] + rcA.GetScalar(SCALAR.WIDTH) / 2;
                iBCenter = rcB[(int)SmartRect.SIDE.LEFT] + rcB.GetScalar(SCALAR.WIDTH) / 2;

                return ( iACenter - iBCenter );
            }
        }

		/// <summary>
		/// This loads the shepards into the sidebox. Won't preserve order
		/// between loads. Won't ensure the shepard is loaded with an eligible
		/// decor.
		/// </summary>
        /// <seealso cref="DecorSetState"/>
        /// <seealso cref="DecorLoad(XmlElement)"/>
        protected void LayoutLoadShepardsAt( SideIdentify eSide ) {
			SideRect oSide = null;
			
			try {
				oSide = _rgSideInfo[eSide];
			} catch( ArgumentOutOfRangeException ) {
				LogError( null, "Decor", "Attempting to layout a non existing side! ^_^;" );
				return;
			}

			if( oSide == null )
				return;

			List<SmartHerderBase> rgSort = new List<SmartHerderBase>( oSide.Count );

            // Got to check the menu setting NOT the shepard.hidden. The
            // later isn't as reliable indicator of intention.
            foreach( IPgMenuVisibility oCurrentMenuItem in DecorSettings ) {
                if( oCurrentMenuItem.Orientation == eSide ) {
                    if( oCurrentMenuItem.Checked == true ) {
                        rgSort.Add( oCurrentMenuItem.Shepard );
                    } else {
                        // This shouldn't need messing with. But we work
                        // better if we clear it.
                        oCurrentMenuItem.Shepard.Hidden = true;
                    }
                }
            }

			// Sort 'em so they'll insert somewhere near where dragged.
            switch( oSide.Direction ){
				case TRACK.VERT:
					rgSort.Sort( new CompareVertical() );
					break;
				case TRACK.HORIZ:
					rgSort.Sort( new CompareHorizontal() );
					break;
			}

			oSide.Clear         ();
			oSide.Load          ( rgSort );
			oSide.PercentReset  ( fNormalize:true );
			oSide.LayoutChildren();  // BUG: If rail distance is zero, no layout happens!!
		}

        /// <summary>
        /// This is a test function to draw a diagonal where the decor
        /// boxes are. Use it in the OnPaint() function for diagnostics.
        /// </summary>
        /// <param name="oE"></param>
        protected void DecorLocationShow( PaintEventArgs oE )
        {
            foreach( SmartRect oRect in _rgSideInfo.Values ) {
                oE.Graphics.DrawLine( Pens.Aquamarine, 
                                      oRect.GetScalar(SCALAR.LEFT),
                                      oRect.GetScalar(SCALAR.TOP),
                                      oRect.GetScalar(SCALAR.RIGHT),
                                      oRect.GetScalar(SCALAR.BOTTOM) );
            }
        }

        /// <summary>
        /// Save the decor positions in the session file. This class depends on the shepards/herders
        /// in a side are sorted by their visual order by the decor manager. If we break that we'll
        /// have to sort here.
        /// </summary>
        /// <param name="xmlOurRoot"></param>
        /// <seealso cref="DecorLoad( XmlElement )"/>
        /// <seealso cref="LayoutLoadShepardsAt"/>
        public void DecorSave(  XmlDocumentFragment xmlOurRoot ) {
            try {
			    XmlElement xmlDecors = xmlOurRoot.OwnerDocument.CreateElement( "Decors" );
                foreach( KeyValuePair<SideIdentify,SideRect> oPair in _rgSideInfo ) {
                    int i = 0;
                    foreach( SmartHerderBase oHerder in oPair.Value ) {
                        if( !oHerder.Hidden ) {
                            XmlElement xmlDecor =xmlOurRoot.OwnerDocument.CreateElement( "Decor" );

				            xmlDecor.SetAttribute( "decor", oHerder.Decor.ToString() );
                            xmlDecor.SetAttribute( "side",  oHerder.Orientation.ToString().ToLower() );
                            xmlDecor.SetAttribute( "order", i.ToString() );
                            xmlDecor.SetAttribute( "track", oHerder.Track.ToString() );

                            xmlDecors.AppendChild( xmlDecor );
                            ++i;
                        }
                    }
                    if( i > 0 ) {
                        XmlElement xmlSide =xmlOurRoot.OwnerDocument.CreateElement( "Side" );

                        xmlSide.SetAttribute( "name", oPair.Key.ToString() );
                        xmlSide.SetAttribute( "rail", oPair.Value.Track.ToString() );
                    }
                }
                xmlOurRoot.AppendChild( xmlDecors );
            } catch ( Exception oEx ) {
                Type[] rgErrors = { typeof( XmlException ),
                                    typeof( ArgumentException ),
                                    typeof( ArgumentNullException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
                LogError( null, "Main Window Save", "Couldn't Save decor configuration" );
            }
        }

        protected class MenuReset {
            public readonly IPgMenuVisibility _oDecorMenu;
            public readonly SideIdentify      _eOldSide;

            public bool              _fVisible;
            public SideIdentify      _eNewSide;
            public int               _iOrder;
            public uint             _uiTrack;

            public MenuReset( IPgMenuVisibility oDecorVis ) {
                _oDecorMenu = oDecorVis;
                _eOldSide   = oDecorVis.Shepard.Orientation;
                _eNewSide   = _eOldSide;
                _fVisible   = false;
                _iOrder     = 100;
                _uiTrack    = 10;
            }
        }


        /// <summary>
        /// Normally we sort items on a side based on the rectangle centers. However when
        /// reloading form a session (pvs) file, we use the specified order.
        /// </summary>
        class CompareOrder : IComparer<MenuReset> {
            public int Compare(MenuReset rcA, MenuReset rcB ) {
                return rcA._iOrder - rcB._iOrder;
            }
        }

        struct EnumerateHerders : IEnumerable<SmartHerderBase> {
            List<MenuReset> _rgResetList;

            public EnumerateHerders( List<MenuReset> rgResetList ) {
                _rgResetList = rgResetList ?? throw new ArgumentNullException();
            }

            public IEnumerator<SmartHerderBase> GetEnumerator() {
                foreach( MenuReset oReset in _rgResetList ) {
                    yield return oReset._oDecorMenu.Shepard;
                }
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }
        }

        /// <summary>
        /// this is a little backward compatibility. We'll get rid of after
        /// we've updated our PVS files eventually.
        /// </summary>
        /// <param name="strName">Old way to look up tool/decor by string name.</param>
        [Obsolete]protected Guid FindDecorByLegacyName( string strName ) {
            foreach( KeyValuePair<Guid, DecorProperties> oPair in _rgStdDecor ) {
                if( string.Compare( oPair.Value.Name, strName, ignoreCase:true ) == 0 ) {
                    return oPair.Key;
                }
            }
            throw new KeyNotFoundException( "Could not find legacy decor name." );
        }

        /// <summary>
        /// We depend on ViewSelect() called AFTER this has been called on load.
        /// </summary>
        /// <seealso cref="ViewSelect(ViewSlot, bool)" />
        /// <seealso cref="LayoutLoadShepardsAt"/>
        /// <seealso cref="DecorMenuReload"/>
        /// <seealso cref="DecorSave"/>
        public void DecorLoad( XmlElement xmlRoot ) {
            try {
				XmlNodeList                       rgXmlDecors = xmlRoot.SelectNodes( "Decors/Decor");
                Dictionary<string, SideIdentify>  dctFindSide = new(); // search side enum by a string.
                Dictionary<SideIdentify, bool>    dctSides    = new();
                Dictionary<Guid, MenuReset>       dctDecor    = new(); // search decor by string.

                // Identify any "side" that will be effected by a shepard visibility.
                foreach( SideIdentify eSide in Enum.GetValues( typeof( SideIdentify ) ) ) {
                    dctSides   .Add( eSide, false );
                    dctFindSide.Add( eSide.ToString().ToLower(), eSide );
                }
                // Set up copy of the menu assuming no decor specified in the .pvs file
                foreach( IPgMenuVisibility oDecorVis in _rgDecorEnum ) {
                    dctDecor.Add( oDecorVis.Shepard.Decor, new MenuReset( oDecorVis ) );
                }
                // If we find a decor specified as shown, flag it that we want it on.
				foreach( XmlElement xmlDecor in rgXmlDecors ) {
                    try {
                        Guid   gDecor = Guid.Empty;

                        string strDecorName = xmlDecor.GetAttribute( "name" );
                        string strDecorGuid = xmlDecor.GetAttribute( "decor" );
                        string strDecorSide = xmlDecor.GetAttribute( "side" );
                        string strDecorOrdr = xmlDecor.GetAttribute( "order" );
                        string strDecorTrak = xmlDecor.GetAttribute( "track" );

                        if( string.IsNullOrEmpty( strDecorName ) ) {
						    gDecor = Guid.Parse( strDecorGuid );      // New Path.
                        } else {
                            gDecor = FindDecorByLegacyName( strDecorName ); // Old Path.
                        }

                        MenuReset oReset = dctDecor[gDecor];

                        oReset._fVisible = true;
                        oReset._eNewSide = dctFindSide[ strDecorSide ];
                        oReset._iOrder   = int .Parse( strDecorOrdr );
                        oReset._uiTrack  = uint.Parse( strDecorTrak );
                    } catch( Exception oEx ) {
                        Type[] rgErrors = { typeof( KeyNotFoundException ),
                                            typeof( FormatException ),
                                            typeof( OverflowException ),
                                            typeof( ArgumentNullException ) };
                        if( rgErrors.IsUnhandled( oEx ) ) 
                            throw;
                    }
                    // if we fail to find side we'll still dock the decor
                    // but it won't be on the correct side or right order.
                }
                // Now go thru all the menu items and see if the .pvs visibility matches it's current visibility.
                bool fFoundAtLeastOne = false;
                foreach( KeyValuePair< Guid,MenuReset> oPair in dctDecor ) {
                    IPgMenuVisibility oMenuVis = oPair.Value._oDecorMenu;
                    SideIdentify      eOrient  = oPair.Value._eNewSide;   // oMenuVis.Shepard.Orientation;

                    if( oPair.Value._fVisible != oMenuVis.Checked ||
                        oPair.Value._eNewSide != oMenuVis.Shepard.Orientation ) 
                    {
                        oMenuVis.Checked             =  oPair.Value._fVisible;
					    oMenuVis.Shepard.Hidden      = !oPair.Value._fVisible;
                        oMenuVis.Shepard.Orientation =  eOrient;
                        dctSides[ eOrient ]          =  oPair.Value._fVisible;
                        fFoundAtLeastOne             =  true;
                    }
                    // Always load the track value from saved value.
                    oMenuVis.Shepard.Track = oPair.Value._uiTrack; // check CSS style.
                }
                // If anything is found reset the UI.
                if( fFoundAtLeastOne ) {
			        List<MenuReset> rgSort   = new ( 10 );
                    CompareOrder    oCompare = new ();
                    foreach( KeyValuePair< SideIdentify, bool > oPair in dctSides ) {
                        if( oPair.Value ) { // If the side was touched...
			                rgSort.Clear();
                            // load everthing we want on that side into our sorter.
                            foreach( MenuReset oHerder in dctDecor.Values ) {
                                if( oHerder._eNewSide == oPair.Key &&
                                    oHerder._oDecorMenu.Checked ) {
                                    rgSort.Add( oHerder );
                                }
                            }
                            // Sort 'em by the order specified in the pvs file.
                            rgSort.Sort( oCompare );

                            // TODO: We might be able to obviate the need or our external sort array.
                            SideRect oSide  = _rgSideInfo[oPair.Key];

			                oSide.Clear();
                            // Check the track %'s seem reasonable. If not, reset them.
                            int iTrack = oSide.Load( new EnumerateHerders( rgSort ) );
                            if( iTrack < 90 || iTrack > 110 )
                                oSide.PercentReset( fNormalize:true );

			                oSide.LayoutChildren();  // BUG: If rail distance is zero, no layout happens!!
                        }
                        _rgSideInfo[oPair.Key].Hidden = !oPair.Value;
                        DecorShuffleSide( oPair.Key );
                    }
                    LayoutFrame();
                }
            } catch ( Exception oEx ) {
                Type[] rgErrors = { typeof( XmlException ),
                                    typeof( ArgumentException ),
                                    typeof( ArgumentNullException ),
                                    typeof( InvalidOperationException ),
                                    typeof( KeyNotFoundException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
                LogError( null, "Main Window Load", "Couldn't Load decor configuration" );
            }
        }

        /// <summary>
        ///  Set us up for or normal display mode. s/b adornments if visible.
        ///  I need to save that state of the display, but for now just size it
        ///  to something nice.
        /// </summary>
        public void DecorShow() {
            _rcFrame.Hidden = false;
            // No need to save the sides extents b/c the DecorSideShuffle does that when needed.

 			if( _miDecorMenu.DropDownItems[0] is ToolStripMenuItem oItem ) {
				oItem.Checked = true;
			}

			foreach( IPgMenuVisibility oMenu in DecorSettings ) {
				oMenu.Shepard.Hidden = !oMenu.Checked;
            }
            foreach( KeyValuePair<SideIdentify, SideRect> oPair in _rgSideInfo ) {
                SideRect oSide = oPair.Value;

                if( oSide.Track > _iMargin )
                    oSide.Hidden = false;
            }

            DecorShuffle();
        }

        /// <summary>
        /// Hide all the dacor on the sides.
        /// </summary>
        public void DecorHide() {
            foreach( SmartHerderBase oShepard in this ) {
                oShepard.Hidden = true;
            }
            foreach( KeyValuePair<SideIdentify, SideRect> oKey in _rgSideInfo ) {
                oKey.Value.Hidden = true;
            }

			if( _miDecorMenu.DropDownItems[0] is ToolStripMenuItem oItem ) {
				oItem.Checked = false;
			}

            // Ach! We can't do this anymore since the frame marshals the layout.
            //_rcFrame.Hidden = true; 

            // No need to shuffle, since we we've disabled all the decor anyway!!
            LayoutFrame();
        }

        /// <summary>
        /// Check the state of the Decor visible menu item and toggle.
        /// </summary>
		public void DecorToggle() {
			if( _miDecorMenu.DropDownItems[0] is ToolStripMenuItem oItem ) {
				oItem.Checked = !oItem.Checked;

		        if( oItem.Checked )
			        DecorShow();
		        else
			        DecorHide();
			}
		}

        /// <summary>
        /// Find all the shepard's matching the given orientation and see how many of them 
        /// have the menu item checked (and thus want to be opened). We can't use the
        /// shepard.hide method to help determine availability since that may be open
        /// or closed based on the decor availability.
        /// </summary>
        /// <remarks>This method has a side effect of shuffling the decor. Turns off the
		/// decor not from the current view.</remarks>
        /// <seealso cref="DecorShuffle"/>
        protected bool IsAnyShepardReady( SideIdentify eOrientation ) {
            bool fAnyReady = false;
            // This was a nasty bug. Wasn't doing at all what it should have
            // been doing. 
			//foreach( SmartHerderBase oSide in _rgSideInfo[eOrientation] ) {
            //    if( oSide.AdornmentShuffle( _oSelectedWinSite ) ) {
            //        fAnyReady = true;
            //    }
			//}
            // This is STILL nasty bug. We can't rely on the Herder for the show hide
            // state that we need the on that side. 
            //foreach( KeyValuePair<string, SmartHerderBase> oPair in _rgShepards ) {
            //    SmartHerderBase oHerder = oPair.Value;
            //    if( oHerder.Orientation == eOrientation ) {
            //        fAnyReady |= oHerder.AdornmentShuffle( _oSelectedWinSite );
            //    }
            //}
            // The MENU get's the final say!!
            foreach( IPgMenuVisibility oCurrentMenuItem in DecorSettings ) {
                if( oCurrentMenuItem.Orientation == eOrientation &&
                    oCurrentMenuItem.Checked && 
                    oCurrentMenuItem.Shepard.AdornmentShuffle( _oSelectedWinSite )  ) 
                {
                    fAnyReady = true;
                }
            }

            return( fAnyReady );
        }

		/// <summary>
		/// When a decor is shown or hidden, call this method to open or close
        /// the side containing those decor.
        /// 1) If the side is closed, check if any side adornments are showing
        ///    and if so open up the side.
        /// 2) If the side is opened, check if any side adornments are showing
        ///    and if NOT close up teh side.
		/// </summary>
        /// <seealso cref="CenterDrag"/>
        private void DecorShuffleSide( SideIdentify eOrientation ) {
            // Top isn't in the sideinfo anymore. Guard against that.
            try {
                SideRect oSide = _rgSideInfo[eOrientation];

                if( !oSide.Hidden ) { // currently open
                    if( !IsAnyShepardReady( eOrientation ) ) {
                        oSide.Hidden = true;
                    }
                } else {              // currently closed
                    if( IsAnyShepardReady( eOrientation ) ) {
                        if( oSide.Track < _iMargin ) {
                            oSide.Track = (uint)oSide.SideInit;
                        }
                        oSide.Hidden = false;
                    }
                }
                OnSizeChanged( new EventArgs() );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( KeyNotFoundException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
            }
        }

        /// <summary>
        /// This get's called when the selected view changes between our center hosted views, a view
        /// gets deleted. Or when the inside is dragged around.
        /// </summary>
        /// <seealso cref="DecorSetState"/>
        /// <seealso cref="IsAnyShepardReady(SideIdentify)"/>
        protected void DecorShuffle() {
            // view site can be null if no documents are open in the editor.
            if( _oSelectedWinSite == null )
                return;

            foreach( IPgMenuVisibility oMenuItem in DecorSettings ) {
                if( oMenuItem.Checked ) {
                    // Lazy create the requested decor associated with current view.
					// If site doesn't support the decor, remember, other's might!!
                    DecorCreate( _oSelectedWinSite, oMenuItem.Shepard );
                } else {
					oMenuItem.Shepard.Hidden = true;
                }
            }

            foreach( SideIdentify eSide in Enum.GetValues( typeof( SideIdentify ) ) ) {
				DecorShuffleSide( eSide);
			}

			LayoutFrame();
        }

        const bool OPEN  = true;
        const bool CLOSE = false;

        /// <summary>
        /// Open or close a single tool window. Which also might mean opening or 
        /// closing the corresponding side and opening up other tools.
        /// </summary>
        /// <seealso cref="DecorShuffle"/>
        protected void DecorSetState( IPgMenuVisibility oMenuItem, bool fNewState ) 
        {
            if( oMenuItem == null )
                return;
            // Technically this can't happen anymore, but leave it for now.
			if( _rcFrame.Hidden )
				DecorShow();

            oMenuItem.Checked = fNewState; // this won't call back, because I sink OnClick() and not OnCheckedChanged().

            SideIdentify eOrientation = oMenuItem.Shepard.Orientation;

            // First set up the new decor or close the old decor. 
            if( fNewState ) {
                if( _oSelectedWinSite != null ) {
                    // Check with the menu for final say on state.
                    foreach( IPgMenuVisibility oCurrentMenuItem in DecorSettings ) {
                        if( oCurrentMenuItem.Orientation == eOrientation ) {
                            if( oCurrentMenuItem.Checked ) {
                                // Lazy create the requested decor associated with current view
                                // Definitely active if created. But hidden only if menu sez so.
                                if( DecorCreate( _oSelectedWinSite, oCurrentMenuItem.Shepard ) ) {
                                    oMenuItem.Shepard.Show = SHOWSTATE.Active;
                                }
                            } else {
                                // This shouldn't need messing with. But we work
                                // better if we clear it.
                                oCurrentMenuItem.Shepard.Hidden = true;
                            } 
                        }
                    }
                }
            } else {
                oMenuItem.Shepard.AdornmentCloseAll();
                oMenuItem.Shepard.Hidden = true;
            }

			LayoutLoadShepardsAt( eOrientation ); // A shepard is coming or going. Was above the switch...
            DecorShuffleSide    ( eOrientation );

            LayoutFrame();
			Invalidate ();
        }

        internal SmartHerderBase DecorOpen( Guid gDecor, bool fOpen ) {
            foreach( IPgMenuVisibility oMenuItem in DecorSettings ) {
                if( oMenuItem.Shepard.Decor == gDecor ) {
                    DecorSetState( oMenuItem, fOpen );
                    return( oMenuItem.Shepard );
                }
            }

            return( null );
        }

        /// <summary>
        /// Let's try opening the requested decor on demand when a view that needs it is opened...
        /// This method is on the IPgMainWindow interface. 
        /// </summary>
        public bool DecorOpen( Guid gDecor ) {
            return DecorOpen( gDecor, true ) != null;
        }

        /// <summary>
        /// Presently there is no Menu decor/shepard. The menu is hard coded to be
        /// in the layout.
        /// </summary>
        protected void OnDecorMenuOpenCommand( object s, EventArgs e ) {
            DecorOpen( GlobalDecor.Menu, true );
        }

        /// <summary>
        /// The context menu in the decor shepard calls this to close the decoration.
        /// </summary>
        protected void OnDecorCloseCommand( object s, EventArgs e ) {
            foreach( object oItem in _miDecorMenu.DropDownItems ) {
				if( oItem is MenuItemHerder oMenuItem ) {
					if( oMenuItem.Shepard.IsInside(_pntContextLocation.X, _pntContextLocation.Y) ) {
						DecorSetState(oMenuItem, false);
						break;
					}
				}
			}
        }

        /// <summary>
        /// Toggle current check state.
        /// </summary>
		protected void OnDecorMenuClick( object oTarget, EventArgs oArgs ) {
			if( oTarget is MenuItemHerder oMenuItem ) {
				DecorSetState( oMenuItem, !oMenuItem.Checked );
			}
		}
        
    } // end class
}