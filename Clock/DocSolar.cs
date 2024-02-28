using System;
using System.IO;
using System.Reflection;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading.Tasks;

using Play.Interfaces.Embedding; 
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

		public bool      IsDirty   => false; // TODO:  Update this if/when we can edit the source.
		public IPgParent Parentage => _oSiteBase.Host;
		public IPgParent Services  => Parentage.Services;

        public ImageSoloDoc SolarMap { get; }
        public ImageSoloDoc SolarVhf { get; }

		protected static readonly HttpClient _oHttpClient = new HttpClient(); 
		protected readonly IPgRoundRobinWork _oWorkPlace; 


        /// <summary>
        /// Document object for a little Morse Practice document.
        /// </summary>
        public SolarDoc( IPgBaseSite oSiteBase ) {
			_oSiteBase  = oSiteBase ?? throw new ArgumentNullException();
            _oWorkPlace = ((IPgScheduler)Services).CreateWorkPlace() ?? throw new InvalidOperationException( "Need the scheduler service in order to work. ^_^;" );

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

		protected static Type[] _rgStdErrors = { 
			typeof( TargetInvocationException ),
			typeof( System.Net.Sockets.SocketException ),
			typeof( NullReferenceException ),
			typeof( HttpRequestException ),
			typeof( InvalidOperationException ),
			typeof( TaskCanceledException ) };


		public IEnumerator<int> GetPageLoader() {
			Task<HttpResponseMessage> tskVHF = null;
			Task<HttpResponseMessage> tskMap = null;

			try {
				tskVHF = _oHttpClient.GetAsync( @"http://www.hamqsl.com/solar101vhf.php" );
				tskMap = _oHttpClient.GetAsync( @"http://www.hamqsl.com/solarmap.php" );
			} catch( Exception oEx ) {
				if( _rgStdErrors.IsUnhandled( oEx ) )
					throw;
				LogError( "Net IO", "Problem handling http request solar101" );
			}
			while( !tskVHF.IsCompleted )
				yield return 100;

			while( !tskMap.IsCompleted )
				yield return 100;

			SolarVhf.Load( tskVHF.Result.Content.ReadAsStream() );
			SolarMap.Load( tskMap.Result.Content.ReadAsStream() );

			tskMap.Dispose();
			tskVHF.Dispose();
		}

        /// <summary>
        /// Both InitNew and Load call this base initialization function.
        /// </summary>
		protected bool Initialize() {
            if( !SolarMap.InitNew() )
                return false;
            if( !SolarVhf.InitNew() )
                return false;

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

			_oWorkPlace.Queue( GetPageLoader(), 1 );

			return true;
		}

		public void LoadSolar() {
			_oWorkPlace.Queue( GetPageLoader(), 0 );
		}

		public bool Save(TextWriter oStream) {
            return true;
		}
    }
}
