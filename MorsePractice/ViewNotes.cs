using System;
using System.Windows.Forms;

using Play.Interfaces.Embedding;
using Play.Parse;
using Play.Edit;

namespace Play.MorsePractice {
    /// <summary>
    /// this is an embedded view in the ViewQrz view.
    /// </summary>
    class ViewBio : EditWindow2 { 
        protected readonly ViewQrz  _oParent;
        protected readonly DocStdLog _oDocMorse;

        public ViewBio(IPgViewSite oSiteView, DocStdLog oDocument ) : 
            base( oSiteView, oDocument.CallSignBio )
        {
            _oDocMorse  = oDocument ?? throw new ArgumentNullException( "Document must not be null.");
            _oParent    = oSiteView.Host as ViewQrz ?? throw new ArgumentException("Our parent must be the ViewQrz object" );
        }

        /// <seealso cref="ViewBio.InitNewInternal"/>
        protected override bool InitInternal() {
            if( !base.InitInternal() )
                return false;

            if( HyperLinks.ContainsKey( "callsign" ) ) {
                HyperLinks.Remove( "callsign" );
                HyperLinks.Add( "callsign", OnCallSign );
            }
            return true;
        }

        protected void OnCallSign(Edit.Line oLine, IPgWordRange oRange ) {
            try { 
                _oParent.StationLoad( oLine.SubString( oRange.Offset, oRange.Length ) );
            } catch(Exception oEx ) {
				Type[] rgErrors = { typeof( InvalidCastException ),
                                    typeof( NullReferenceException ) };
				if(rgErrors.IsUnhandled(oEx ) )
					throw;

				LogError( "ViewSolo", "Problem with selection ship" );
            }
        }
    }

    /// <summary>
    /// This is the editor that does not show the outline with the callsign tally. This is used
    /// by the "stdlog" file type.
    /// </summary>
    public class ViewSimple : EditWindow2 {
        public static readonly Guid _guidViewCategory = new Guid("{868D414A-5614-4D9D-8F7E-C46D85BCE294}");

		public override Guid Catagory => _guidViewCategory;

        protected readonly DocStdLog _oDocNotes;

        public ViewSimple(IPgViewSite oSiteView, DocStdLog oDocument ) : 
            base( oSiteView, oDocument.Notes )
        {
            _oDocNotes     = oDocument ?? throw new ArgumentNullException( "Document must not be null.");
            _iSelectedTool = 1;
        }

        public override object Decorate( IPgViewSite oBaseSite, Guid sGuid ) {
            try {
                if( sGuid.Equals( GlobalDecor.Properties ) ) {
                    return new ViewRadioProperties( oBaseSite, _oDocNotes.Properties );
                }
                return base.Decorate(oBaseSite, sGuid);
            } catch (Exception oEx) {
                Type[] rgErrors = { typeof( NotImplementedException ),
                                    typeof( NullReferenceException ),
                                    typeof( ArgumentException ),
                                    typeof( ArgumentNullException ) };
                if (rgErrors.IsUnhandled(oEx))
                    throw;

                LogError("decor", "Couldn't create EditWin decor: " + sGuid.ToString());
            }

            return (null);
        }

        public override bool Execute( Guid sGuid ) {
            if( _oDocNotes.Execute( sGuid ) )
                return true;

            return base.Execute(sGuid);
        }

        protected override void OnKeyDown(KeyEventArgs e) {
            if( e.KeyCode == Keys.T && e.Control == true ) {
                _oDocNotes.InsertFreqDateTime();
                ScrollTo(SCROLLPOS.BOTTOM);
            }
            base.OnKeyDown(e);
        }
    }

