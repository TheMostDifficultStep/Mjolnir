using System.Collections.Generic;
using System;

using Play.Edit;
using Play.Interfaces.Embedding;
using Play.Rectangles;
using SkiaSharp;
using System.Windows.Forms;

namespace Play.Clock {
    using DClmn = RowZone.DCol;
    using LCss  = LayoutRect.CSS;
    public class ViewTimeZones : WindowMultiColumn,
        IPgCommandView
    {
      //public readonly DocumentClock _oDocClock;
        public readonly DocumentZones _oDocZones;

        public static Guid Guid { get; } = new Guid("{48FA5562-FC69-49F2-87F6-3A4C86A42D8B}");

        public string Banner => "All Time Zones";

        public SKImage Icon =>  null;

        public Guid Catagory => Guid;

        public ViewTimeZones(IPgViewSite oViewSite, object oDocument) : 
            base(oViewSite, oDocument) 
        {
            // embed our own just for starters.
            _oDocZones =  (DocumentZones)oDocument;
        }


        protected override bool Initialize() {
            if( !base.Initialize() )
                return false;

            if( !_oDocZones.InitNew() )
                return false;

            List<ColumnInfo> rgCols = new List<ColumnInfo> {
                new ( (int)DClmn.Check,   new LayoutRect() { Style=LCss.Flex, Track=33 } ),
                new ( (int)DClmn.Offset, new LayoutRect() { Style=LCss.Flex, Track=30 } ),       
                new ( (int)DClmn.Zone,   new LayoutRect() { Style=LCss.None } ),
            };

            InitColumns( rgCols );

            // At present the base window doesn't put the cursor anywhere, sooo...
            SelectionSet( 0, (int)DClmn.Zone, 0, 0 );

            return true;
        }

        protected override void OnMouseDown(MouseEventArgs e) {
            base.OnMouseDown(e);
        }

        public object Decorate(IPgViewSite oBaseSite, Guid sGuid) {
            return null;
        }
    }
}
