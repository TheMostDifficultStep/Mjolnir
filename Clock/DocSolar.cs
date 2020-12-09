using System;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Collections.Generic;

using Play.Interfaces.Embedding; 
using Play.Edit;
using Play.Integration;
using Play.Parse.Impl;
using Play.ImageViewer;

namespace Play.Clock
{
	public class SolarDoc:
		IPgParent,
		IPgLoad<TextReader>,
		IPgSave<TextWriter>,
        IDisposable
	{
        protected class MorseDocSlot :
			IPgBaseSite
		{
			protected readonly SolarDoc _oHost;

			public MorseDocSlot( SolarDoc oHost ) {
				_oHost = oHost ?? throw new ArgumentNullException();
			}

			public IPgParent Host => _oHost;

			public void LogError(string strMessage, string strDetails, bool fShow=true) {
				_oHost.LogError( strMessage, strDetails, fShow );
			}

			public void Notify( ShellNotify eEvent ) {
			}
		}

		readonly IPgBaseSite _oSiteBase;

        public ImageSoloDoc SolarMap { get; }
        public ImageSoloDoc SolarVhf { get; }

		protected static readonly HttpClient _oHttpClient = new HttpClient(); 

        /// <summary>
        /// Document object for a little Morse Practice document.
        /// </summary>
        public SolarDoc( IPgBaseSite oSiteBase ) {
			_oSiteBase  = oSiteBase ?? throw new ArgumentNullException();

            SolarMap = new ImageSoloDoc( new MorseDocSlot( this ) );
            SolarVhf = new ImageSoloDoc( new MorseDocSlot( this ) );
        }

        private bool _fDisposed = false;

		public void Dispose() {
			if( !_fDisposed ) {
                SolarMap.Dispose();
                SolarVhf.Dispose();

                _fDisposed = true;
			}
		}

		protected void LogError( string strMessage, string strDetails, bool fShow = false ) {
			_oSiteBase.LogError( strMessage, strDetails, fShow );
		}

		public bool      IsDirty   => false; // TODO:  Update this if/when we can edit the source.
		public IPgParent Parentage => _oSiteBase.Host;
		public IPgParent Services  => Parentage.Services;

        public ICollection<ICollection<Line>> Rows { get; } = new List<ICollection<Line>>();

        public async void LoadSolar() {
            using Stream oStreamVhf = await _oHttpClient.GetStreamAsync( @"http://www.hamqsl.com/solar101vhf.php" );
            using Stream oStreamMap = await _oHttpClient.GetStreamAsync( @"http://www.hamqsl.com/solarmap.php" );

            SolarVhf.Load( oStreamVhf );
            SolarMap.Load( oStreamMap );
        }

        /// <summary>
        /// Both InitNew and Load call this base initialization function.
        /// </summary>
		protected bool Initialize() {
            if( !SolarMap.InitNew() )
                return false;
            if( !SolarVhf.InitNew() )
                return false;

            for( int i=0; i<3; ++i ) {
                List<Line> rgRow = new List<Line>(7);

                for( int j=0; j<7; ++j ) {
                    rgRow.Add( new TextLine( j, j.ToString() ) );
                }
                Rows.Add( rgRow );
            }

			return true;
		}

        public bool InitNew() {
			if( !Initialize() ) 
				return false;

			return true;
		}

		public bool Load(TextReader oStream) {
			if( !Initialize() ) 
				return false;

			return true;
		}

		public bool Save(TextWriter oStream) {
            return true;
		}
    }
}
