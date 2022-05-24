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
    public class ToolWins : 
        SKControl,
		IPgLoad
    {
        private readonly IPgViewSite        _oViewSite;
        private readonly ViewTransmitDeluxe _oOwner;
        private readonly DocSSTV            _oDocSSTV;
        private readonly object[]           _rgNullParams = new object[]{};

        Dictionary<Guid, ParentRect > _rgFlock = new (); 
        Guid                          _gSelected = Guid.Empty;

        private readonly ComboBox _ddModeSub  = new ComboBox();
        private readonly ComboBox _ddModeMain = new ComboBox();

        private bool _bProcessCheckModeList = false;


        public ToolWins( IPgViewSite oViewSite, ViewTransmitDeluxe oWinOwner ) { 
            _oViewSite = oViewSite ?? throw new ArgumentNullException( nameof( oViewSite ) );
            _oOwner    = oWinOwner ?? throw new ArgumentNullException( nameof( oWinOwner ) );
            _oDocSSTV  = _oOwner.Document ?? throw new InvalidProgramException( "Can't find document for view" );

            Parent = (Control)_oViewSite.Host;
        }

        public bool InitNew() {
            InitTemplates();
            InitModes    ();

            Execute( TransmitCommands.Mode );

            OnSizeChanged( new EventArgs() );
            //_oDocSSTV.TxModeList.CheckedEvent += OnCheckedEvent_TxModeList;

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

        public SSTVMode CurrentMode {
            get {
                if( _ddModeSub.SelectedItem == null )
                    return _ddModeSub.Items[0] as SSTVMode;

                return _ddModeSub.SelectedItem as SSTVMode;
            }
        }

        /// <summary>
        /// Still need to sort out the messaging so we can keep all the
        /// selected items in sync.
        /// </summary>
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

            _rgFlock.Add( TransmitCommands.Templates, oLayout );
            _gSelected = TransmitCommands.Templates;
        }

        /// <summary>
        /// TODO: If there are multiple TX windows open, they might get out of
        /// sync with the composition. Still need to sort that all out.
        /// But we're close.
        /// </summary>
        /// <seealso cref="PopulateSubModes"/>
        private void OnSelectedModeChanged(object sender, EventArgs e) {
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
        }

        /// <summary>
        /// Populate the main dropdown and then this will fill in all
        /// the sub modes for the selected item. It's got the ability to track
        /// the TxModeList selection but I don't think I'm going to need that
        /// since the way to set Mode selection is via the dual drop downs and 
        /// not a straight TxModeList editor.
        /// </summary>
        private void PopulateSubModes( ComboBox ddModeMain, ComboBox ddModeSub, SSTVMode oSelectMode = null ) {
            if( ddModeMain.SelectedItem is SSTVDEM.ModeDescription oDesc ) {
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
        private void OnCheckedEvent_TxModeList(Line oLineChecked) {
            if( !_bProcessCheckModeList ) {
                _bProcessCheckModeList = true;
                SSTVMode oNewMode = _oDocSSTV.TransmitModeSelection;

                for( int iIndex = 0; iIndex < _ddModeMain.Items.Count; ++iIndex ) {
                    object oItem = _ddModeMain.Items[iIndex];
                    if( oItem is SSTVDEM.ModeDescription oDesc &&
                        oDesc._eFamily == oNewMode.Family ) {
                        _ddModeMain.SelectedIndex = iIndex;

                        for( int iSub = 0; iSub < _ddModeSub.Items.Count; ++iSub ) {
                            if( _ddModeSub.Items[iSub] is SSTVMode oMode ) {
                                PopulateSubModes( _ddModeMain, _ddModeSub, oMode );
                            }
                        }
                    }
                }
            }

            _bProcessCheckModeList = false;
        }

        /// <summary>
        /// Set up the dual dropdowns for the SSTV node tool options.
        /// </summary>
        private void InitModes() {
            IEnumerator<SSTVDEM.ModeDescription> itrFamily = SSTVDEM.EnumFamilies();
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
            oLayout.Add( new LayoutControl( _ddModeSub,  LayoutRect.CSS.Pixels, 200 ) );
            oLayout.Add( new LayoutRect( LayoutRect.CSS.None ) );

            _rgFlock.Add( TransmitCommands.Mode, oLayout );
        }

        protected override void OnSizeChanged( EventArgs e ) { 
            _rgFlock[_gSelected].SetRect( 0, 0, Width, Height );
            _rgFlock[_gSelected].LayoutChildren();
        }
    }
}
