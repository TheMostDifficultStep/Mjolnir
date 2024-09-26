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

        private bool _bProcessCheckModeList = false;


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

        /// <remarks>
        /// This is a nice hybrid of my model/view where the template list is
        /// a standard editor window, but we sync with the dropdowns.
        /// The Famile/Mode pair of dropdowns is more complicated and not sorted yet.
        /// </remarks>
        private void OnSelectedIndexChanged_TemplateDD(object sender, EventArgs e) {
            if( sender is ComboBox ddTemplates ) {
                _oDocSSTV.TemplateList.CheckedLine =
                    _oDocSSTV.TemplateList[ ddTemplates.SelectedIndex ];
            }
		    _oDocSSTV.RenderComposite();
        }

        private void OnCheckedEvent_TemplateList(Line oLineChecked) {
            // if we change the drop down selection b/c of this that'll
            // send an OnSelectedTemplateChanged() and we loop forever
            // so don't set this up for now.
        }

        private void InitTemplates() {
            ComboBox  wnDDTemplates = new ComboBox();

            foreach( Line oLine in _oDocSSTV.TemplateList ) {
                wnDDTemplates.Items.Add( oLine );
            }
            // Set up before callback plugged in.
            if( _oDocSSTV.TemplateList.CheckedLine != null ) {
                wnDDTemplates.SelectedIndex = _oDocSSTV.TemplateList.CheckedLine.At;
            } else {
                _oDocSSTV.TemplateList.CheckedLine = _oDocSSTV.TemplateList[0];
                wnDDTemplates.SelectedIndex = 0;
            }

            _oDocSSTV.TemplateList.CheckedEvent += OnCheckedEvent_TemplateList;

            wnDDTemplates.SelectedIndexChanged += OnSelectedIndexChanged_TemplateDD;
            wnDDTemplates.AutoSize      = true;
            wnDDTemplates.Name          = "Template Select";
            wnDDTemplates.TabIndex      = 1;
            wnDDTemplates.DropDownStyle = ComboBoxStyle.DropDownList;
            wnDDTemplates.Parent        = this;

            LayoutStack oLayout = new LayoutStackHorizontal() { Spacing = 5 };
            oLayout.Add( new LayoutRect( LayoutRect.CSS.None ) );
            oLayout.Add( new LayoutControl( wnDDTemplates, LayoutRect.CSS.Pixels, 200 ) );
            oLayout.Add( new LayoutRect( LayoutRect.CSS.None ) );

            //_rgFlock.Add( TransmitCommands.Templates, oLayout );
            //_gSelected = TransmitCommands.Templates;
        }

        /// <summary>
        /// Normally, we want to get the document message that the checked
        /// line changed. But since the dropdown selectes itself we need
        /// to ignore the checked event later, else we get in an infinite
        /// loop.
        /// </summary>
        /// <seealso cref="PopulateSubModes"/>
        private void OnSelectedModeChanged(object sender, EventArgs e) {
            if( !_bProcessCheckModeList ) {
                _bProcessCheckModeList = true;
                if( sender is ComboBox oMain ) { 
                    PopulateSubModes( oMain, _ddModeSub );
                }
                if( _ddModeSub.SelectedItem is SSTVMode oDDListMode ) {
                    foreach( Line oLine in _oDocSSTV.TxModeList ) {
                        if( oLine.Extra is SSTVMode oTxListMode &&
                            oTxListMode.LegacyMode == oDDListMode.LegacyMode ) 
                        {
                            _oDocSSTV.TxModeList.CheckedLine = oLine;
                        }
                    }
                }
                // Setting the hilight will send an event that DocSSTV will
                // pick up and cause a RenderComposite on that one.
                _bProcessCheckModeList = false;
            }
        }

        /// <summary>
        /// Populate the main dropdown and then this will fill in all
        /// the sub modes for the selected item. It's got the ability to track
        /// the TxModeList selection but I don't think I'm going to need that
        /// since the way to set Mode selection is via the dual drop downs and 
        /// not a straight TxModeList editor.
        /// </summary>
        public void PopulateSubModes( ComboBox ddModeMain, ComboBox ddModeSub, SSTVMode oSelectMode = null ) {
            if( ddModeMain.SelectedItem is SSTVDEM.SSTVFamily oDesc ) {
                ddModeSub.Items.Clear();

                if( oDesc._typClass.GetMethod( "EnumAllModes" ).Invoke( null, Array.Empty<object>() ) is IEnumerator<SSTVMode> itrSubMode ) {
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
        /// When the template changes, the DocSSTV needs to have a doc
        /// specific mode selection. I might as well have that be the checked item in 
        /// the TxModeList. This will be how I sync the views. It's a hack since the
        /// dropdowns send an update event whenever the selection changes even
        /// if we're changing it ourselves, and that is not convenient in our
        /// multiview world. We'll ignore the checked event
        /// if it's coming from ourselves syncing the drop down item
        /// </summary>
        /// <remarks>Not currently used.</remarks>
        /// <param name="oLineChecked">TxModeList line checked.</param>
        private void OnCheckedEvent_TxModeList(Line oLineChecked) {
            if( !_bProcessCheckModeList ) {
                _bProcessCheckModeList = true;
                SSTVMode oNewMode = _oDocSSTV.TransmitModeSelection;

                for( int iIndex = 0; iIndex < _ddModeMain.Items.Count; ++iIndex ) {
                    object oItem = _ddModeMain.Items[iIndex];
                    if( oItem is SSTVDEM.SSTVFamily oDesc &&
                        oDesc.TvFamily == oNewMode.TvFamily ) {
                        _ddModeMain.SelectedIndex = iIndex;

                        for( int iSub = 0; iSub < _ddModeSub.Items.Count; ++iSub ) {
                            if( _ddModeSub.Items[iSub] is SSTVMode oMode ) {
                                PopulateSubModes( _ddModeMain, _ddModeSub, oMode );
                            }
                        }
                    }
                }
                _bProcessCheckModeList = false;
            }
        }

        /// <summary>
        /// Set up the dual dropdowns for the SSTV node tool options.
        /// </summary>
        private void InitModes() {
            IEnumerator<SSTVDEM.SSTVFamily> itrFamily = SSTVDEM.EnumFamilies();
            while( itrFamily.MoveNext() ) {
                _ddModeMain.Items.Add( itrFamily.Current );
            }

            PopulateSubModes( _ddModeMain, _ddModeSub );

            _ddModeMain.SelectedIndexChanged += OnSelectedModeChanged;
            _ddModeMain.AutoSize      = true;
            _ddModeMain.Name          = "Mode Select";
            _ddModeMain.TabIndex      = 0;
            _ddModeMain.SelectedIndex = 0;
            _ddModeMain.DropDownStyle = ComboBoxStyle.DropDownList;
            _ddModeMain.Parent        = this;

            _ddModeSub.SelectedIndexChanged += OnSelectedModeChanged;
            _ddModeSub.AutoSize      = true;
            _ddModeSub.Name          = "Mode Sub Select";
            _ddModeSub.TabIndex      = 1;
            _ddModeSub.SelectedIndex = 0;
            _ddModeSub.DropDownStyle = ComboBoxStyle.DropDownList;
            _ddModeSub.Parent        = this;

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
        /// TODO: If there are multiple TX windows open, they might get out of
        /// sync with the composition. Still need to sort that all out.
        /// But we're close.
        /// </summary>
        /// <seealso cref="PopulateSubModes"/>
        //private void OnSelectedModeChanged(object sender, EventArgs e) {
        //    if( !_bProcessCheckModeList ) {
        //        _bProcessCheckModeList = true;
        //        if( sender == _ddModeMain ) { 
        //            PopulateSubModes( _ddModeMain, _ddModeSub );
        //        }
        //        if( _ddModeSub.SelectedItem is SSTVMode oDDListMode ) {
        //            foreach( Line oLine in _oDocSSTV.RxModeList ) {
        //                if( oLine.Extra is SSTVMode oRxListMode &&
        //                    oRxListMode.LegacyMode == oDDListMode.LegacyMode ) 
        //                {
        //                    _oDocSSTV.RxModeList.CheckedLine = oLine;
        //                }
        //            }
        //        }
        //        _bProcessCheckModeList = false;
        //    }
        //}

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
        /// When the template changes, the DocSSTV needs to have a doc
        /// specific mode selection. I might as well have that be the checked item in 
        /// the TxModeList. This will be how I sync the views. It's a hack since the
        /// dropdowns send an update event whenever the selection changes even
        /// if we're changing it ourselves, and that is not convenient in our
        /// multiview world. We'll ignore the checked event
        /// if it's coming from ourselves syncing the drop down item
        /// </summary>
        /// <param name="oLineChecked">TxModeList line checked.</param>
        //private void OnCheckedEvent_RxModeList(Line oLineChecked) {
        //    if( !_bProcessCheckModeList ) {
        //        if( _oDocSSTV.RxModeList.CheckedLine.Extra is SSTVMode oNewMode ) {
        //            for( int iIndex = 0; iIndex < _ddModeMain.Items.Count; ++iIndex ) {
        //                object oItem = _ddModeMain.Items[iIndex];
        //                if( oItem is SSTVDEM.SSTVFamily oDesc &&
        //                    oDesc._eFamily == oNewMode.Family ) {
        //                    _ddModeMain.SelectedIndex = iIndex;

        //                    for( int iSub = 0; iSub < _ddModeSub.Items.Count; ++iSub ) {
        //                        if( _ddModeSub.Items[iSub] is SSTVMode oMode ) {
        //                            PopulateSubModes( _ddModeMain, _ddModeSub, oMode );
        //                        }
        //                    }
        //                }
        //            }
        //        }
        //    }
        //}

        private void OnCheckEvent_RxSSTVModeDoc(Row obj) {
            throw new NotImplementedException();
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
