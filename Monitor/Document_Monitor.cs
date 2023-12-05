using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Play.Interfaces.Embedding;
using Play.Edit; 
using Play.Parse;
using Play.Parse.Impl;

namespace Monitor {
    internal class Document_Monitor :
        IPgParent,
		IDisposable,
        IPgLoad<TextReader>,
        IPgSave<TextWriter>
    {
        protected readonly IPgBaseSite _oBaseSite;
        public IPgParent Parentage => _oBaseSite.Host;
        public IPgParent Services  => Parentage.Services;

        public Editor Doc_Asm   { get; }
        public Editor Doc_Displ { get; }


        public bool IsDirty => Doc_Asm.IsDirty;

        public class DocSlot :
            IPgBaseSite
        {
            protected readonly Document_Monitor _oHost;

            public DocSlot( Document_Monitor oHost ) {
                _oHost = oHost;
            }
            public IPgParent Host => _oHost;

            public void LogError(string strMessage, string strDetails, bool fShow = true) {
                _oHost._oBaseSite.LogError(strMessage, strDetails, fShow);
            }

            public void Notify(ShellNotify eEvent) {
                _oHost._oBaseSite.Notify( eEvent );
            }
        }

        public Document_Monitor( IPgBaseSite oBaseSite ) {
            _oBaseSite = oBaseSite ?? throw new ArgumentNullException();

            Doc_Asm   = new Editor( new DocSlot( this ) );
            Doc_Displ = new Editor( new DocSlot( this ) );
        }

        protected void LogError( string strLabel, string strMessage ) {
            _oBaseSite.LogError( strLabel, strMessage );
        }

        public void Dispose() {
        }

        public bool Load(TextReader oStream) {
            if( !Doc_Asm.Load( oStream ) )
                return false;

            if( !Doc_Displ.InitNew() )
                return false;

            return true;
        }

        public bool InitNew() {
            if( !Doc_Asm.InitNew() )
                return false;

            if( !Doc_Displ.InitNew() )
                return false;

            return true;
        }

        public bool Save(TextWriter oStream) {
            return Doc_Asm.Save( oStream );
        }
    }
}
