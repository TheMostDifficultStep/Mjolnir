using System;
using System.Collections.Generic;

using Play.Interfaces.Embedding; 
using Play.Edit;
using Play.Parse.Impl;
using Play.Parse;

namespace Play.MorsePractice {
    public class DatagramParser : IParseEvents<char>, IDisposable {
        readonly protected DataStream2<char>    _oStream;
        readonly protected Grammer<char>        _oGrammar;
        readonly protected List<IPgCiVEvents> _rgEvents = new List<IPgCiVEvents>();

        readonly protected static Dictionary<string, string> _rgModes = new Dictionary<string, string>();

        public DatagramParser( DataStream2<char> oStream, Grammer<char> oGrammar ) {
            _oStream  = oStream  ?? throw new ArgumentNullException();
            _oGrammar = oGrammar ?? throw new ArgumentNullException();

            if( _rgModes.Count == 0 ) {
                _rgModes.Add( "00", "LSB"    );
                _rgModes.Add( "01", "USB"    );
                _rgModes.Add( "02", "AM"     );
                _rgModes.Add( "03", "CW"     );
                _rgModes.Add( "04", "RTTY"   );
                _rgModes.Add( "05", "FM"     );
                _rgModes.Add( "06", "WFM"    );
                _rgModes.Add( "07", "CW-R"   );
                _rgModes.Add( "08", "RTTY-R" );
                _rgModes.Add( "17", "DV"     );
            }
        }

        public void Dispose() {
            _rgEvents.Clear();
        }

        public void ListerAdd( IPgCiVEvents oSink ) {
            _rgEvents.Add( oSink );
        }

        public void ListerRemove( IPgCiVEvents oSink ) {
            _rgEvents.Remove( oSink );
        }

        public void OnMatch( ProdBase<char> p_oElem, int p_lStart, int p_lLength ) {
            try {
                if( p_oElem is MemoryBinder<char> oMemElem ) {
                    if( oMemElem.Target is MemoryState<char> oTarg ) {
                        string strCmdID  = GetStringBinding( _oStream, oTarg, "cmdid"  );
                        string strSource = GetStringBinding( _oStream, oTarg, "source" );
                        string strDest   = GetStringBinding( _oStream, oTarg, "dest"   );
                        string strCmdSub = GetStringBinding( _oStream, oTarg, "cmdsub" );

                        if( string.IsNullOrEmpty( strCmdID ) )
                            return;

                        if( oTarg.GetValue( "data" ) is MemoryState<char> oData  ) {
                            switch( strCmdID ) {
                                case "01": // Send the mode data (transceive)
                                    string strMode   = GetStringBinding( _oStream, oData, "mode"   );
                                    string strFilter = GetStringBinding( _oStream, oData, "filter" );
                                    foreach( IPgCiVEvents oSink in _rgEvents ) {
                                        oSink.CiVModeChange( _rgModes[strMode],  "FIL" + strFilter );
                                    }
                                    break;
                                case "03":
                                case "00": { // Send the frequency data (transceive)
                                    string[] rgDigits = { "1Hz", "10Hz", "100Hz", "1kHz", "10kHz", "100kHz", "1mHz", "10mHz", "100mHz", "1gHz" };
                                    int      iPow     = 1;
                                    int      iResult  = 0;
                                    foreach( string strDigit in rgDigits ) {
                                        string strValue = GetStringBinding( _oStream, oData, strDigit );
                                        iResult += int.Parse( strValue ) * iPow;
                                        iPow *= 10;
                                    }
                                    foreach( IPgCiVEvents oSink in _rgEvents ) {
                                        oSink.CiVFrequencyChange( iResult );
                                    }
                                } break;
                            }
                        }
                    }
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( ArgumentNullException ),
                                    typeof( FormatException ),
                                    typeof( OverflowException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
            }
        }

        public static string GetStringBinding( 
            DataStream<char>  rgTextStream,
            MemoryState<char> oMemState, 
            string            strIndex 
        ) {
            try {
                int iIndex = oMemState.Class.Bindings.IndexOfKey( strIndex );

                // Only the memory element has the stream offset. IColorRange is a line offset.
                if( iIndex > -1 ) {
                    if( oMemState.Values[iIndex] is MemoryElem<char> oMemory )
                        return rgTextStream.SubString( oMemory.Start, oMemory.Length );
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( IndexOutOfRangeException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
            }
            return( string.Empty );
        }

        public void OnParserError( ProdBase<char> p_oMemory, int p_iStart ) {
            //throw new ApplicationException( "Panic in datagram parser" );
            // Arg... really need a pointer to the document to spew some errors.
        }

        public void Parse() {
			State<char> oStart = _oGrammar.FindState("start");

            if( oStart == null )
			    throw new ApplicationException( "Couldn't find datagram grammar start" );

            _oStream.Position = 0;

            MemoryState<char>   oMStart = new MemoryState<char>( new ProdState<char>( oStart ), null );
            ParseIterator<char> oParser = new ParseIterator<char>( _oStream, this, oMStart );

            while( oParser.MoveNext() );
        }
    }

}
