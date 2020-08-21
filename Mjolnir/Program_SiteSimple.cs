using System;

using Play.Interfaces.Embedding;

namespace Mjolnir {
    partial class Program {
		class SimpleSlot : IPgBaseSite {
			readonly Program _oHost;

			public SimpleSlot( Program oHost ) {
				_oHost = oHost ?? throw new ArgumentNullException();
			}

			public IPgParent Host => _oHost;

			public void LogError(string strMessage,string strDetails,bool fShow = true) {
				_oHost.LogError( this, strMessage, strDetails, fShow );
			}

			public void Notify(ShellNotify eEvent) {
			}
		}
	}
}
