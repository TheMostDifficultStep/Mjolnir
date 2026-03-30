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

        /// <summary>
        /// Should be able to merge with XmlSlotRefCount now...
        /// </summary>
        /// <seealso cref="XmlSlotRefCount"/>
        public class XmlSlotRefCount2 : 
            BaseSlot, 
            IDocSlot,
            IXmlSlot
        {
            protected IPgSave<XmlNode> _oGuestSave;

            public XmlSlotRefCount2( Program oProgram, PgDocDescr oDescriptor, string strName ) : 
                base( oProgram, oDescriptor.Controller, oDescriptor.FileExtn ) 
            {
                //CheckLocation( true ); Need do do something like this...
                _strFilePath = strName;
            }
            public XmlSlotRefCount2( Program oProgram, PgDocDescr oDescriptor ) : 
                base( oProgram, oDescriptor.Controller, oDescriptor.FileExtn ) 
            {
            }


            protected override void GuestSet( IDisposable value ) {
                base.GuestSet( value );

                _oGuestSave = (IPgSave<XmlNode>)value;
            }

            public override string LastPath {
                get { return string.Empty; }
            }

            public virtual bool IsInternal { get; set;} = false; // clock and fileman override.
            public void SetID( int iID ) { ID = iID; }

            public override bool InitNew() {
                IPgLoad<TextReader> oGuestReader = _oGuestSave as IPgLoad<TextReader>;
                if( oGuestReader == null ) {
                    LogError( "Guest does not support IPgLoad<TextReader>." );
                    return( false );
                }

                oGuestReader.InitNew();
                return( true );
            }

            public bool Save( XmlNode oXmlFileNode ) {
                if( oXmlFileNode == null ) {
                    LogError( "Missing file node to save into" );
                    return false;
                }

                XmlNode xmlFrag = oXmlFileNode.OwnerDocument.CreateDocumentFragment();

                if( _oGuestSave.Save( xmlFrag ) ) {
                    oXmlFileNode.AppendChild( xmlFrag );
                    return true;
                }

                return false;
            }

            /// <summary>
            /// DocSlot want's this....
            /// </summary>
            public override bool Load( string strFilename ) {
                return false;
            }

            public bool Load( XmlNode xmlParent ) {
                if( xmlParent == null ) {
                    LogError( "Missing Parent XML node to load from" );
                    return( false );
                }

                IPgLoad<TextReader> oGuestLoad = _oGuestSave as IPgLoad<TextReader>;
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
                    case ShellNotify.MonikerChanged:
                        _oHost.Raise_UpdateTitles( this );
                        break;
				}
            }

            public override bool IsDirty {
                get { return( _oGuestSave.IsDirty ); }
            }
        }
        /// <summary>
        /// This is the start of a new xml subtree saving slot. The find dialog will likely
        /// need a complex persistance: find string, search type, match case. For example.
        /// </summary>
        /// <seealso cref="XmlSlogNoRef"/>
        public class ComplexXmlSlot : 
            BaseSlot,
            IDocSlot 
        {
            public ComplexXmlSlot( Program oProgram, PgDocDescr oDescriptor, string strName ) : 
                base( oProgram, oDescriptor.Controller, oDescriptor.FileExtn ) 
            {
                _strFilePath = strName; // TODO: This looks a little odd. :-/
            }

            public override bool InitNew() {
                return true;
            }

            public override void Dispose() {
                // We don't want to dispose the program. The program is both the guest and host at present.
            }

            public bool IsInternal => false;

            /// <summary>
            /// This is implmented on the BaseSlot AND IDocSlot, that's why I need to have
            /// the BaseSlot as virtual.
            /// </summary>
            public override string LastPath {
                get { return string.Empty; }
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

            /// <summary>
            /// DocSlot want's this...
            /// </summary>
            public override bool Load( string strFilename ) {
                return false;
            }

            public bool Save(bool fNewLocation) {
                return true;
            }
        }
	} // End class
}
