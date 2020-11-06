using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

using Play.Interfaces.Embedding;
using Play.Parse.Impl;

namespace Play.Edit {
    public partial class BaseEditor {
        /// <summary>
        /// New object for efficient bulk changes to the file. Selection uses this.
        /// Replace all selection works now, but undo of that operation fails.
        /// TODO: Have the MasterUndo functions use the bulk undo.
        /// TODO: Break this out into a set of manipulators. I'm pretty sure I've only tested
        ///       calling one method on the manipulater per use. 
        /// </summary>
        public class Manipulator : 
            IDisposable 
        {
            readonly BaseEditor              _oDocument;
            readonly IArray<Line>            _rgLines;
            readonly ICollection<ILineRange> _rgCaretsToReset = new List<ILineRange>();
			         bool                    _fSendEvents;

            public Manipulator( BaseEditor oDoc, bool fSendEvents = true ) {
				_oDocument   = oDoc ?? throw new ArgumentNullException( "Editor must not be null" );
                _rgLines     = oDoc._rgLines;
				_fSendEvents = fSendEvents;
            }

            public void Dispose() {
                // TODO: Really only need to count from lowest line we edited.
                _oDocument.CharacterCount( 0 );

                foreach( ILineRange oCaret in _rgCaretsToReset ) {
                    oCaret.Line   = _oDocument.GetLine(0); // Might add a dummy. Will need to flush out later.
                    oCaret.Offset = 0;
                }

                // TODO: Unfortunately, we don't know if we did anything!!!
                //       Fix it to keep track if anything happened.
				if( _fSendEvents )
					_oDocument.Raise_BufferEvent( BUFFEREVENTS.MULTILINE );
            }

            /// <summary>
            /// Check if the line is even in the document!
            /// </summary>
            /// <param name="iLine"></param>
            /// <returns></returns>
            public bool IsHit( int iLine ) {
                return( _oDocument.IsHit( iLine ) );
            }

            /// <summary>
            /// Fails silently if you haven't assigned the site yet. Kind of a bummer but there's
            /// no reason to throwing exceptions just because you didn't wire it up correctly.
            /// Sort of weird talking thru the document's site. Might want to look into that.
            /// </summary>
            /// <param name="strError"></param>
            private void Raise_HostError( string strError ) {
                if( _oDocument.Site != null ) {
                    _oDocument.Site.LogError( "edit", strError );
                }
            }

            /// <summary>
            /// Try to insert a single line of text. 
            /// </summary>
            /// <param name="iLine"></param>
            /// <param name="strLine"></param>
            /// <remarks>What about -append- at end. It's essentially an insert before the Count position.</remarks>
            public Line LineInsert( int iLine, string strLine, bool fUndoable = true ) {
                if( iLine < 0 || iLine > _rgLines.ElementCount ) {
                    Raise_HostError("LineInsert(): Line is out of range.");
                    return ( null );
                }
                Line oLine = _oDocument.CreateLine( iLine, strLine);

                _rgLines.Insert( iLine, oLine );

                // TODO: I can save the lowest line and re-count from there at the end, instead on each load.

                _oDocument.UndoPush( new UndoLineInsert( iLine ) );
                _oDocument.SetDirty();

                _oDocument.Raise_AfterInsertLine( oLine );

                return( oLine );
            }

            /// <summary>
            /// What I would like is to append to current position if line is empty else add to the nextline.
            /// I'm just hacking it for now.
            /// </summary>
            /// <param name="strLine"></param>
            /// <returns></returns>
            public Line LineAppend( string strLine ) {
                return( LineInsert( _rgLines.ElementCount, strLine ) );
            }

            /// <summary>
            /// This is a good reason why I need different manipulator classes. This
            /// LineAppend is actually part of a entire buffer fill from empty.
            /// All undo has to do is delete EVERYTHING in on fell sweep. I'm not going
            /// to create an undo unit for this action. This manipulation would be like
            /// a mini textstream reader.
            /// </summary>
            /// <param name="strLine"></param>
            /// <returns></returns>
            public Line LineAppendNoUndo( string strLine ) {
                try {
                    int  iLine = _rgLines.ElementCount;
                    Line oLine = _oDocument.CreateLine( iLine, strLine);

                    _rgLines.Insert( _rgLines.ElementCount, oLine );

                    _oDocument.SetDirty();
                    _oDocument.Raise_AfterInsertLine( oLine );

                    return( oLine );
                } catch( NullReferenceException ) {
                    return( null );
                }
            }

            public void LineDelete( int iLine ) {
                try {
                    if( !IsHit( iLine ) ) {
                        Raise_HostError( "Manipulator::LineDelete(): Line is out of range." );
                        return;
                    }
                    Line oLine = _rgLines[iLine];

                    _oDocument.Raise_BeforeLineDelete( oLine );

                    foreach( ILineRange oCaret in _oDocument.CaretEnumerable ) {
                        if( oCaret.Line != null && oCaret.At == oLine.At ) {
                            if( iLine > 0 ) {
                                // Hmmm... probably should be the NEXT line not the prev...
                                Line oNewLine = _rgLines[iLine - 1];
                                if( oNewLine != null ) {
                                    oCaret.Line   = oNewLine;
                                    oCaret.Offset = oNewLine.ElementCount;
                                } else {
                                    Raise_HostError( "Manipulator::LineDelete(): Line above is null." );
                                }
                            } else {
                                _rgCaretsToReset.Add( oCaret );
                            }
                        }
                    }

                    _oDocument.UndoPush( new UndoLineDelete( iLine, oLine.ToString() ) );
                    _rgLines.RemoveAt( iLine );
                    oLine.Dispose();
                    _oDocument.SetDirty();

                    //_oDocument.Raise_ChangesFinished();
                } catch( NullReferenceException ) {
                    _oDocument.LogError( "Error in LineDelete" );
                }
            }

            /// <summary>
            /// Function to convert a stream into a string list. No side effects.
            /// It's public but it could be private.
            /// </summary>
            /// <param name="oStream"></param>
            /// <returns>A list of strings. May be empty.</returns>
            public static List<string> StreamConvert(IPgBaseSite oSite, string strEOL, TextReader oStream)
            {
                List<string>  rgNewLines = new List<string>();
                StringBuilder sbNewLine  = new StringBuilder();
                Int32         iChar      = 0;

                try
                {
                    do
                    {
                        iChar = oStream.Read();

                        if (iChar == '\r')
                        {
                            rgNewLines.Add(sbNewLine.ToString());
                            sbNewLine.Length = 0;

                            iChar = oStream.Read();
                            if (iChar == '\n')
                            {
                                continue;
                            }
                            // else fall thru!!!
                        }
                        if (iChar == '\n')
                        {
                            rgNewLines.Add(sbNewLine.ToString());
                            sbNewLine.Length = 0;
                        }
                        if (iChar > -1)
                        {
                            sbNewLine.Append((char)iChar);
                        }
                    } while (iChar > -1);

                    if (sbNewLine.Length > 0)
                    {
                        rgNewLines.Add(sbNewLine.ToString());
                    }
                    else
                    {
                        // if there is no text that means the last line had an EOL
                        // and so we want to add it to our stream! if we used ReadLine()
                        // we don't know if it's there or not
                        rgNewLines.Add(strEOL);
                    }
                }
                catch (IOException oEx)
                {
                    if (oSite != null) {
                        oSite.LogError("Error reading stream contents", oEx.StackTrace);
                    }
                }

                return (rgNewLines);
            }

            public bool StreamInsert(int iLine, int iOffset, TextReader oStream) 
            {
                if( iLine < 0 )
                    iLine = 0;
                
                if( iLine > _rgLines.ElementCount )
                    iLine = _rgLines.ElementCount;
                    
                List<string> rgNewLines = StreamConvert( _oDocument.Site, _oDocument.EOL, oStream );

                if( rgNewLines.Count > 0 ) {
                    using( _oDocument.UndoMasterBegin() ) {
                        if( _rgLines.ElementCount == 0 ) {
                            Line oLine = _oDocument.CreateLine( 0, string.Empty );
                            _rgLines.Insert(0, oLine );
                            _oDocument.UndoPush(new UndoLineInsert(0));
                            _oDocument.Raise_AfterInsertLine(oLine);
                        }
                        // First new line might insert somewhere in the middle of an old line, thus 
                        // we start by splitting the old line and insert at the end of the top of the split.
                        if( rgNewLines.Count > 1 ) {
                            LineSplit( iLine, iOffset );
                        } 

                        if( rgNewLines[0].Length > 0 ) // if an empty line, nothing to insert.
                            LineTextInsert( iLine, iOffset, rgNewLines[0], 0, rgNewLines[0].Length );
                        iLine++;

                        // Following lines always insert at horizontal position 0.
                        int i = 1;
                        for( i = 1; i < rgNewLines.Count - 1; ++i, ++iLine ) {
                            // Remember the EOL is striped. So the string on the line might be empty!
                            string strNew = rgNewLines[i];

                            if( strNew == null )
                                strNew = string.Empty;

                            Line oLine = _oDocument.CreateLine( iLine, strNew );
                            _rgLines.Insert(iLine, oLine );
                            _oDocument.UndoPush(new UndoLineInsert(iLine));

                            _oDocument.Raise_AfterInsertLine(oLine);
                        }

                        // And deal with the last line.
                        if( i < rgNewLines.Count ) {
                            string strNew = rgNewLines[i];
                            if( !string.IsNullOrEmpty( strNew ) ) {
                                // Last is empty if the EOL was selected on the last line of copied text.
                                if( strNew.Contains( _oDocument.EOL ) ) {
                                    strNew = strNew.Replace( _oDocument.EOL, string.Empty );
                                }
                                // Only insert string if it's not empty after the replacement above.
                                if( strNew != string.Empty ) {
                                    LineTextInsert( iLine, 0, strNew, 0, strNew.Length);
                                }
                            }
                        }
                    } // end using
                }
                // I don't renumber the lines here since that's done on the dispose of this manipulator.
                // TODO: It would be nice if renumber was right here so I know it get's done.

                return( true );
            } // end method

            /// <summary>
            /// Insert multiple characters into the line. A single formatting unit is
            /// added for the inserted text. Parsing must occur after this insert. Be careful, 
            /// this function does not raise the ChangesFinished() event!
            /// </summary>
            /// <param name="iDestOffset">Where in the line to insert the new text</param>
            /// <param name="strSource">The string to insert.</param>
            /// <param name="iSrcIndex">Where in the string to start copy characters from.</param>
            /// <param name="iSrcLength">The number of characters to copy.</param>
            /// <returns></returns>
            /// <remarks>TODO: Alot like the document version, can I union the two?</remarks>
            public bool LineTextInsert( 
                int    iLine,
                int    iDestOffset, 
                string strSource, 
                int    iSrcIndex, 
                int    iSrcLength
            ) {
                try {
                    if( !IsHit( iLine ) ) {
                        Raise_HostError("TextInsert(): Line is out of range.");
                        return( false );
                    }

                    Line oLine = _rgLines[iLine];

                    bool fResult = oLine.TryInsert(iDestOffset, strSource, iSrcIndex, iSrcLength);
                    if( fResult ) {
                        foreach( ILineRange oCaret in _oDocument.CaretEnumerable ) {
                            if( oCaret.Line != null && oCaret.At == oLine.At )
                                Marker.ShiftInsert(oCaret, iDestOffset, iSrcLength);
                        }

                        _oDocument.UndoPush(new UndoInsert(iLine, iDestOffset, iSrcLength));
                        _oDocument.SetDirty();
                        _oDocument.CharacterCount( iLine );
                        _oDocument.Raise_AfterLineUpdate( oLine, iDestOffset, iSrcIndex, iSrcLength );
                        //_oDocument.Raise_ChangesFinished(); 
                    }
                    return( fResult );
                } catch ( NullReferenceException ) {
                    return( false );
                }
            }

            public void LineCharInsert( int iLine, int iIndex, char cChar ) {
                _oDocument.LineCharInsert( iLine, iIndex, cChar );
            }

            /// <summary>
            /// Try to delete the given range in this line.
            /// </summary>
            /// <param name="iLine">The line you want to delete text on.</param>
            /// <param name="oRange">Range of text on this line to delete.</param>
            public void LineTextDelete( int iLine, IMemoryRange oRange ) {
                try {
                    if( !IsHit( iLine ) ) {
                        _oDocument.Site.LogError( "Manipulator::LineDelete(): Line is out of range.", string.Empty );
                        return;
                    }

                    if( oRange.Length <= 0 ) 
                        return;

                    string strRemoved = string.Empty;
                    Line oLine = _rgLines[iLine];

                    //_oDocument.Raise_BeforeLineUpdate( iLine, oRange.Offset, oRange.Length, 0 );

                    if( oLine.TryDelete( oRange.Offset, oRange.Length, out strRemoved ) ) {
                        foreach( IColorRange oFormat in oLine.Formatting ) {
                            Marker.ShiftDelete( oFormat, oRange );
                        }
                        foreach( ILineRange oCaret in _oDocument.CaretEnumerable ) {
                            if( oCaret.Line != null && oCaret.At == oLine.At ) {
                                if( oCaret.Offset >= oRange.Offset ) {
                                    oCaret.Offset = oRange.Offset;
                                }
                            }
                        }
                        _oDocument.UndoPush( new UndoDelete( iLine, oRange.Offset, strRemoved ) );
                        _oDocument.SetDirty();
                        _oDocument.Raise_AfterLineUpdate( oLine, oRange.Offset, oRange.Length, 0 );
                    }

                    //_oDocument.Raise_ChangesFinished();
                } catch( NullReferenceException ) {
                     _oDocument.LogError( "Error in LineTextDelete" );
               }
            }

            /// <summary>
            /// Try to merge this line with the following line. The only real
            /// reason for failure would be not enough lines in the buffer. Which
            /// in and of itself is harmless enough.
            /// </summary>
            public void LineMergeWithNext( int iLine ) {
                try {
                    if( !IsHit( iLine ) ) {
                        _oDocument.Site.LogError( "Manipulator::LineMerge(): Line is out of range.", string.Empty );
                        return;
                    }
                    if( !IsHit( iLine + 1 ) )
                        return;

                    int  iNext = iLine + 1;
                    Line oNext = _rgLines[iNext];
                    Line oLine = _rgLines[iLine];
                    int iEndOffset = oLine.ElementCount;
                    int iNewLength = oLine.ElementCount + oNext.ElementCount;

                    _oDocument.Raise_BeforeLineDelete( oNext );
                    //_oDocument.Raise_BeforeLineUpdate( iLine, iEndOffset, 0, iNewLength );

                    // Note: Merge and Split aren't exact mirrors of each other. 
                    // Do not call TryTextInsert(), since it creates an undo unit. 
                    if( oLine.TryInsert( iEndOffset, oNext.ToString(), 0, oNext.ElementCount ) ) {
                        foreach( IColorRange oRange in oNext.Formatting ) {
                            oRange.Offset += iEndOffset;
                            oLine.Formatting.Add( oRange );
                        }
                        foreach( ILineRange oCaret in _oDocument.CaretEnumerable ) {
                            if( oCaret.Line != null && oCaret.At == oNext.At ) {
                                oCaret.Line   = oLine; // Gotta do this first to prevent clipping to previous line length. ^_^;;
                                oCaret.Offset += iEndOffset;
                            }
                        }
                        _oDocument.UndoPush( new UndoMerge( iLine, iEndOffset ) );

                        _rgLines.RemoveAt( iNext );
                        oNext.Dispose();
                        _oDocument.SetDirty();

                        _oDocument.Raise_AfterLineUpdate( oLine, iEndOffset, 0, iNewLength );
                    }
                    //_oDocument.Raise_ChangesFinished();
                } catch( NullReferenceException ) {
                    _oDocument.LogError( "Error in LineMergeWithNext" );
                }
            } // end method

            /// <summary>
            /// Try to split the line AFTER the given offset. A new line will be added
            /// after the line that is split. The remainder of this line is put into
            /// the new line along with any unaffected formatting.
            /// </summary>
            /// <param name="iOffset">Index representing the first chararacter of the 
            /// remainder of this line to be moved to a new next line.</param>
            /// <returns>True if successful, false if not.</returns>
            public bool LineSplit(int iLine, int iOffset) 
            {
				if( _oDocument.ElementCount == 0 ) {
					LineInsert( 0, string.Empty );
				}
                if( !IsHit( iLine ) ) {
                    Raise_HostError( "LineSplit(): Line is out of range.");
                    return ( false );
                }

                string strRemainder = string.Empty;
                Line   oLine        = _rgLines[iLine];
                int    iModified    = oLine.ElementCount - iOffset;

                // Since we are part of the editor lines then insert the remainder of the
                // old line onto the next line.
                bool fReturn = oLine.TryDelete( iOffset, int.MaxValue, out strRemainder );
                if( fReturn ) {
                    Line oLineAfter = _oDocument.CreateLine( iLine + 1, strRemainder );

                    if( oLine.Formatting.Count > 0 ) {
                        Marker.SplitNext( oLine.Formatting, iOffset, oLineAfter.Formatting );

                        // This catches any new characters typed at the end of the line so you can see them!
                        oLineAfter.Formatting.Add(new ColorRange( oLineAfter.ElementCount, 1, 0));
                    }
                    _rgLines.Insert( iLine + 1, oLineAfter );
                    _oDocument.CharacterCount( iLine );
                    _oDocument.SetDirty();

                    _oDocument.Raise_AfterLineUpdate( oLine, iOffset, iModified, 0 );
                    _oDocument.Raise_AfterInsertLine( oLineAfter );

                    foreach( ILineRange oRange in _oDocument.CaretEnumerable ) {
                        if( oRange.Line != null && oRange.At == oLine.At && oRange.Offset >= iOffset ) {
                            oRange.Line   = oLineAfter;
                            oRange.Offset = 0;
                        }
                    }
                    _oDocument.UndoPush(new UndoSplit(iLine, iOffset));
                }

                //_oDocument.Raise_ChangesFinished();

                return ( true );
            } // end method

            public void DeleteAll( bool fUndo = true )
            {
                try {
                    for( int i = _rgLines.ElementCount - 1; i >= 0; --i ) {
                        Line oLine = _rgLines[i];
                        _oDocument.Raise_BeforeLineDelete( oLine );

                        _oDocument.UndoPush( new UndoLineDelete( i, oLine.ToString() ) );
                        oLine.Dispose();
                    }

                    foreach( ILineRange oCaret in _oDocument.CaretEnumerable ) {
                        _rgCaretsToReset.Add( oCaret );
                    }

                    _rgLines.Clear();

                    //_oDocument.Raise_ChangesFinished();
                    _oDocument.SetDirty();
                } catch( NullReferenceException ) {
                    _oDocument.LogError( "Error in DeleteAll" );
                }
            }
        } // end class
    }
} // end namespace
