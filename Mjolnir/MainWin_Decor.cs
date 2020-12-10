using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Xml;

using SkiaSharp;

using Play.Interfaces.Embedding;
using Play.Rectangles;
using Play.Edit;
using System.Collections;

namespace Mjolnir {
	public class SideRect : 
		LayoutStack,
		IEnumerable<SmartHerderBase>
	{
		readonly List<SmartBinder> _rgSpacers = new List<SmartBinder>();
				 uint              _uiSpacer;

		public SideRect( TRACK eDir, uint uiSpacer ) : base( eDir, 0 ) {
			_uiSpacer = uiSpacer;
		}

		public SideRect( TRACK eDir, uint uiSpacer, uint uiTrack, float flMaxPercent ) : 
			base( eDir, 0, uiTrack, flMaxPercent ) 
		{
			_uiSpacer = uiSpacer;
		}

		IEnumerable<SmartBinder> Spacers => _rgSpacers;

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

		public void Load( IEnumerable<SmartHerderBase> rgSource ) {
			foreach( SmartHerderBase oChild in rgSource ) {
				Add( oChild );
			}
		}

		/// <exception cref="ArgumentNullException" />
		public override void Add( LayoutRect oNext ) {
			if( oNext == null )
				throw new ArgumentNullException();

			if( Count > 0 ) {
				SmartBinder oSpacer = new SmartSpacer( Direction, Last, oNext, (int)_uiSpacer );

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
						oChild.Track = (uint)( 100 / iAdjustables );
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
        public class MainWinDecorEnum :
            IEnumerable<IPgMenuVisibility> 
        {
            readonly MainWin _oHost;

            public MainWinDecorEnum( MainWin oHost ) {
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
            get { return _oDecorEnum; }
        } 
        /// <summary>
        /// Do this after all the tools, solo, and document adornments are added.
        /// TODO: The only way to open tools is via the menu. That means I need to get to the
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

        /// <summary>
        /// Read out the positions of the tools from the config file. 
        /// </summary>
        /// <param name="xmlDocument">Config xml file.</param>
        protected void InitializeShepards( XmlDocument xmlDocument ) {
            Dictionary<string, string> rgToolResx = new Dictionary<string,string>();
            XmlNodeList                lstTools   = xmlDocument.SelectNodes("config/mainwindow/docking/dock");
            Point                      ptOrigin   = new Point();

            foreach( XmlElement xeType in lstTools ) {
                string strToolName = xeType.GetAttribute("tool"); // Returns empty string if not found.
                string strToolEdge = xeType.GetAttribute("edge");
                string strToolDisp = xeType.GetAttribute("display" );
				string strToolVis  = xeType.GetAttribute("visible" );
                string strToolSolo = xeType.GetAttribute("solo");
                string strToolIcon = xeType.GetAttribute("icon");
                string strToolGuid = xeType.GetAttribute("decor" );

                int    iEdge    = 0;
                Bitmap oBitmap  = null;
                Guid   guidTool = Guid.Empty;

                rgToolResx.Add(strToolName, strToolIcon );

                try {
                    oBitmap = new Bitmap( GetType(), "Content." + rgToolResx[ strToolName] ); // the icon is a resource now.
                } catch( Exception oE ) {
                    Type[] rgErrors = { typeof( KeyNotFoundException ), // This error if the user errored on the attribute name or value.
                                        typeof( ArgumentException ) };  // This error if we didn't embed resource.
                    if( rgErrors.IsUnhandled( oE ) )
                        throw;

                    oBitmap = new Bitmap( 1, 1 ); 
                }
                try {
                    iEdge = (int)_rgSideNames[strToolEdge];
                } catch( KeyNotFoundException ) {
                }

                try {
                    guidTool = new Guid( strToolGuid );
                } catch ( Exception oEx ) {
                    Type[] rgError = { typeof(ArgumentNullException), 
                                       typeof(FormatException), 
                                       typeof(OverflowException) };
                    if( rgError.IsUnhandled( oEx ) )
                        throw new ApplicationException( "unexpected error reading tool guid", oEx );
                }
                
                // A herder is the outer frame holding all the dialogs of a particular
                // function, like outlines, for all the documents in the system. This
                // is the piece that gets dragged around if the user want's to see it in
                // a different place on the edges.
                SmartHerderBase oShepard;
                if( string.Compare( strToolSolo, "true", true ) == 0 )
                    oShepard = new SmartHerderSolo( this, oBitmap, strToolName, strToolDisp, guidTool );
                else
                    oShepard = new SmartHerderClxn( this, oBitmap, strToolName, strToolDisp, guidTool );

                // Unfortunately the corner boxes won't be set until we've got our window size.
                // so just set some arbitray size for now.
                oShepard.SetRect(LOCUS.UPPERLEFT, ptOrigin.X, ptOrigin.Y, 30, 30 );
                oShepard.Orientation = iEdge;
                oShepard.Sizing      = SMARTSIZE.Normal;
				if( strToolVis.ToLower() == "true" )
					oShepard.Show = SHOWSTATE.Active;
				else
					oShepard.Hidden = true;

                //if( strToolName == "outline" ) // TODO: Experimental.
                //    oShepard.HideTitle = true;
                if( strToolName == "command" ) // TODO: More experimental.
                    oShepard.Margin = new SmartRect( 5, 5, 5, 5 );

                _rgShepards.Add( strToolName, oShepard );

                // Stagger all the windows both in x and y so that when we try to arrange them after
                // our main window get's sized they'll be roughly in the order loaded.
                ptOrigin.Offset( 5, 5 );
            }

			LayoutLoadShepards(); // loads the shepards into the sides BUT NOT THE decor in the shepard!!
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
			typeof( ArgumentException ),
			typeof( InvalidCastException ),
			typeof( InvalidOperationException )
		};

        /// <summary>
        /// Attempt create the associated decor for the given view and shepard.
        /// </summary>
        protected bool DecorCreate( ViewSlot oViewSite, SmartHerderBase oShepard ) {
			IPgCommandView oViewCmmd;
			ViewSlot       oDecorSite;
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

                if( !oDecorSite.InitNew() ) {
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
        protected bool DecorAdd( string strShepardName, Control oControl ) {
            SmartHerderBase oShepard = Shepardfind( strShepardName );

            if( oShepard == null )
                return( false );
            
            if( !oShepard.AdornmentAdd(null, oControl ) ) {
                LogError(null, "internal", "Couldn't add adornment: " + strShepardName );
                return( false );
            }

            oControl.TabStop = false;

            return( true );
        }

		public Control DecorSoloSearch( string strShepardName ) {
			SmartHerderBase oShepardMatches = Shepardfind( "matches" );
			if( oShepardMatches != null ) {
				return oShepardMatches.AdornmentFind( null );
			}

			return null;
		}

        internal IEnumerator<SmartHerderBase> ShepardEnum() {
            foreach( KeyValuePair<string, SmartHerderBase> oPair in _rgShepards ) {
                yield return( oPair.Value );
            }
        }

        internal SmartHerderBase Shepardfind( string strName ) {
            SmartHerderBase oHerder = null;

            try {
                oHerder = _rgShepards[strName];
            } catch( KeyNotFoundException ) {
            }

            return( oHerder );
        }

        internal SHOWSTATE InsideShow {
            set { _rcFrame.Show = value; }
        }

        /// <summary>
        /// After the frame rect is sized, calculate the sizes of the docking areas on the perimeter.
		/// We size relative to the height of the top.
        /// </summary>
        protected void LayoutSideBoxes() {
			if( _rcFrame.Hidden )
				return;

            _rgSideBox[(int)EDGE.LEFT  ].SetRect( LOCUS.UPPERLEFT,
                                                  0,
                                                  _rcFrame.Outer.Top,
                                                  _rcFrame.Outer.Left,
                                                  ClientRectangle.Height - _rcFrame.Outer.Top );
            _rgSideBox[(int)EDGE.TOP   ].SetRect( LOCUS.UPPERLEFT, 
                                                  0,
                                                  0,
                                                  ClientRectangle.Width,
                                                  _rcFrame.Outer.Top );
            _rgSideBox[(int)EDGE.RIGHT ].SetRect( LOCUS.UPPERLEFT,
                                                  _rcFrame.Outer.Right,
                                                  _rcFrame.Outer.Top,
                                                  ClientRectangle.Right  - _rcFrame.Outer.Right,
                                                  ClientRectangle.Height - _rcFrame.Outer.Top );
            _rgSideBox[(int)EDGE.BOTTOM].SetRect( LOCUS.UPPERLEFT, 
                                                  _rcFrame.Outer.Left, 
                                                  _rcFrame.Outer.Bottom,
                                                  _rcFrame.Outer.Width,
                                                  ClientRectangle.Bottom - _rcFrame.Outer.Bottom );

			
			// TODO: It doesn't look like the side boxes re-layout children if their
			//       size changes. That might be a nice feature.
			foreach( SideRect oSide in _rgSideBox ) {
				oSide.LayoutChildren();
			}
        }

        /// <remarks>
        /// Remember, we might not have any view loaded!! Got to check if the SelectedWinSite is null!
        /// </remarks>
		protected void LayoutViews() {
			try {
				switch( _eLayout ) {
					case TOPLAYOUT.Solo:
                        if( _oSelectedWinSite != null )
                            _oSelectedWinSite.SetLayout( _eLayout );

						_oLayout1.Copy = _rcFrame; // When called, the view gets an OnSizeChanged() call!
						_oLayout1.LayoutChildren();

                        if( _oSelectedWinSite != null )
						    _oSelectedWinSite.Guest.Show();
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
		}

        /// <summary>
        /// TODO: 2/10/2020, Need to rework the frame layout for the viewslots. There's a bug
        ///       where if the slot doesn't have a icon then the layout gp faults.
        /// </summary>
        /// <param name="oG"></param>
        protected void LayoutPaint( Graphics oG ) {
			using( GraphicsContext oDC = new GraphicsContext( oG ) ) {
				using( new ItemContext( oDC.Handle, ToolsFont.ToHfont() ) ) {
                    // I initialize the script cache in the OnHandleCreate(). This means I don't 
                    // need to be checking if the script cache has been initialized. HOWEVER, if I want
                    // to change the font. I'm gonna have to free the _hScriptCache and reload the _sDefFontProps.
			        //if( _hScriptCache == IntPtr.Zero )
				       // _sDefFontProps.Load( oDC.Handle, ref _hScriptCache );

                    //foreach( ViewsLine oLine in _oDoc_ViewSelector ) {
                    //    if( oLine.ViewSite.IsTextInvalid ) {
                    //        oLine.ViewSite.UpdateText( oDC.Handle, ref _hScriptCache, ToolsFont.Height, _sDefFontProps );
                    //    }
                    //    //if( _eLayout == TOPLAYOUT.Multi )
                    //    //    oLine.ViewSite.Layout.Render( oDC.Handle, _hScriptCache );
                    //}
                    //if( _eLayout == TOPLAYOUT.Solo && _oSelectedWinSite != null )
                    //    _oSelectedWinSite.Layout.Render( oDC.Handle, _hScriptCache );
                }
            }
            switch( _eLayout ) { 
                case TOPLAYOUT.Solo:
                    _oLayout1.Paint( oG );
                    break;
                case TOPLAYOUT.Multi:
                    _oLayout2.Paint( oG );
                    break;
            }
        }

        /// <summary>
        /// When our outside window changes size or any of our sides change extent,
        /// we need to adjust the inside rectangle.
        /// </summary>
		/// <seealso cref="OnInsideAdjusted"/>
        protected void LayoutFrame()
        {
            Point ulPoint = new Point( _rcSide.Left, LayoutSizeTop() + ( _rcFrame.Hidden ? 0 : 5 ) );
            Point lrPoint = new Point( ClientRectangle.Right  - _rcSide[SCALAR.RIGHT],
                                       ClientRectangle.Bottom - _rcSide[SCALAR.BOTTOM]);
			SmartRect rcTemp = new SmartRect();

            rcTemp.SetPoint(SET.STRETCH, LOCUS.UPPERLEFT,  ulPoint.X, ulPoint.Y);
            rcTemp.SetPoint(SET.STRETCH, LOCUS.LOWERRIGHT, lrPoint.X, lrPoint.Y);
			
            LayoutFrameValidate( rcTemp );

			_rcFrame.Copy = rcTemp;
            _rcFrame.UpdateHandles();

			LayoutViews();

            Invalidate();
        }

        /// <summary>
        /// When the size/position of the inside window is changed, we need
        /// to retrieve the new edge distances. We only change the edge when
        /// the inside is being dragged. If the user sizes the host/app window
        /// then we are causing the re-size and don't want to update the edges.
        /// </summary>
		/// <seealso cref="LayoutFrame"/>
        protected void OnInsideAdjusted( SmartRect oInside ) {
            if( _oDrag != null ) {
                Rectangle rctClient = this.ClientRectangle; // The host window size.

				// BUG: I can probably better set this with one call.
                _rcSide.SetScalar(SET.STRETCH, SCALAR.LEFT,   _rcFrame.Left);
                _rcSide.SetScalar(SET.STRETCH, SCALAR.TOP,    _rcFrame.Top);
                _rcSide.SetScalar(SET.STRETCH, SCALAR.RIGHT,  rctClient.Right  - _rcFrame.Right);
                _rcSide.SetScalar(SET.STRETCH, SCALAR.BOTTOM, rctClient.Bottom - _rcFrame.Bottom);
            }

			LayoutViews    ();
            LayoutSideBoxes();
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
        protected void LayoutLoadShepardsAt( int iSide ) {
			SideRect oSide = null;
			
			try {
				oSide = _rgSideBox[iSide];
			} catch( ArgumentOutOfRangeException ) {
				LogError( null, "Decor", "Attempting to layout a non existing side! ^_^;" );
				return;
			}

			if( oSide == null )
				return;

			List<SmartHerderBase> rgSort = new List<SmartHerderBase>( oSide.Count );

			// Load visible shepard even if empty.
			foreach( SmartHerderBase oShepard in this ) {
				if( oShepard.Orientation == iSide &&
					oShepard.Hidden      == false )
				{
					rgSort.Add( oShepard );
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

			oSide.Clear();
			oSide.Load( rgSort );
			oSide.PercentReset( fNormalize:true );
			oSide.LayoutChildren();
		}

		/// <remarks>This one is a little hacky since we're effectively looking for the tool bar
		/// and then asking it for its prefered size. We'll replace using vertical stack, then create
		/// a layoutrect subclass to get the preference for track.</remarks>
		protected int LayoutSizeTop() {
			int iHeight = 0;

            foreach( SmartHerderBase oShepard in this ) {
                if( oShepard.Orientation == 1 &&
					oShepard.Hidden      == false ) 
				{
					if( oShepard is SmartHerderSolo oSolo ) {
						try {
							Size oPreference = oSolo.Adornment.GetPreferredSize( new Size( Width, Height ) );
							oShepard.SetRect( 0, 0, Width, oPreference.Height );
							iHeight += oPreference.Height;
						} catch( NullReferenceException ) {
							LogError( null, "Shepards", "Null Adornment", false );
							iHeight += 14;
						}
					}
                }
			}

			return( iHeight );
		}

        protected void LayoutLoadShepards() {
            for( int iSide = 0; iSide<_rgSideBox.Count; ++iSide ) {
                LayoutLoadShepardsAt(iSide);
            }
        }

        /// <summary>
        /// A little bit of non-linear behavior. If the user drags the box beyond
        /// the main window frame. We resize to just inside the frame. This is a
        /// simple gesture to open and close the corner windows! Also get's called
        /// when we switch from viewing adorments to off and back. It's
        /// not called when the main window is resized.
        /// </summary>
        protected uint LayoutFrameValidate( SmartRect rcTest )
        {
            Rectangle rcTemp   = this.ClientRectangle;
            SmartRect rcClient = new SmartRect( rcTemp.Left, rcTemp.Top, rcTemp.Right, rcTemp.Bottom );
            int[]     rgiMultiplier = { 1, 1, -1, -1 };
            uint      uiEdge   = 1;
            uint      uiReturn = 0;

            if( !_rcFrame.Hidden )
                rcClient.Inflate(-1, _rcMargin ); // -1 means deflate.

            for( int i = 0; i < 4; ++i ) {
                int iDifference = rcTest.GetSide(i) - rcClient.GetSide(i);

                // Check that the "inside" is in bounds.
                if( iDifference * rgiMultiplier[i] < 0 ) {
                    rcTest .SetScalar(SET.STRETCH, (SCALAR)uiEdge,  rcClient.GetSide(i));
                    _rcSide.SetScalar(SET.STRETCH, (SCALAR)uiEdge, _rcFrame.Hidden ? 0 : _rcMargin.GetSide(i));

                    uiReturn |= uiEdge; // Return which edge(s) are now closed.
                }
                uiEdge = uiEdge << 1;
            }

			return uiReturn;
        }


        /// <summary>
        /// This is a test function to draw a diagonal where the decor
        /// boxes are. Use it in the OnPaint() function for diagnostics.
        /// </summary>
        /// <param name="oE"></param>
        protected void DecorLocationShow( PaintEventArgs oE )
        {
            foreach( SmartRect oRect in _rgSideBox ) {
                oE.Graphics.DrawLine( Pens.Aquamarine, 
                                      oRect.GetScalar(SCALAR.LEFT),
                                      oRect.GetScalar(SCALAR.TOP),
                                      oRect.GetScalar(SCALAR.RIGHT),
                                      oRect.GetScalar(SCALAR.BOTTOM) );
            }
        }

        public void DecorSave(  XmlDocumentFragment xmlOurRoot ) {
            try {
			    XmlElement xmlDecors = xmlOurRoot.OwnerDocument.CreateElement( "Decors" );
                foreach( IPgMenuVisibility oDecorMenu in _oDecorEnum ) {
                    if( oDecorMenu.Checked ) {
                        XmlElement xmlDecor =xmlOurRoot.OwnerDocument.CreateElement( "Decor" );

				        xmlDecor.SetAttribute( "name", oDecorMenu.Shepard.Name );

                        xmlDecors.AppendChild( xmlDecor );
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

        /// <summary>
        /// We depend on ViewSelect() called AFTER this has been called on load.
        /// </summary>
        /// <seealso cref="ViewSelect(ViewSlot, bool)" />
        public void DecorLoad( XmlElement xmlRoot ) {
            try {
				XmlNodeList rgXmlViews = xmlRoot.SelectNodes( "Decors/Decor");
                bool        fAnyCheck  = false;
                List<int>   rgOrient = new List<int>();

				foreach( XmlElement xmlView in rgXmlViews ) {
                    string strDecor = xmlView.GetAttribute( "name" );
                    foreach( IPgMenuVisibility oDecorVis in _oDecorEnum ) {
                        if( string.Compare( oDecorVis.Shepard.Name, strDecor ) == 0 ) {
                            oDecorVis.Checked = true;
						    oDecorVis.Shepard.Hidden = false;
			                rgOrient.Add( oDecorVis.Shepard.Orientation );
                            fAnyCheck = true;
                        }
                    }
                }
                if( fAnyCheck ) {
                    DecorMenuReload();
                    foreach( int iOrientation in rgOrient ) {
			            LayoutLoadShepardsAt( iOrientation );
                    }
                }
            } catch ( Exception oEx ) {
                Type[] rgErrors = { typeof( XmlException ),
                                    typeof( ArgumentException ),
                                    typeof( ArgumentNullException ) };
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
            _rcSide .Copy   = _rcSideSave;

 			if( _miDecorMenu.DropDownItems[0] is ToolStripMenuItem oItem ) {
				oItem.Checked = true;
			}

			foreach( IPgMenuVisibility oMenu in DecorSettings ) {
				oMenu.Shepard.Hidden = !oMenu.Checked;
            }

            DecorShuffle();
        }

        public void DecorHide() {
            _rcSideSave.Copy = _rcSide;

            foreach( SmartHerderBase oShepard in this ) {
                if( oShepard.Name != "menu")
                    oShepard.Hidden = true;
            }

			if( _miDecorMenu.DropDownItems[0] is ToolStripMenuItem oItem ) {
				oItem.Checked = false;
			}

            _rcFrame.Hidden = true; // The view housed inside is still visible.
            _rcSide.Copy     = new SmartRect( 0, LayoutSizeTop(), 0, 0 );

            // No need to shuffle, since we we've disabled all the decor anyway!!
            LayoutFrame();
        }

		public void DecorToggle() {
			if( _rcFrame.Hidden == true )
				DecorShow();
			else
				DecorHide();
		}

        /// <remarks>The _rcSide rectangle is used to store the current margin settings around
        /// the inner rectangle. _rcMargin represents what we look like in the "no decor" mode.
        /// Basically space for the top menu with zero along the remaining sides.</remarks>
        protected bool IsSideOpen( int iOrientation ) {
            return( _rcSide[iOrientation] > _rcMargin.GetSide( iOrientation ) );
        }

		protected bool IsSideSavedClosed( int iOrientation ) {
			return( _rcSideSave[iOrientation] <= _rcMargin.GetSide( iOrientation ) );
		}

        /// <summary>
        /// Find all the shepard's matching the given orientation and see how many of them 
        /// have the menu item checked (and thus want to be opened). We can't use the
        /// shepard.hide method to help determine availability since that may be open
        /// or closed based on the decor availability.
        /// </summary>
        /// <remarks>This method has a side effect of shuffling the decor. Turns off the
		/// decor not from the current view.</remarks>
        protected bool IsAnyShepardReady( int iOrientation ) {
            bool fAnyReady = false;

			foreach( SmartHerderBase oShepard in _rgSideBox[iOrientation] ) {
                if( oShepard.AdornmentShuffle( _oSelectedWinSite ) ) {
                    fAnyReady = true; // Don't break on first true, so we'll shuffle the rest.
                }
			}

            return( fAnyReady );
        }

		/// <summary>
		/// Check if the side needs to be opened. It might be available but prevously,
		/// no decor, so currently hidden. 
		/// </summary>
        private void DecorSideShuffle( int iOrientation ) {
            bool   fIsSideLoaded = IsAnyShepardReady ( iOrientation );
            SCALAR eSide         = SmartRect.ToScalar( iOrientation );

            if( IsSideOpen( iOrientation) ) {
                if( !fIsSideLoaded ) {
                    _rcSideSave.SetScalar( SET.STRETCH, eSide, _rcSide[eSide]   ); // Save side value.
                    _rcSide    .SetScalar( SET.STRETCH, eSide, 0                ); // Close side.
					Invalidate();
				}
            } else {
                if( fIsSideLoaded ) {
					if( IsSideSavedClosed( iOrientation ) )
						 _rcSideSave.SetScalar( SET.STRETCH, eSide, _rcSideInit[eSide] );
                    _rcSide.SetScalar( SET.STRETCH, eSide, _rcSideSave[eSide] ); // Open side.
					Invalidate();
                }
            }
        }

        /// <summary>
        /// This get's called when the selected view changes between our center hosted views, a view
        /// gets deleted. Or when the inside is dragged around.
        /// </summary>
        /// <seealso cref="DecorSetState"/>
        protected void DecorShuffle() {
            // view site can be null if no documents are open in the editor.
            if( _oSelectedWinSite == null )
                return;

			// If all decor is hidden, then don't do anything.
			if( _rcFrame.Hidden )
				return;

            foreach( IPgMenuVisibility oMenuItem in DecorSettings ) {
                if( oMenuItem.Checked ) {
                    // Lazy create the requested decor associated with current view.
					// If site doesn't support the decor, or all decor is hidden then hide it.
                    if( DecorCreate( _oSelectedWinSite, oMenuItem.Shepard ) )
						oMenuItem.Shepard.Hidden = false;
                }
            }

			for (int iOrientation = 0;iOrientation < 4;++iOrientation) {
				DecorSideShuffle(iOrientation);
			}

			LayoutFrame();
        }

        const bool OPEN  = true;
        const bool CLOSE = false;

        /// <summary>
        /// Open or close a tool window. Which also might mean opening or closing the corresponding side.
        /// </summary>
        protected void DecorSetState( IPgMenuVisibility oMenuItem, bool fNewState ) 
        {
            if( oMenuItem == null )
                return;
			if( _rcFrame.Hidden )
				DecorShow();

            oMenuItem.Checked = fNewState; // this won't call back, because I sink OnClick() and not OnCheckedChanged().
			if( oMenuItem.Checked ) {
				oMenuItem.Shepard.Show = SHOWSTATE.Active;
			} else {
				oMenuItem.Shepard.Hidden = true;
			}

            int iOrientation = oMenuItem.Shepard.Orientation;

			LayoutLoadShepardsAt( iOrientation ); // A shepard is coming or going.

            // first set up the new decor or close the old decor.
            switch ( fNewState ) {
                case OPEN:
                    foreach( IPgMenuVisibility oCurrentMenuItem in DecorSettings ) {
                        if( _oSelectedWinSite != null && oCurrentMenuItem.Checked ) {
                            // Lazy create the requested decor associated with current view
                            if( DecorCreate( _oSelectedWinSite, oCurrentMenuItem.Shepard ) )
								oCurrentMenuItem.Shepard.Show = SHOWSTATE.Active;
							else
								oCurrentMenuItem.Shepard.Hidden = true;
                        }
                    }
                    break;
                case CLOSE:
                    oMenuItem.Shepard.AdornmentCloseAll();
                    oMenuItem.Shepard.Hidden = true;
                    break;
            }

            DecorSideShuffle( iOrientation );

            LayoutFrame();
			Invalidate  ();
        }

        internal SmartHerderBase DecorOpen( string strName, bool fOpen ) {
            foreach( IPgMenuVisibility oMenuItem in DecorSettings ) {
                if( oMenuItem.Shepard.Name == strName ) {
                    DecorSetState( oMenuItem, fOpen );
                    return( oMenuItem.Shepard );
                }
            }

            return( null );
        }

        protected void OnDecorMenuOpenCommand( object s, EventArgs e ) {
            DecorOpen( "menu", true );
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
			MenuItemHerder oMenuItem = oTarget as MenuItemHerder;

			if( oMenuItem != null ) {
				DecorSetState( oMenuItem, !oMenuItem.Checked );
			}
		}
        
    } // end class
}