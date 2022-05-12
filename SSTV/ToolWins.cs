using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using SkiaSharp;
using SkiaSharp.Views.Desktop;

using Play.Rectangles;
using Play.Interfaces.Embedding;

namespace Play.SSTV {
    /// <summary>
    /// This should really be like a herder within a herder.
    /// </summary>
    internal class ToolWins : 
        SKControl,
		IPgLoad
    {
        private IPgViewSite        _oViewSite;
        private ViewTransmitDeluxe _oOwner;

        private ComboBox  _wnDropDown;
        public ToolWins( IPgViewSite oViewSite, ViewTransmitDeluxe oWinOwner ) { 
            _oViewSite = oViewSite ?? throw new ArgumentNullException( nameof( oViewSite ) );
            _oOwner    = oWinOwner ?? throw new ArgumentNullException( nameof( oWinOwner ) );

            Parent = (Control)_oViewSite.Host;
        }

        protected readonly LayoutStack _oLayout = new LayoutStackHorizontal() { Spacing = 5 };

        public bool InitNew() {
            _wnDropDown = new ComboBox();

            string[] rgSearchTypes = { "Text", "Regex", "Line" };

            _wnDropDown.Items.AddRange( rgSearchTypes );
            _wnDropDown.SelectedIndexChanged += OnSelectedIndexChanged; ;
            _wnDropDown.AutoSize      = true;
            _wnDropDown.Name          = "oSearchType";
            _wnDropDown.TabIndex      = 1;
            _wnDropDown.SelectedIndex = 1;
            _wnDropDown.DropDownStyle = ComboBoxStyle.DropDownList;
            _wnDropDown.Parent        = this;

            _oLayout.Add( new LayoutRect( LayoutRect.CSS.None ) );
            _oLayout.Add( new LayoutControl( _wnDropDown, LayoutRect.CSS.Flex, 150 ) );
            _oLayout.Add( new LayoutRect( LayoutRect.CSS.None ) );

            return true;
        }

        private void OnSelectedIndexChanged(object sender, EventArgs e) {
        }
    }
}
