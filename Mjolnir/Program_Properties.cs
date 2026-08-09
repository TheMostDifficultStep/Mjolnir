using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml;

using Play.Edit;
using Play.Interfaces.Embedding;

namespace Mjolnir {
    internal class Program_Properties : 
        EditMultiColumn,
        IPgLoad<XmlNode>,
        IPgSave<XmlNode>
    {
        internal class PropertyRow : Row {
            public enum DCol :int {
                Name =0,
                Value,
            }

            static int ColumnCount = Enum.GetValues(typeof(DCol)).Length;
            public Line this[DCol eValue] => this[(int)eValue];

            public string ColumnToString( DCol eValue ) {
                string? strValue = this[eValue].ToString();

                if( strValue is null )
                    return string.Empty;

                return strValue;
            }

            public PropertyRow( string strName, string strValue ) {
                _rgColumns = new Line[ColumnCount];

                ArgumentNullException.ThrowIfNull( strName );

                CreateColumn( DCol.Name,  strName );
                CreateColumn( DCol.Value, strValue );

                CheckForNulls(); 
            }

            void CreateColumn( DCol eCol, string strValue ) {
				_rgColumns[(int)eCol] = new TextLine( (int)eCol, strValue );
            }

        }

        public Program_Properties(IPgBaseSite oSiteBase) : base(oSiteBase) {
        }


        public bool InitNew() {
            return true;
        }

        public bool Load(XmlNode oXmlRoot) {
            ArgumentNullException.ThrowIfNull( oXmlRoot );

            if( oXmlRoot.SelectNodes( "Properties/Property" ) is XmlNodeList rgProps ) {
                foreach( XmlNode oNode in rgProps ) {
                    if( oNode is XmlElement oXmlNode ) {
                        string strName  = oXmlNode.GetAttribute( "name" );
                        string strValue = oXmlNode.InnerText;

                        _rgRows.Add( new PropertyRow( strName, strValue ));
                    }
                }
            }
            RenumberAndSumate();
            Raise_DocLoaded  ();
            DoParse          ();

            return true;
        }

        public bool Save(XmlNode oStream) {
            XmlDocument? oOwner  = oStream.OwnerDocument;
            if( oOwner is null )
                return false;

            XmlElement oRoot = oOwner.CreateElement( "Properties" );

            foreach( Row oRow in _rgRows ) {
                if( oRow is PropertyRow oPropRow ) {
                    XmlElement oXmlRow = oOwner.CreateElement( "Property" );
                    oXmlRow.SetAttribute( "name", oPropRow.ColumnToString( PropertyRow.DCol.Name ) );
                    oXmlRow.InnerText = oPropRow.ColumnToString( PropertyRow.DCol.Value );

                    oRoot.AppendChild( oXmlRow );
                }
            }

            oStream.AppendChild( oRoot );
            return true;
        }
    }
}
