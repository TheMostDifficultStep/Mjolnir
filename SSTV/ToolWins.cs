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
    /// normal Mjolnir herder. Also, all the tools are sort of globbed together.
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

        private readonly ComboBox _ddModeSub = new ComboBox();

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
        private void OnSelectedTemplateChanged(object sender, EventArgs e) {
            if( sender is ComboBox ddTemplates ) {
                _oDocSSTV.TemplateList.CheckedLine =
                    _oDocSSTV.TemplateList[ ddTemplates.SelectedIndex ];
            }
        }

        private void OnCheckedEvent_TemplateList(Line oLineChecked) {
            // if we change the drop down selection b/c of this that'll
            // send an OnSelectedTemplateChanged() and we loop forever
            // so don't set this up for now.
        }

        private void InitTemplates() {
            ComboBox  wnDropDown = new ComboBox();

            foreach( Line oLine in _oDocSSTV.TemplateList ) {
                wnDropDown.Items.Add( oLine );
            }
            // Set up before callback plugged in.
            if( _oDocSSTV.TemplateList.CheckedLine != null ) {
                wnDropDown.SelectedIndex = _oDocSSTV.TemplateList.CheckedLine.At;
            } else {
                _oDocSSTV.TemplateList.CheckedLine = _oDocSSTV.TemplateList[0];
                wnDropDown.SelectedIndex = 0;
            }

            _oDocSSTV.TemplateList.CheckedEvent += OnCheckedEvent_TemplateList;

            wnDropDown.SelectedIndexChanged += OnSelectedTemplateChanged;
            wnDropDown.AutoSize      = true;
            wnDropDown.Name          = "Template Select";
            wnDropDown.TabIndex      = 1;
            wnDropDown.DropDownStyle = ComboBoxStyle.DropDownList;
            wnDropDown.Parent        = this;

            LayoutStack oLayout = new LayoutStackHorizontal() { Spacing = 5 };
            oLayout.Add( new LayoutRect( LayoutRect.CSS.None ) );
            oLayout.Add( new LayoutControl( wnDropDown, LayoutRect.CSS.Pixels, 200 ) );
            oLayout.Add( new LayoutRect( LayoutRect.CSS.None ) );

            _rgFlock.Add( TransmitCommands.Templates, oLayout );
            _gSelected = TransmitCommands.Templates;
        }

        private void OnSelectedModeChanged(object sender, EventArgs e) {
            if( sender is ComboBox oMain ) {
                PopulateSubModes( oMain, _ddModeSub );
            }
        }

        private void PopulateSubModes( ComboBox ddModeMain, ComboBox ddModeSub ) {
            if( ddModeMain.SelectedItem is SSTVDEM.ModeDescription oDesc ) {
                ddModeSub.Items.Clear();

                if( oDesc._typClass.GetMethod( "EnumAllModes" ).Invoke( null, _rgNullParams ) is IEnumerator<SSTVMode> itrSubMode ) {
                    while( itrSubMode.MoveNext() ) {
                        ddModeSub.Items.Add( itrSubMode.Current );
                    } 
                    ddModeSub.SelectedIndex = 0;
                }
            }
        }

        /// <summary>
        /// Set up the dual dropdowns for the SSTV node tool options.
        /// </summary>
        private void InitModes() {
            ComboBox ddModeMain = new ComboBox();

            IEnumerator<SSTVDEM.ModeDescription> itrFamily = SSTVDEM.EnumFamilies();
            while( itrFamily.MoveNext() ) {
                ddModeMain.Items.Add( itrFamily.Current );
            }

            ddModeMain.SelectedIndexChanged += OnSelectedModeChanged;
            ddModeMain.AutoSize      = true;
            ddModeMain.Name          = "Mode Select";
            ddModeMain.TabIndex      = 0;
            ddModeMain.SelectedIndex = 0;
            ddModeMain.DropDownStyle = ComboBoxStyle.DropDownList;
            ddModeMain.Parent        = this;

            PopulateSubModes( ddModeMain, _ddModeSub );

            _ddModeSub.SelectedIndexChanged += OnSelectedModeChanged;
            _ddModeSub.AutoSize      = true;
            _ddModeSub.Name          = "Mode Sub Select";
            _ddModeSub.TabIndex      = 1;
            _ddModeSub.SelectedIndex = 0;
            _ddModeSub.DropDownStyle = ComboBoxStyle.DropDownList;
            _ddModeSub.Parent        = this;

            LayoutStack oLayout = new LayoutStackHorizontal() { Spacing = 5 };
            oLayout.Add( new LayoutRect( LayoutRect.CSS.None ) );
            oLayout.Add( new LayoutControl( ddModeMain, LayoutRect.CSS.Pixels, 150 ) );
            oLayout.Add( new LayoutControl( _ddModeSub, LayoutRect.CSS.Pixels, 200 ) );
            oLayout.Add( new LayoutRect( LayoutRect.CSS.None ) );

            _rgFlock.Add( TransmitCommands.Mode, oLayout );
        }

        protected override void OnSizeChanged( EventArgs e ) { 
            _rgFlock[_gSelected].SetRect( 0, 0, Width, Height );
            _rgFlock[_gSelected].LayoutChildren();
        }
    }
}
