using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using System.Drawing;
using System.Diagnostics;

using SkiaSharp;

using Play.Parse;
using Play.Interfaces.Embedding;

namespace Play.Edit {
    /// <summary>
    /// FTCacheException is thrown as a generic exception for errors in FTCacheLine's.
    /// </summary>
    [global::System.Serializable]
    public class FTCacheException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public FTCacheException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public FTCacheException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the class with a specified error
        /// message and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="inner">The exception that is the cause of the current exception, or a null reference (Nothing in Visual Basic) if no inner exception is specified.</param>
        public FTCacheException(string message, Exception inner)
            : base(message, inner)
        {
        }

        /// <summary>
        /// Initializes a new instance of the class with serialized data.
        /// </summary>
        /// <param name="info">The System.Runtime.Serialization.SerializationInfo that holds the serialized object data about the exception being thrown.</param>
        /// <param name="context">The System.Runtime.Serialization.StreamingContext that contains contextual information about the source or destination.</param>
        /// <exception cref="System.ArgumentNullException">The info parameter is null.</exception>
        /// <exception cref="System.Runtime.Serialization.SerializationException">The class name is null or System.Exception.HResult is zero (0).</exception>
        protected FTCacheException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
            : base(info, context)
        {
        }
    }

    public struct MemoryRange {
        public int Offset { get; set; }
        public int Length { get; set; } 
    }
	
    /// <summary>
    /// Right now I'm hoping that my glyph stream can be converted to a UTF-8 stream
    /// by knowing the codepoint. Else I'll need to save both the UTF-8 stream pos
    /// and the Glyph stream pos.
    /// </summary>
    public class PgCluster : IEnumerable<int> {
        public PgGlyphPos Coordinates;
        public float        AdvanceOffs { get => Coordinates.advance_x; 
                                          set { Coordinates.advance_x = (short)value; } }
        public float        AdvanceLeft { get; set; }

        public int ColorIndex { get; set; }

        public MemoryRange Glyphs; // This points to the glyphs array start/length of the glyphs in this cluster.
        public MemoryRange Source; // 

        public int  Segment       { get; set; }
        public bool IsVisible     { get; set; } = true; 
        public bool IsPunctuation { get; set; } = false;

        public override string ToString() {
            return Coordinates.ToString();
        }

        public PgCluster( int iGlyphIndex ) {
            Coordinates  = new PgGlyphPos();

            Glyphs.Offset = iGlyphIndex;
            Glyphs.Length = 0;
            Source.Offset = 0;
            Source.Length = 0;

            ColorIndex   = 0;
            Segment      = 0;
        }

        /// <summary>
        /// Enumerate the glyphs that make up this cluster.
        /// </summary>
        public IEnumerator<int> GetEnumerator() {
            for( int i = Glyphs.Offset; i < Glyphs.Offset + Glyphs.Length; ++i ) {
                yield return i;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public float Increment( float fAdvance, int iSegment ) {
            Segment     = iSegment;
            AdvanceLeft = fAdvance;

            return fAdvance + AdvanceOffs;
        }
    }

    public class FTCacheLine : 
		IEnumerable<IColorRange>
    {
        public enum Align {
            Left,
            Center,
            Right
        }

        public         Line Line      { get; }
        public         int  At        { get { return Line.At; } }
        public         int  Top       { get; set; }
        public virtual int  Height    { get { return LineHeight; } }
        public         bool IsInvalid { get; protected set; } = true;
        public         int  LineHeight{ protected set; get; }
        public         int  FontHeight{ protected set; get; }
        public       Align  Justify   { set; get; } = Align.Left;

        protected const int InvisibleEOL = 0; // use this if I put the "<" at the end of selected lines.
                                              // this marks places where I used to fix up for that.

        protected readonly List<IPgGlyph>  _rgGlyphs     = new List<IPgGlyph >(100); // Glyphs that construct characters.
        protected readonly List<PgCluster> _rgClusters   = new List<PgCluster>(100); // Single unit representing a character.
        protected readonly List<int>       _rgClusterMap = new List<int      >(100); // Cluster map from UTF to Cluster.

        public List<IPgWordRange> Words { get; } = new List<IPgWordRange>();

        public FTCacheLine( Line oLine ) {
            Line = oLine ?? throw new ArgumentNullException();
        }

        /// <summary>
        /// This is for the debugger. Never use this for line output.
        /// </summary>
        public override string ToString() {
            StringBuilder sbBuild = new StringBuilder();

            sbBuild.Append( "T:" );
            sbBuild.Append( Top );
            sbBuild.Append( "B:" );
            sbBuild.Append( Bottom );
            sbBuild.Append( "@" );
            sbBuild.Append( Line.At.ToString() );
            sbBuild.Append( "=" );
            sbBuild.Append( Line.ToString(), 0, Line.ElementCount > 50 ? 50 : Line.ElementCount );

            return( sbBuild.ToString() );
        }

        public void SetEdge( CACHEEDGE eSide, int iValue ) {
            switch( eSide ) {
                case CACHEEDGE.TOP:
                    this.Top = iValue;
                    break;
                case CACHEEDGE.BOTTOM:
                    this.Bottom = iValue;
                    break;
            }
        }

        public int Bottom {
            get { return Top + Height; }
            set { Top = value - Height; }
        }

        /// <summary>
        /// Total width of the line UNWRAPPED. 
        /// BUG: Let's cache the max size at some point.
        /// </summary>
        public float UnwrappedWidth { 
            get { 
                if( _rgClusters.Count > 0 ) {
                    PgCluster oTop = _rgClusters[_rgClusters.Count - 1];
                    return oTop.AdvanceLeft + oTop.AdvanceOffs;
                }
                
                return 0 ;
            }
        }

        public PgCluster ClusterAt( int iSourceOffset ) {
            try {
                if( _rgClusters.Count == 0 )
                    return null;

                return _rgClusters[_rgClusterMap[iSourceOffset]];
            } catch( ArgumentOutOfRangeException ) {
                return null;
            }
        }

        /// <exception cref="ArgumentNullException" />
        public IEnumerator<IPgGlyph> ClusterCharacters( PgCluster oCluster ) {
            if( oCluster == null )
                throw new ArgumentNullException();

            for( int i=oCluster.Glyphs.Offset; i<oCluster.Glyphs.Offset + oCluster.Glyphs.Length; ++i ) {
                yield return _rgGlyphs[i];
            }
        }

        /// <summary>
        /// Is the point location vertically within our element? Anywhere on the left or
        /// right of the valid vertical position will return true.
        /// </summary>
        /// <param name="pntLocation">Test, value in world coordinates.</param>
        /// <returns>true if the point is with our cached element location.</returns>
        public bool IsHit( Point pntLocation ) {
            return pntLocation.Y >= Top && pntLocation.Y < Bottom;
        }

        /// <summary>
        /// A dummy enumerator that identifies all the text as a single black run.
        /// </summary>
		public IEnumerator<IColorRange> EnumBlack() {
			yield return( new ColorRange( 0, Line.ElementCount, 0 ) );
		}

        public void Invalidate() {
            IsInvalid = true;
        }

        // TODO: Move this to a static helper class.
        // https://stackoverflow.com/questions/23919515/how-to-convert-from-utf-16-to-utf-32-on-linux-with-std-library
        static UInt32 ReadCodepointFrom( CharStream oStream ) 
        {
          //bool is_surrogate(char uc) { return (uc - 0xd800) < 0x2048; }
            bool is_hi_surrogate(char uc) { return (uc & 0xfffffc00) == 0xd800; }
            bool is_lo_surrogate(char uc) { return (uc & 0xfffffc00) == 0xdc00; }

            UInt32 surrogate_to_utf32(char high, char low) { 
                return (UInt32)(high << 10) + low - 0x35fdc00; 
            }
        
            while( oStream.InBounds( oStream.Position ) ) {
                char uc = oStream.Read();
                if (!char.IsSurrogate( uc ) ) {
                    return uc; 
                } else {
                    if (is_hi_surrogate(uc) ) {
                        char lc = oStream.Read();
                        if( is_lo_surrogate(lc) ) {
                            return surrogate_to_utf32(uc, lc);
                        }
                    } else {
                        // this is why we don't want to allow editing inside clusters!!
                        throw new FTCacheException( "Codepoint from UTF-16" );
                    }
                }
            }
            return 0;
        }

        /// <summary>
        /// Still be needed for selection. Which makes sense as the carat
        /// is always to the left of the current cluster. But make sure
        /// the width is zero, that way it won't affect any line width measurements!
        /// </summary>
        /// <param name="oFR"></param>
        protected void Update_EndOfLine( IPgFontRender oFR ) {
            PgCluster oCluster = new PgCluster(_rgGlyphs.Count);
            IPgGlyph  oGlyph   = oFR.GetGlyph(0x20);

          //oCluster.Coordinates    = oGlyph.Coordinates; DO NOT SET!
            oCluster.IsVisible      = false;
            oCluster.Glyphs.Length  = 1;

            _rgClusters.Add( oCluster );
            _rgGlyphs  .Add( oGlyph ); // use space glyph, but codepoint LF.
        }

        /// <summary>
        /// Generate a map from UTF-16 indicies to Cluster offsets. Use the cluster
        /// map to index parser color info back to the cluster. This is the standard 
        /// cluster handling that always comes up with unicode. This routine gives
        /// us a way to map from the source back to our cluster. 
        /// stream. 
        ///               0 1 2 3 4
        /// ClusterMap :  0 0 0 1 1
        /// Utf Stream :  5 2 8 3 2 
        /// ... cluster 0 has 3 elements, cluster 1 has 2 elements.
        /// </summary>
        protected void Update_ClusterMap() {
            _rgClusterMap.Clear();

            int iSrcOffset = 0;
            for( int i = 0; i<_rgClusters.Count; ++i ) {
                PgCluster oCluster = _rgClusters[i];

                oCluster.Source.Offset = iSrcOffset; // _rgClusterMap.Count; 
                for( int j = oCluster.Glyphs.Offset; j < oCluster.Glyphs.Offset + oCluster.Glyphs.Length; ++j ) {
                    iSrcOffset += _rgGlyphs[j].CodeLength;
                    for( int k = 0; k < _rgGlyphs[j].CodeLength; ++k ) {
                        _rgClusterMap.Add( i );
                    }
                    oCluster.Source.Length += (int)_rgGlyphs[j].CodeLength;
                }
            }
        }

        protected void LoadCodePoints( IPgFontRender oFR ) {
            _rgGlyphs.Clear();

            CharStream oStream = new CharStream( Line );
            while( oStream.InBounds( oStream.Position ) ) {
                int      iOffs  = oStream.Position;
                uint     uiCode = ReadCodepointFrom( oStream );
                int      iLen   = oStream.Position - iOffs;
                IPgGlyph oGlyph;
                
                oGlyph = oFR.GetGlyph(uiCode);

                oGlyph.CodeLength = iLen; // In the future we'll set it in the font manager. (both 16&32 values)

                _rgGlyphs.Add( oGlyph );
            }
        }

        /// <summary>
        /// Right now I'm just hacking a simple shaper. Normally I'd get a cluster list
        /// from a shaper. But I'm faking it for fun. Right now the big challenge is this
        /// chara...👩‍⚕️ which is 5 utf-16 units and 13 utf-8 units!
        /// Here's the UTF-16...
        /// 0xd83d &
        /// 0xdc69 Woman (cp 0x1f469)
        /// 0x200d zero width joiner. (cp 0x200d)
        /// 0x2695 staff of aesculapius. (cp 0x2695)
        /// 0xfe0f variation selector-16 (cp 0xfe0f) emoji with color.
        /// </summary>
        /// <remarks>Do NOT set the cluster AdvanceLeft, that will be set in WrapSegments() </remarks>
        /// <seealso cref="FTCacheWrap.WrapSegments"/>
        public void Update( IPgFontRender oFR ) {
            if( oFR == null )
                throw new ArgumentNullException();

            FontHeight = (int)oFR.FontHeight;
            LineHeight = (int)(FontHeight * 1.2 ); // Make this a intra line property in the future.
            LoadCodePoints( oFR );

            _rgClusters.Clear();
            PgCluster oCluster;
            int       iGlyphIndex = 0; // Keep track of where we are in our Glyphs.

            try {
                while( iGlyphIndex < _rgGlyphs.Count ) {
                    oCluster = new PgCluster( iGlyphIndex );
                    _rgClusters.Add( oCluster );

                    oCluster.Glyphs.Length++; 
                    oCluster.Coordinates   = _rgGlyphs[iGlyphIndex].Coordinates;
                    oCluster.IsVisible     = !Rune.IsWhiteSpace( (Rune)_rgGlyphs[iGlyphIndex].CodePoint );
                    oCluster.IsPunctuation = Rune.IsPunctuation( (Rune)_rgGlyphs[iGlyphIndex].CodePoint );

                    if( ++iGlyphIndex >= _rgGlyphs.Count )
                        break;

                    if( _rgGlyphs[iGlyphIndex].CodePoint == 0x200d ) {
                        oCluster.Glyphs.Length++;

                        if( ++iGlyphIndex >= _rgGlyphs.Count )
                            break;

                        // Simply eat the alternate character. for now.
                        oCluster.Glyphs.Length++;

                        if( ++iGlyphIndex >= _rgGlyphs.Count )
                            break;

                        if( _rgGlyphs[iGlyphIndex].CodePoint >= 0xfe00 &&
                            _rgGlyphs[iGlyphIndex].CodePoint <= 0xfe0f ) {
                            oCluster.Glyphs.Length++;

                            if( ++iGlyphIndex >= _rgGlyphs.Count )
                                break;
                        }
                    }
                }

                Update_EndOfLine ( oFR );
                Update_ClusterMap();
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( NullReferenceException ),
                                    typeof( IndexOutOfRangeException ),
                                    typeof( ArgumentOutOfRangeException ),
                                    typeof( FTCacheException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
            }

            IsInvalid = false;
        } // end method

        /// <summary>
        /// In the no word wrap case. Only the text visible in the first
        /// logical line that fits in the display width will show.
        /// Call after the Update time, and is not needed for resize.
        /// </summary>
        /// <remarks>We don't need to set the last EOL character since when
        /// enumerating the clusters we get it unlike when we use the
        /// parser.</remarks>
        /// <param name="iDisplayWidth"></param>
        /// <seealso cref="Update"/>
        /// <seealso cref="OnChangeSize"/>
        /// <seealso cref="FTCacheWrap.WrapSegmentNoWords(int)"/>
        public virtual void WrapSegments( int iDisplayWidth ) {
            float flAdvance  = 0;
            int   iWrapCount = 0;
            Span<float> rgStart = stackalloc float[10];

            if( _rgClusters.Count > 0 ) {
                flAdvance = _rgClusters[0].Increment( flAdvance, iWrapCount );
            }

            for( int iCluster = 1; iCluster < _rgClusters.Count; ++iCluster ) {
                flAdvance = _rgClusters[iCluster].Increment(flAdvance, iWrapCount);
            }
        }

        /// <summary>
        /// Since we don't word wrap, just use max int for the width.
        /// </summary>
        /// <param name="iDisplayWidth">Width in "pixels" of the view</param>
        public virtual void OnChangeSize( int iWidth ) {
            WrapSegments( iWidth );
        }

        /// <summary>
        /// Apply the line formatting and selection color to the clusters.
        /// </summary>
        public virtual void OnChangeFormatting( ICollection<ILineSelection> rgSelections ) {
            ClusterColorClear();

            // Only grab the color ranges, States can have a color set but then the
            // subsequent terminals are all black and we blast our color set.
			foreach( IColorRange oColor in Line.Formatting ) {
                if( oColor.ColorIndex > 0 ) {
                    ClusterColorSet = oColor;
                }
			}

            // Selection overrides what ever colors we had set.
            if (rgSelections != null) {
                foreach (ILineSelection oSelect in rgSelections) {
                    if (oSelect.IsHit(Line)) {
                        ClusterColorSet = oSelect;
                    }
                }
            }
        } // end method

        /// <summary>
        /// Set the color of the cluster of glyphs.
        /// </summary>
        /// <remarks>Double check that the index is less than the cluster count. We often have
        /// an parse element that is int max or something when we don't have the line parsed.</remarks>
        protected IColorRange ClusterColorSet {
            set {
                try {
                    for( int i = value.Offset; 
                         i < value.Offset + value.Length && i < _rgClusterMap.Count; 
                        ++i ) 
                    {
                        _rgClusters[_rgClusterMap[i]].ColorIndex = value.ColorIndex;
                    }
                } catch( Exception oEx ) {
                    Type[] rgErrors = { typeof( ArgumentOutOfRangeException ),
                                        typeof( NullReferenceException ) };
                    if( rgErrors.IsUnhandled( oEx ) ) 
                        throw;
                }
            }
        }

        protected void ClusterColorClear() {
            foreach( PgCluster oCluster in _rgClusters ) {
                oCluster.ColorIndex = 0;
            }
        }

        /// <summary>
        /// Call this routine with the skPaint parameter color set to the color you wish
        /// for the glyph being drawn.
        /// </summary>
        /// <remarks>Set the Alpha value to "00" for BG and "FF" for full color. In SKIA, 
        /// An alpha value of 0x00 is fully transparent and 
        /// an alpha value of 0xFF is fully opaque.
        /// </remarks>
        public virtual void DrawGlyph( 
            SKCanvas      skCanvas, 
            SKPaint       skPaint,
            float         flX, 
            float         flY, 
            IPgGlyph      oGlyph
        ) {
            SKRect skRect = new SKRect( flX, flY, 
                                        flX + oGlyph.Image.Width, 
                                        flY + oGlyph.Image.Height );
            // So XOR only works with alpha, which explains why my
            // Alpha8 bitmap works with this.
            skPaint .BlendMode = SKBlendMode.Xor;
            skCanvas.DrawBitmap(oGlyph.Image, flX, flY, skPaint);

            // So the BG is already the color we wanted, it get's XOR'd and
            // has a transparency set, then we draw our text colored rect...
            skPaint .BlendMode = SKBlendMode.DstOver;
            skCanvas.DrawRect(skRect, skPaint);
        }

        /// <summary>
        /// Draw's the glyph's background with the color set on the Paint object.
        /// </summary>
        protected void DrawGlyphBack( 
            SKCanvas      skCanvas,
            SKPaint       skPaint,
            float         flX,
            float         flY,
            PgCluster     oCluster )
        {
            SKRect skRect = new SKRect( flX, flY - LineHeight, // Wrestling with this. LineHeight or FontHeight isn't quite right.
                                        flX + ( oCluster.AdvanceOffs ), 
                                        flY );

            skPaint .BlendMode = SKBlendMode.Src;
            skCanvas.DrawRect(skRect, skPaint);
        }

        /// <summary>
        /// A cluster is a set of glyphs that make up a character. All of the 
        /// glyphs are in the _rgGlyphs array and the cluster has an offset
        /// into that array, and a length of how many to traverse to build that
        /// one character.
        /// </summary>
        /// <remarks>At present we don't support more than one glyph in a cluster.
        /// I'll probably want to change that when rendering emoji's, but now it
        /// seems to work even for Japanese.</remarks>
        public virtual void Render(
            SKCanvas       skCanvas,
            IPgStandardUI2 oStdUI,
            PointF         pntEditAt,
            bool           fFocused = true )
        {
            if( _rgGlyphs.Count <= 0 )
                return;

            // Our new system paints UP from the baseline. It's nice because it's a bit more how
            // we think of text but weird since the screen origin is top left and we print successive
            // lines down. >_<;;
            SKPoint pntLowerLeft = new SKPoint( pntEditAt.X, pntEditAt.Y + FontHeight );

            using( SKPaint skPaint = new SKPaint() ) {
                skPaint.FilterQuality = SKFilterQuality.High;

                try { // Draw all glyphs so whitespace is properly colored when selected.
			        foreach( PgCluster oCluster in _rgClusters ) {
                        int iYDiff = LineHeight * oCluster.Segment;

                        float flX = pntLowerLeft.X + oCluster.AdvanceLeft; 
                        float flY = pntLowerLeft.Y + iYDiff - oCluster.Coordinates.top;

                        // Only draw if we need to override the last painted bg color.
                        if( oCluster.ColorIndex < 0 ) {
                            skPaint.Color = oStdUI.ColorsStandardAt( fFocused ? StdUIColors.BGSelectedFocus : StdUIColors.BGSelectedBlur );
                            DrawGlyphBack( skCanvas, skPaint, flX, pntEditAt.Y + LineHeight + iYDiff, oCluster );
                        }

                        // Negative numbers can be hyper links too, need to work that out. 
                        skPaint.Color = oCluster.ColorIndex < 0 ? oStdUI.ColorsStandardAt( StdUIColors.TextSelected ) : 
                                                                  oStdUI.ColorsText[ oCluster.ColorIndex ];

                        //foreach( int iGlyph in oCluster ) {
                            DrawGlyph( skCanvas, skPaint, flX, flY, _rgGlyphs[oCluster.Glyphs.Offset] );
                        //}
                    } // end foreach
                } catch( Exception oEx ) {
                    Type[] rgErrors = { typeof( ArgumentOutOfRangeException ),
                                        typeof( NullReferenceException ),
                                        typeof( ArithmeticException ) };
                    if( rgErrors.IsUnhandled( oEx ) )
                        throw;

                    // Would love to report an error. But this is pretty rare. Let's just put up a red flag!!
                    // TODO: I should make a new exception type. 
                    skPaint .Color = Line.At > 0 ? SKColors.Red : SKColors.Lime;
                    skCanvas.DrawRect(new SKRect(pntLowerLeft.X, pntEditAt.Y,
                                                 pntLowerLeft.X + 100, pntEditAt.Y + Height), skPaint);
                    Debug.Fail( "Exception thrown in FTCacheLine.Render" );
                }
            }
        } // end method

        /// <summary>
        /// This is a more general renderer where you can supply the paint object.
        /// I expect to use this one more for rendering in paint programs versus
        /// writing programs which the other render function is better suited for.
        /// </summary>
        public virtual void Render(
            SKCanvas skCanvas,
            SKPaint  skPaint,
            PointF   pntEditAt )
        {
            if( _rgGlyphs.Count <= 0 )
                return;

            // Our new system paints UP from the baseline. It's nice because it's a bit more how
            // we think of text but weird since the screen origin is top left and we print successive
            // lines down. >_<;;
            SKPoint pntLowerLeft = new SKPoint( pntEditAt.X, pntEditAt.Y + FontHeight );

            skPaint.FilterQuality = SKFilterQuality.High;

            try { // Draw all glyphs so whitespace is properly colored when selected.
                //skCanvas.DrawLine( pntLowerLeft, new SKPoint( pntLowerLeft.X + 300, pntLowerLeft.Y ), skPaint );
			    foreach( PgCluster oCluster in _rgClusters ) {
                    int iYDiff = LineHeight * oCluster.Segment;

                    float flX = pntLowerLeft.X + oCluster.AdvanceLeft; 
                    float flY = pntLowerLeft.Y + iYDiff - oCluster.Coordinates.top;

                    // Only draw if we need to override the last painted bg color.
                    if( oCluster.ColorIndex < 0 ) {
                        DrawGlyphBack( skCanvas, skPaint, flX, pntEditAt.Y + LineHeight + iYDiff, oCluster );
                    }

                    // Only draw first element of cluster. Haven't seen multi elem cluster yet.
                    DrawGlyph( skCanvas, skPaint, flX, flY, _rgGlyphs[oCluster.Glyphs.Offset] );
                } // end foreach
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentOutOfRangeException ),
                                    typeof( NullReferenceException ),
                                    typeof( ArithmeticException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                Debug.Fail( "Exception thrown in FTCacheLine.Render" );
            }
        } // end method

        /// <summary>Render the End Of Line character.</summary>
        /// <param name="pntEditAt">Top left of the cache element on screen.</param>
        /// <remarks>Need to look at why we pass the pntEditAt. Why not just use our own 'left' / 'top'</remarks>
        public void RenderEOL( SKCanvas skCanvas, SKPaint skPaint, PointF pntEditAt,  List<SKColor> rgStdColors, IPgGlyph oGlyphLessThan )
        {
            Point pntOffset = GlyphOffsetToPoint( _rgClusters.Count );

            DrawGlyph( skCanvas, skPaint,
                       (Int32)( pntEditAt.X + pntOffset.X ),
                       (Int32)( pntEditAt.Y + pntOffset.Y ),
                       oGlyphLessThan );
        } // end method

        /// <summary>
        /// Render the little underline's to show where an error has occured.
        /// </summary>
        /// <param name="hDC">handle to a display context.</param>
        /// <param name="pntEditAt">Top left of the cache element on screen.</param>
        public void RenderLinks( SKCanvas skCanvas, SKPaint skPaint, PointF pntEditAt ) 
        {
			try {
				using( IEnumerator<IColorRange> oEnum = GetEnumerator() ) {
					while( oEnum.MoveNext() ) {
						IColorRange oElem = oEnum.Current;
						if( oElem.ColorIndex == -4 ) {
							Point pntElemOffset = GlyphOffsetToPoint( oElem.Offset );
                            Point pntElemLength = GlyphOffsetToPoint( oElem.Offset + oElem.Length );
                            SKPoint skPnt0 = new SKPoint( pntEditAt.X + pntElemOffset.X, 
                                                          pntEditAt.Y + pntElemOffset.Y + Height - 1 );
                            SKPoint skPnt1 = new SKPoint( pntEditAt.X + pntElemLength.X, 
                                                          pntEditAt.Y + pntElemLength.Y + Height - 1 );

                            skCanvas.DrawLine( skPnt0, skPnt1, skPaint );
						}
					}
				}
			} catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentOutOfRangeException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                Debug.Fail( "Exception in FTCacheLine.RenderLinks" );
			}
        } // end method

        /// <summary>
        /// Find the character offset of the mouse position. TODO: This is a crude
        /// re-factoring for the word wrap version. I can do a better job. See
        /// GlyphPointToOffset2() for the beginnings of a replacement.
        /// </summary>
        /// <returns>Character offset of the given location. 0 if left of the first element.</returns>
        public virtual int GlyphPointToOffset( SKPointI pntWorld ) {
            if( _rgClusters.Count < 1 )
                return 0;

            try {
                float flWorldXEm = pntWorld.X;
                // -1 if the sought elem precedes the elem specified by the CompareTo method.
                int ClusterCompare( PgCluster oTry ) {
                    if( flWorldXEm < oTry.AdvanceLeft)
                        return -1;
                    if( flWorldXEm >= oTry.AdvanceLeft + oTry.AdvanceOffs / 2 ) // divide by 2.
                        return 1;
                    return 0;
                }
                int iIndex = FindStuff<PgCluster>.BinarySearch( _rgClusters, 0, _rgClusters.Count - 1, ClusterCompare );
            
                // We never miss since our world point is always within the world.
                if( iIndex < 0 )
                    iIndex = ~iIndex; // But if miss, this element is on the closest edge.

                return iIndex;
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentOutOfRangeException ),
                                    typeof( IndexOutOfRangeException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                return 0;
            }
        }

        /// <summary>
        /// Return the position of the glyph relative to an upper left position
        /// that would be 0, 0 for this cache element.
        /// </summary>
        /// <param name="iOffset"></param>
        /// <remarks>I'd like to throw if out of bounds. But we've got too many
        /// places to fix up. So Just return a safe value on error.</remarks>
        public virtual Point GlyphOffsetToPoint( int iOffset )
        {
            try {
                if( _rgClusters.Count <= 0 || _rgClusterMap.Count < 1 )
                    return new Point( 0, 0 );

                if( iOffset > _rgClusterMap.Count - 1 )
                    iOffset = _rgClusterMap.Count - 1;
                if( iOffset < 0 )
                    iOffset = 0;

                return new Point( (int)_rgClusters[_rgClusterMap[iOffset]].AdvanceLeft, 0 );
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentOutOfRangeException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                return new Point( 0, 0 );
            }
        } // end method

        /// <summary>
        /// Move left or right on this line.
        /// </summary>
        /// <param name="iDir">+/- number of glyphs to move.</param>
        /// <param name="flAdvance">Current graphics position on the line.</param>
        /// <param name="iOffset">Current logical position on the line.</param>
        /// <returns>True if able to move. False if positioning will move out of bounds.</returns>
        /// <remarks>TODO: In the future we must use the cluster info.</remarks>
        protected virtual bool NavigateHorizontal( int iDir, ref float flAdvance, ref int iOffset ) {
            try {
                int iNextCluster = _rgClusterMap[iOffset] + iDir;
                
                if( iNextCluster > -1 && iNextCluster < _rgClusters.Count ) {
                    PgCluster oNewCluster = _rgClusters[iNextCluster];

                    iOffset  = oNewCluster.Source.Offset;
                    flAdvance = oNewCluster.AdvanceLeft;
                    return( true );
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentOutOfRangeException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
            }

            return( false );
        }

        /// <summary>
        /// Move up or down based on the previous advance. For a non-wrapped line it always fails
        /// to move internally.
        /// </summary>
        /// <param name="iIncrement">Direction of travel, positive is down, negative is up.</param>
        /// <param name="iAdvance">Previous "pixel" offset on a given line. The wrapped value.</param>
        /// <param name="iOffset">Closest character we can find to the given offset on a line above or below.</param>
        /// <returns>false, always since one cannot navigate vertically on a non-wrapped line.</returns>
        protected virtual bool NavigateVertical( int iDir, float flAdvance, ref int iOffset ) {
            return( false );
        }

        public bool Navigate( Axis eAxis, int iDir, ref float flAdvance, ref int iOffset ) {
            // See if we can navigate within the line we are currently at.
            switch( eAxis ) {
                case Axis.Horizontal:
                    return( NavigateHorizontal( iDir, ref flAdvance, ref iOffset ) );
                case Axis.Vertical:
                    return( NavigateVertical( iDir, flAdvance, ref iOffset ) );
            }

            throw new ArgumentOutOfRangeException( "expecting only horizontal or vertical" );
        }
        /// <summary>
        /// Get the minimum or maximum glyph position available on this line.
        /// </summary>
        /// <param name="iIncrement">0 or positive returns MAX, negative returns MIN.</param>
        /// <returns></returns>
        /// <remarks>This depends on how we set up the EOL marker!!</remarks>
        protected int OffsetHorizontalBound( int iIncrement ) {
            const int iMin = 1 + InvisibleEOL;
            try {
                if( iIncrement >= 0 && _rgClusters.Count > iMin ) {
                    return(  _rgClusters[_rgClusters.Count-iMin].Source.Offset ); 
                }
            }  catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentOutOfRangeException ),
                                    typeof( NullReferenceException ),
                                    typeof( IndexOutOfRangeException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;

                // Argh, I'd like to log an error but no back pointer to the shell.
            }

            return( 0 );
        }
        
        /// <summary>
        /// Find the nearest glyph offset to the given advance at the current line.
        /// </summary>
        /// <param name="iIncrement">Ignore the line wrap directive</param>
        /// <param name="flAdvance">Now many pixels to the right starting from left.</param>
        /// <remarks>7/13/2020, in the new implementation, it looks like we're a little off
        /// but it seems to be related to spaces. Need to check their width.</remarks>
        protected virtual int OffsetVerticalBound( int iIncrement, float flAdvance ) {
            int i = _rgClusters.Count - 1;

            try {
                while( i > 0 && ( _rgClusters[i].AdvanceLeft ) > flAdvance ) {
                    --i;
                }
            } catch( Exception oEx ) {
                Type[] rgErrors = { typeof( ArgumentOutOfRangeException ),
                                    typeof( NullReferenceException ) };
                if( rgErrors.IsUnhandled( oEx ) )
                    throw;
                Debug.Fail( "Trouble in FTCacheLine.OffsetVerticalBound." );

                return 0;
            }

            return( i );
        }

        /// <summary>
        /// When moving from a different cache element this function returns the
        /// offset position for the carat on the new element.
        /// </summary>
        /// <returns>Offset position.</returns>
        public int OffsetBound( Axis eAxis, int iIncrement, float flAdvance ) {
            switch( eAxis ) {
                case Axis.Horizontal:
                    return OffsetHorizontalBound( iIncrement );
                case Axis.Vertical:
                    return OffsetVerticalBound( iIncrement, flAdvance );
            }
            throw new ArgumentOutOfRangeException( "Not horizontal or vertical axis" );
        }

		/// <summary>
		/// Source of all color information for this cache element. Kewl 'eh?
		/// </summary>
		/// <remarks>If we don't parse, then Color contains selection information ONLY when there
		/// is a selection. Then we get only the selection colored. If there is no selection
		/// Words and Formatting are more primative (parse data only) and never contain selection.
		/// We correct this problem in OnChangeFormatting().
		/// </remarks>
		/// <seealso cref="OnChangeFormatting"/>
		public IEnumerator<IColorRange> GetEnumerator() {
			if( Line.Formatting.Count > 0 ) {
				foreach( IColorRange oRange in Line.Formatting ) {
                    if( oRange is IPgWordRange oWord && oWord.IsTerm )
                        yield return oWord;
                }
                yield break;
            }
			yield return( new WordRange( 0, Line.ElementCount, 0 ) );
		}

		IEnumerator IEnumerable.GetEnumerator() {
			return GetEnumerator();
		}
	} // end class
}