    /// <summary>
    /// This is a stand alone document view to be used to show the notes with a 
    /// decoration that is list of callsigns found in the document. This is the
    /// old "stdlog" viewer that could show the unique calls on the left!
    /// </summary>
    class ViewNotes :
        EditWindow2
    {
        public static readonly Guid _guidViewCategory = new Guid("{9fe5d0bc-0b91-4556-bf52-f9d823a7346c}");

		public override Guid Catagory => _guidViewCategory;

        protected readonly IPgShellSite _oSiteShell;
        protected readonly DocStdLog     _oDocMorse;

        public ViewNotes(IPgViewSite oSiteView, DocStdLog oDocument ) : 
            base( oSiteView, oDocument.Notes )
        {
            _oDocMorse  = oDocument ?? throw new ArgumentNullException( "Document must not be null.");
            _oSiteShell = oSiteView as IPgShellSite ?? throw new ArgumentException("Parent view must provide IPgShellSite service");
            _iSelectedTool = 1;
        }

        /// <summary>
        /// At present we don't use this, we used to open our own browser, but now call the 
        /// system set browser.
        /// </summary>
        protected void OnCallSign(Edit.Line oLine, IPgWordRange oRange ) {
            try { 
                _oSiteShell.AddView(ViewQrz._guidViewCategory, fFocus: true);

                _oDocMorse.StationLoad( oLine.SubString( oRange.Offset, oRange.Length ) );
            } catch(Exception oEx ) {
				Type[] rgErrors = { typeof( InvalidCastException ),
                                    typeof( NullReferenceException ),
                                    typeof( ArgumentOutOfRangeException ) };
				if(rgErrors.IsUnhandled(oEx ) )
					throw;

				LogError( "ViewSolo", "Problem with selection ship" );
            }
        }

        // BUG: Need to refresh the called line after the reparse on notes comes.
        protected override void Raise_Navigated( NavigationSource eSource, ILineRange oCarat ) {
            base.Raise_Navigated(eSource, oCarat);

            // Look to see if a callsign in our Calls file is where the cursor is.
            foreach( IColorRange oColor in oCarat.Line.Formatting ) {
                if( oColor is IPgWordRange oWord && oWord.StateName == "callsign" && oWord.Offset == 0 ) {
                    string strCaratCall = oCarat.Line.SubString( oWord.Offset, oWord.Length );

                    foreach( Edit.Line oCallLine in _oDocMorse.Calls ) {
                        if( string.Compare( oCallLine.SubString( 0, oWord.Length ),
                                            strCaratCall, ignoreCase:true ) == 0 )
                        {
                            _oDocMorse.Calls.HighLight = oCallLine;
                            return;
                        }
                    }
                }
            }
            _oDocMorse.Calls.HighLight = null;
        }

        /// <seealso cref="ViewBio.InitNewInternal"/>
        protected override bool InitInternal() {
            if( !base.InitInternal() )
                return false;

            //if (this.ContextMenuStrip != null) {
            //    ContextMenuStrip oMenu = this.ContextMenuStrip;
            //    oMenu.Items.Add(new ToolStripMenuItem("Copy", null, new EventHandler(this.OnCut), Keys.Control | Keys.X));
            //}

            // I like the standard callsign hyperlink for now. Well make an
            // addornment to show stations in the future.
            //try {
            //    if( HyperLinks.ContainsKey( "callsign" ) ) {
            //        HyperLinks.Remove( "callsign" );
            //        HyperLinks.Add( "callsign", OnCallSign );
            //    }
            //} catch( ArgumentException ) {
            //    LogError( "Hyperlink Setup", "Failed upt update Callsign Hyperlink callback" );
            //}

            return true;
        }

        public override object Decorate( IPgViewSite oBaseSite, Guid sGuid ) {
            try {
                if (sGuid.Equals(GlobalDecor.Outline)) {
                    // TODO: When Calls gets updated, it would be nice to reset the hilighted line.
                    //       Need some way to communicate with outline owner w/o creating a nightmare.
                    return new EditWindow2(oBaseSite, _oDocMorse.Calls, fReadOnly: true, fSingleLine: false);
                }
                if( sGuid.Equals( GlobalDecor.Properties ) ) {
                    return new ViewRadioProperties( oBaseSite, _oDocMorse.Properties );
                }
                return base.Decorate(oBaseSite, sGuid);
            } catch (Exception oEx) {
                Type[] rgErrors = { typeof( NotImplementedException ),
                                    typeof( NullReferenceException ),
                                    typeof( ArgumentException ),
                                    typeof( ArgumentNullException ) };
                if (rgErrors.IsUnhandled(oEx))
                    throw;

                LogError("decor", "Couldn't create EditWin decor: " + sGuid.ToString());
            }

            return (null);
        }

        public override bool Execute( Guid sGuid ) {
            if( _oDocMorse.Execute( sGuid ) )
                return true;

            return base.Execute( sGuid );

            //if( sGuid == GlobalCommands.Play ) {
            //    _oDocMorse.CiVFrequencyChange( 51470000 ); // test without key up repeater. (intput frequency)
            //    return true;
            //}
        }
    } // End class
}
