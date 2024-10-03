using System;
using System.Collections.Generic;
using System.Windows.Forms;

using SkiaSharp.Views.Desktop;

using Play.Rectangles;
using Play.Interfaces.Embedding;
using Play.Sound;
using Play.Edit;

namespace Play.SSTV {
    /// <summary>
    /// This window is like a herder, but no ability to make a title like the
    /// normal Mjolnir herder. But here, all the tools are sort of globbed together.
    /// Let's see how it goes and perhaps I'll break them apart.
    /// </summary>
    /// <remarks>Not using this anymore. It turns out, it is a lot nicer to have
    /// the family/mode and galleries settings simply in the property page window
    /// so you can see them all the time instead of switching between.
    /// UI LESSON! We might bring this back since it'll be great for Photoshop like drawing
    /// commands where we really just need a TOOL at that moment.</remarks>
    public class WindowTxTools : 
        SKControl,
		IPgLoad
    {
        private readonly IPgViewSite        _oViewSite;
        private readonly DocSSTV            _oDocSSTV;

        // This is a list of all the menu layouts, one gets selected
        // to be shown the rest get turned off.
        Dictionary<Guid, ParentRect > _rgFlock = new (); 
        Guid                          _gSelected = Guid.Empty;

        public readonly ComboBox _ddModeSub  = new ComboBox();
        public readonly ComboBox _ddModeMain = new ComboBox();

        public WindowTxTools( IPgViewSite oViewSite, DocSSTV oDocSSTV ) { 
            _oViewSite = oViewSite ?? throw new ArgumentNullException( nameof( oViewSite ) );
            _oDocSSTV  = oDocSSTV  ?? throw new InvalidProgramException( "Can't find document for view" );

            Parent = (Control)_oViewSite.Host;
        }

        public bool InitNew() {
            InitTemplates();
            InitModes    ();

            Execute( TransmitCommands.Resize );

            OnSizeChanged( new EventArgs() );
            //_oDocSSTV.TxModeList.CheckedEvent += OnCheckedEvent_TxModeList;

            return true;
        }

        protected void OptionHideAll() {
            foreach( KeyValuePair<Guid, ParentRect> oPair in _rgFlock ) {
                oPair.Value.Hidden = true;
            }
        }

        /// <summary>
        /// Switch between tool options depending on the selected tool.
        /// </summary>
        public bool Execute( Guid gCommand ) {
            if( _rgFlock.TryGetValue( gCommand, out ParentRect oToolOptions ) ) {
                OptionHideAll();

                _gSelected          = gCommand;
                oToolOptions.Hidden = false;
                OnSizeChanged( new EventArgs() );

                return true;
            }

            return false;
        }

        public SSTVMode CurrentMode {
            get {
                if( _ddModeSub.SelectedItem == null )
                    return _ddModeSub.Items[0] as SSTVMode;

                return _ddModeSub.SelectedItem as SSTVMode;
            }
        }

        private void InitTemplates() {
            LayoutStack oLayout = new LayoutStackHorizontal() { Spacing = 5 };

            oLayout.Add( new LayoutRect( LayoutRect.CSS.None ) );
          //oLayout.Add( new LayoutControl( new ViewTxTemplates( WinSlot( this ), LayoutRect.CSS.Pixels, 200 ) );
            oLayout.Add( new LayoutRect( LayoutRect.CSS.None ) );

            //_rgFlock.Add( TransmitCommands.Templates, oLayout );
            //_gSelected = TransmitCommands.Templates;
        }

        /// <summary>
        /// Set up the dual dropdowns for the SSTV node tool options.
        /// </summary>
        private void InitModes() {
            // This was gutted. Use the new Tx/Rx documents and dropdowns...

            LayoutStack oLayout = new LayoutStackHorizontal() { Spacing = 5 };
            oLayout.Add( new LayoutRect( LayoutRect.CSS.None ) );
            oLayout.Add( new LayoutControl( _ddModeMain, LayoutRect.CSS.Pixels, 150 ) );
            oLayout.Add( new LayoutControl( _ddModeSub,  LayoutRect.CSS.Pixels, 250 ) );
            oLayout.Add( new LayoutRect( LayoutRect.CSS.None ) );

          //_rgFlock.Add( TransmitCommands.Mode, oLayout );
        }

        protected override void OnSizeChanged( EventArgs e ) { 
            _rgFlock[_gSelected].SetRect( 0, 0, Width, Height );
            _rgFlock[_gSelected].LayoutChildren();
        }
    }

