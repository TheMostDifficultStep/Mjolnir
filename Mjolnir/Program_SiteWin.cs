using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Xml;
using System.Web;
using System.Windows.Forms;

using Play.Interfaces.Embedding;

namespace Mjolnir {
	public partial class Program {
		/// <summary>
		/// degenerate slot for use by the program to host the alerts window. This view is not like
		/// the full function views residing in the multi/doc system of the main window. 
		/// </summary>
		public class ViewSlot:
			IPgViewSite,
			IPgViewNotify,
			IEnumerable<ColorMap>
		{
			protected readonly Program _oProgram;

			public ViewSlot( Program oProgram ) {
				_oProgram = oProgram ?? throw new ArgumentNullException();
			}

			public Control Guest {
				get;
				set;
			}

			public IPgParent     Host       => _oProgram;
			public IPgViewNotify EventChain => this;

			public void LogError(string strMessage, string strDetails, bool fShow=true) {
				_oProgram.LogError( this, strMessage, strDetails, fShow );
			}

			public void OnDocDirty() {
			}

			public void Notify( ShellNotify eEvent ) {
			}

			public bool IsCommandKey(CommandKey ckCommand,KeyBoardEnum kbModifier) {
				return( false );
			}

			public bool IsCommandPress(char cChar) {
				return( false );
			}

			public void NotifyFocused(bool fSelect) {
			}

			public IEnumerator<ColorMap> GetEnumerator() {
				return( _oProgram.GetSharedGrammarColors() );
			}

			IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

			public bool InitNew() {
				try {
					return ((IPgLoad)Guest).InitNew();
				} catch( InvalidCastException ) {
					LogError( "hosting", "Primary view must support IPgLoad<TextReader>", true );
				}
				return( false );
			}
		} // End class

        public class XmlSlot : 
            BaseSlot, 
            IDocSlot // Kind of a drag to inherit from IDocSlot, since Load( filename ) needs to be implemented. Look at this later.
        {
            protected IPgSave<TextWriter> _oGuest;

            public XmlSlot( Program oProgram, string strControllerExtn, string strName ) : 
                base( oProgram, oProgram.GetController( strControllerExtn ), strControllerExtn ) 
            {
                _strFileName = strName;
            }

            protected override void GuestSet( IDisposable value ) {
                base.GuestSet( value );

                _oGuest = (IPgSave<TextWriter>)value;
            }

            public string LastPath {
                get { return( string.Empty ); }
            }

            public override bool InitNew() {
                IPgLoad<TextReader> oGuestReader = _oGuest as IPgLoad<TextReader>;
                if( oGuestReader == null ) {
                    LogError( "Guest does not support IPgLoad<TextReader>." );
                    return( false );
                }

                oGuestReader.InitNew();
                return( true );
            }

            public void Save( XmlElement xmlParent ) {
                if( xmlParent == null ) {
                    LogError( "Missing Parent XML node to save into" );
                }
                // I once used a MemoryStream on StreamWriter to capture the save of the guest and then
                // write into the XML via a StreamReader. But this is way better.
                // BUG: Except if there is markup in the stream!!
                using( TextWriter srSave = new StringWriter() ) {
                    _oGuest.Save( srSave );
                    srSave.Flush();
                    xmlParent.InnerXml = HttpUtility.HtmlEncode( srSave.ToString() );
                }
            }

            public bool Load( XmlElement xmlParent ) {
                if( xmlParent == null ) {
                    LogError( "Missing Parent XML node to load from" );
                    return( false );
                }

                IPgLoad<TextReader> oGuestLoad = _oGuest as IPgLoad<TextReader>;
                if( oGuestLoad == null ) {
                    LogError( "Guest does not support IPgLoad<STextReader>." );
                    return( false );
                }

                using( TextReader oReader = new StringReader(HttpUtility.HtmlDecode( xmlParent.InnerXml )) ) {
                    if( !oGuestLoad.Load( oReader ) ) {
                        LogError( "Couldn't save favorites into session." );
                        return( false );
                    }
                }

                return( true );
            }

            //protected void LogError( string strMessage ) {
            //    LogError( "session", strMessage );
            //}

            /// <summary>
            /// We're getting a save request from our guest. So we'll save the
            /// entire session.
            /// </summary>
            /// <param name="fNew">Ignored. You cannot rename the substorage spots in the session XML.</param>
            /// <returns>true</returns>
            public virtual bool Save( bool fNew ) {
                _oHost.SessionSave( false );
                return( true );
            }
            public override void Notify( ShellNotify eEvent ) {
				switch( eEvent ) {
					case ShellNotify.DocumentDirty:
						_oHost.SessionDirtySet();
						break;
				}
            }

            public override bool IsDirty {
                get { return( _oGuest.IsDirty ); }
            }

            /// <summary>
            /// We don't track the reference counts to the associated document
            /// since we never close is. These are permanent documents for the
            /// lifetime of the program.
            /// </summary>
            public new int Reference {
                get { return( 0 ); }
                set { }
            }

        } // End class

        /// <summary>
        /// This is the start of a new xml subtree saving slot. The find dialog will likely
        /// need a complex persistance: find string, search type, match case. For example.
        /// </summary>
        public class ComplexXmlSlot : 
            BaseSlot,
            IDocSlot 
        {
            public ComplexXmlSlot( Program oProgram ) : 
                base( oProgram, new ControllerForTopLevelWindows( oProgram ), ".finddialog" ) 
            {
                _strFileName = "Find Dialog";
            }

            public override bool InitNew() {
                return true;
            }

            public override void Dispose() {
                // We don't want to dispose the program. The program is both the guest and host at present.
            }

            public string LastPath {
                get { return( string.Empty ); }
            }

            public override void Notify( ShellNotify eEvent ) {
            }

            public override bool IsDirty {
                get { return false; }
            }
            /// <summary>
            /// We don't track the reference counts to the associated document
            /// since we never close is. These are permanent documents for the
            /// lifetime of the program.
            /// </summary>
            public new int Reference {
                get { return( 0 ); }
                set { }
            }

            public bool Save(bool fNewLocation) {
                return true;
            }
        }
	} // End class
}
