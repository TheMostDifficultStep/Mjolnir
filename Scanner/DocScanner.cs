using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections.ObjectModel;

using Play.ImageViewer;
using Play.Interfaces.Embedding;

using LibUsbDotNet;
using LibUsbDotNet.Info;
using LibUsbDotNet.Main;

namespace Scanner {
    public class DocScanner :
        ImageSoloDoc,
        IPgLoad<BinaryReader>
    {
        public DocScanner(IPgBaseSite oSiteBase) : base(oSiteBase) {
        }

        public static UsbDevice MyUsbDevice;

        // https://libusbdotnet.sourceforge.net/V2/Index.html
		protected override bool Initialize() {
            List<string> rgResults = new List<string>();

            // Alas, my scanner does not show up in this list. It seems to
            // use usbscan.sys instead of winusb.sys like my tablet does. 
            // Dump all devices and descriptor information to console output.
            UsbRegDeviceList allDevices = UsbDevice.AllDevices;
            foreach (UsbRegistry usbRegistry in allDevices) {
                if (usbRegistry.Open(out MyUsbDevice)) {
                    rgResults.Add(MyUsbDevice.Info.ToString());
                    for (int iConfig = 0; iConfig < MyUsbDevice.Configs.Count; iConfig++)
                    {
                        UsbConfigInfo configInfo = MyUsbDevice.Configs[iConfig];
                        rgResults.Add(configInfo.ToString());

                        ReadOnlyCollection<UsbInterfaceInfo> interfaceList = configInfo.InterfaceInfoList;
                        for (int iInterface = 0; iInterface < interfaceList.Count; iInterface++)
                        {
                            UsbInterfaceInfo interfaceInfo = interfaceList[iInterface];
                            rgResults.Add(interfaceInfo.ToString());

                            ReadOnlyCollection<UsbEndpointInfo> endpointList = interfaceInfo.EndpointInfoList;
                            for (int iEndpoint = 0; iEndpoint < endpointList.Count; iEndpoint++)
                            {
                                rgResults.Add(endpointList[iEndpoint].ToString());
                            }
                        }
                    }
                }
            }


 			return true;
		}

        public bool Load(BinaryReader oStream) {
            return Initialize();
        }
    } // end class
}