    /// <summary>
    /// It's a shame, this is almost a complete copy of the WindowTxTools window. But instead
    /// of using the TxMode list we want the Rxmode list. Tho this window is debugged for
    /// checked line selection in the list updating the dropdowns correctly. At some point
    /// I'll try making a subclassable control for the list selection.
    /// </summary>
    public class WindowFileTools : 
        SKControl,
		IPgLoad
    {
        private readonly IPgViewSite  _oViewSite;
        private readonly DocSSTV      _oDocSSTV;
        private readonly object[]     _rgNullParams = new object[]{};

        Dictionary<Guid, ParentRect > _rgFlock = new (); 
        Guid                          _gSelected = Guid.Empty;

        private readonly ComboBox _ddModeSub  = new ComboBox();
        private readonly ComboBox _ddModeMain = new ComboBox();

        private bool _bProcessCheckModeList = false;

        public WindowFileTools( IPgViewSite oViewSite, DocSSTV oDocSSTV ) { 
            _oViewSite = oViewSite ?? throw new ArgumentNullException( nameof( oViewSite ) );
            _oDocSSTV  = oDocSSTV  ?? throw new InvalidProgramException( "Can't find document for view" );

            Parent = (Control)_oViewSite.Host;
        }

        protected override void Dispose(bool disposing) {
            //_oDocSSTV.RxModeList.CheckedEvent -= OnCheckedEvent_RxModeList;

            base.Dispose(disposing);
        }

        public bool InitNew() {
            InitModes();

            Execute( ReceiveCommands.Mode );

            OnSizeChanged( new EventArgs() );

            return true;
        }

        public void OptionHideAll() {
            foreach( KeyValuePair<Guid, ParentRect> oPair in _rgFlock ) {
                oPair.Value.Hidden = true;
            }
        }

        /// <summary>
        /// Switch between tool options depending on the selected tool.
        /// </summary>
        public bool Execute( Guid gCommand ) {
            if( _rgFlock.TryGetValue( gCommand, out ParentRect oToolOptions ) ) {
                OptionHideAll();

                _gSelected          = gCommand;
                oToolOptions.Hidden = false;
                OnSizeChanged( new EventArgs() );

                return true;
            }

            return false;
        }

        /// <summary>
        /// Populate the main dropdown and then this will fill in all
        /// the sub modes for the selected item. It's got the ability to track
        /// the TxModeList selection but I don't think I'm going to need that
        /// since the way to set Mode selection is via the dual drop downs and 
        /// not a straight TxModeList editor.
        /// </summary>
        private void PopulateSubModes( ComboBox ddModeMain, ComboBox ddModeSub, SSTVMode oSelectMode = null ) {
            if( ddModeMain.SelectedItem is SSTVDEM.SSTVFamily oDesc ) {
                ddModeSub.Items.Clear();

                if( oDesc._typClass.GetMethod( "EnumAllModes" ).Invoke( null, _rgNullParams ) is IEnumerator<SSTVMode> itrSubMode ) {
                    while( itrSubMode.MoveNext() ) {
                        int iIndex = ddModeSub.Items.Count;
                        ddModeSub.Items.Add( itrSubMode.Current );

                        if( oSelectMode != null && 
                            itrSubMode.Current.LegacyMode == oSelectMode.LegacyMode ) 
                        {
                            ddModeSub.SelectedIndex = iIndex;
                        }
                    }
                    if( oSelectMode == null ) {
                        ddModeSub.SelectedIndex = 0;
                    }
                }
            }
        }

        /// <summary>
        /// Set up the dual dropdowns for the SSTV node tool options.
        /// </summary>
        private void InitModes() {
		  //new ViewFamilyDDEditBox(new WinSlot(this), _oDocSSTV.RxSSTVFamilyDoc );
		  //new ViewModeDDEditBox  (new WinSlot(this), _oDocSSTV.RxSSTVModeDoc ) );

            IEnumerator<SSTVDEM.SSTVFamily> itrFamily = SSTVDEM.EnumFamilies();
            while( itrFamily.MoveNext() ) {
                _ddModeMain.Items.Add( itrFamily.Current );
            }

            PopulateSubModes( _ddModeMain, _ddModeSub );

            //_ddModeMain.SelectedIndexChanged += OnSelectedModeChanged;
            _ddModeMain.AutoSize      = true;
            _ddModeMain.Name          = "Mode Select";
            _ddModeMain.TabIndex      = 0;
            _ddModeMain.SelectedIndex = 0;
            _ddModeMain.DropDownStyle = ComboBoxStyle.DropDownList;
            _ddModeMain.Parent        = this;

            //_ddModeSub.SelectedIndexChanged += OnSelectedModeChanged;
            _ddModeSub.AutoSize      = true;
            _ddModeSub.Name          = "Mode Sub Select";
            _ddModeSub.TabIndex      = 1;
            _ddModeSub.SelectedIndex = 0;
            _ddModeSub.DropDownStyle = ComboBoxStyle.DropDownList;
            _ddModeSub.Parent        = this;

            LayoutStack oLayout = new LayoutStackHorizontal() { Spacing = 5 };
            oLayout.Add( new LayoutRect( LayoutRect.CSS.None ) );
            oLayout.Add( new LayoutControl( _ddModeMain, LayoutRect.CSS.Pixels, 150 ) );
            oLayout.Add( new LayoutControl( _ddModeSub,  LayoutRect.CSS.Pixels, 200 ) );
            oLayout.Add( new LayoutRect( LayoutRect.CSS.None ) );

            _rgFlock.Add( ReceiveCommands.Mode, oLayout );

            //_oDocSSTV.RxModeList.CheckedEvent += OnCheckedEvent_RxModeList;
        }

        protected override void OnSizeChanged( EventArgs e ) { 
            _rgFlock[_gSelected].SetRect( 0, 0, Width, Height );
            _rgFlock[_gSelected].LayoutChildren();
        }
    }
}
