using LibUsbDotNet.DeviceNotify;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Media;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using xbtools.Classes;
using xbtools.Functions;
using xbtools.Nand;
using xbtools.WinUSB;

namespace xbtools
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Variables
        public enum DEVICE
        {
            JR_PROGRAMMER_BOOTLOADER = -1,
            NO_DEVICE = 0,
            JR_PROGRAMMER = 1,
            NAND_X = 2,
            XFLASHER_SPI = 3,
            XFLASHER_EMMC = 4,
            PICOFLASHER = 5,
        }

        public static MainWindow mainWindow;
        public static TextWriter _writer = null; //Console redirect
        private IDeviceNotifier devNotifier;
        public DEVICE device = DEVICE.NO_DEVICE;
        IP myIP = new IP();
        public static Nand.PrivateN nand = new Nand.PrivateN();
        //public PicoFlasher picoflasher = new PicoFlasher();
        public xFlasher xflasher = new xFlasher();
        //public Mtx_Usb mtx_usb = new Mtx_Usb();
        //public xdkbuild XDKbuild = new xdkbuild();
        //public rgh3build rgh3Build = new rgh3build();
        //private NandX nandx = new NandX();
        private Panels.NandInfo nandInfo = new Panels.NandInfo();
        public Panels.XeBuildPanel xPanel = new Panels.XeBuildPanel();
        private Panels.LDrivesInfo ldInfo = new Panels.LDrivesInfo();
        public Panels.XSVFChoice xsvfInfo = new Panels.XSVFChoice();
        private DemoN.DemoN demon = new DemoN.DemoN();
        List<System.Windows.Controls.Control> listInfo = new List<System.Windows.Controls.Control>();
        List<System.Windows.Controls.Control> listTools = new List<System.Windows.Controls.Control>();
        List<System.Windows.Controls.Control> listExtra = new List<System.Windows.Controls.Control>();
        public static EventWaitHandle _waitmb = new AutoResetEvent(true);
        public static readonly object _object = new object();
        public static AutoResetEvent _event1 = new AutoResetEvent(false);
        public static Nand.VNand vnand;
        public static bool usingVNand = false;
        Regex objAlphaPattern = new Regex("[a-fA-F0-9]{32}$");

        #endregion

        #region Init
        public MainWindow()
        {
            InitializeComponent();
            otherGrid.Children.Add(nandInfo);
            listInfo.Add(nandInfo);
            xflasher.initTimerSetup();
            xflasher.inUseTimerSetup();
            mtx_usb.inUseTimerSetup();
            setUp();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            mainWindow = this;
            //Redirect Console Output To Textbox
            _writer = new TextBoxStreamWriter(consoleTB);
            Console.SetOut(_writer);
            Console.WriteLine("Welcome to XBTools Beta - 0.0.1");
            Console.WriteLine("Some functions are disabled until XBTools detects a NAND flasher.");

            // Determine current Windows version
            if (Environment.OSVersion.Version.Major >= 10) variables.currentOS = variables.Windows.Win10; // or 11
            else if (Environment.OSVersion.Version.Major >= 6)
            {
                if (Environment.OSVersion.Version.Minor >= 3) variables.currentOS = variables.Windows.Win81;
                if (Environment.OSVersion.Version.Minor == 2) variables.currentOS = variables.Windows.Win8;
                if (Environment.OSVersion.Version.Minor == 1) variables.currentOS = variables.Windows.Win7;
                else variables.currentOS = variables.Windows.Vista;
            }
            else if (Environment.OSVersion.Version.Major == 5 && Environment.OSVersion.Version.Minor == 1) variables.currentOS = variables.Windows.XP;

            deviceinit();

            try
            {
                if (File.Exists(xflasher.svfPath)) File.Delete(xflasher.svfPath);
            }
            catch { }
        }

        void setUp()
        {
            demon.UpdateBloc += updateBlocks;
            demon.UpdateProgres += updateProgress;
            demon.updateFlas += demon_updateFlas;
            demon.updateMod += demon_updateMod;
            demon.UpdateVer += demon_UpdateVer;

            nandInfo.DragDropChanged += nandInfo_DragDropChanged;
            nandx.UpdateProgres += updateProgress;
            nandx.UpdateBloc += updateBlocks;

            ldInfo.UpdateProgres += updateProgress;
            ldInfo.UpdateBloc += updateBlocks;
            ldInfo.UpdateSourc += xPanel_updateSourc;
            ldInfo.CloseLDClick += ldInfo_CloseLDClick;
            ldInfo.doCompar += ldInfo_doCompar;
            ldInfo.UpdateAdditional += ldInfo_UpdateAdditional;
        }

        public delegate void UpdatedDevice();
        public event UpdatedDevice updateDevice;

        public bool IsUsbDeviceConnected(string pid, string vid)
        {
            using (var searcher = new ManagementObjectSearcher(@"Select * From Win32_USBControllerDevice"))
            {
                using (var collection = searcher.Get())
                {
                    foreach (var device in collection)
                    {
                        var usbDevice = Convert.ToString(device);

                        if (usbDevice.Contains(pid) && usbDevice.Contains(vid))
                            return true;
                    }
                }
            }
            return false;
        }

        private void deviceinit()
        {
            devNotifier = DeviceNotifier.OpenDeviceNotifier();
            devNotifier.OnDeviceNotify += onDevNotify;

            showDemon(DemoN.DemoN.FindDemon());

            if (!DemoN.DemoN.DemonDetected)
            {
                if (IsUsbDeviceConnected("7001", "600D")) // PicoFlasher
                {
                    Console.WriteLine("PicoFlasher Detected!");
                    readNandBtn.IsEnabled = true;
                    writeNandBtn.IsEnabled = true;
                    writeEccBtn.IsEnabled = true;
                    //PicoFlasherToolStripMenuItem.Visible = true;
                    device = DEVICE.PICOFLASHER;
                }
                else if (IsUsbDeviceConnected("6010", "0403")) // xFlasher SPI
                {
                    Console.WriteLine("xFlasher SPI Detected!");
                    readNandBtn.IsEnabled = true;
                    writeNandBtn.IsEnabled = true;
                    writeEccBtn.IsEnabled = true;
                    //xFlasherToolStripMenuItem.Visible = true;
                    device = DEVICE.XFLASHER_SPI;
                    xflasher.ready = true; // Skip init
                }
                else if (IsUsbDeviceConnected("8334", "11D4")) // JR-Programmer Bootloader
                {
                    Console.WriteLine("JR-Programmer (Bootloader) Detected!");
                    readNandBtn.IsEnabled = true;
                    writeNandBtn.IsEnabled = true;
                    writeEccBtn.IsEnabled = true;
                    //jRPBLToolStripMenuItem.Visible = true;
                    device = DEVICE.JR_PROGRAMMER_BOOTLOADER;
                }
                else
                {
                    LibUsbDotNet.Main.UsbRegDeviceList mDevList = LibUsbDotNet.UsbDevice.AllDevices;
                    foreach (LibUsbDotNet.Main.UsbRegistry devic in mDevList)
                    {
                        if (devic.Pid == 0x0004 && devic.Vid == 0xFFFF) // NAND-X
                        {
                            if (variables.mtxUsbMode)
                            {
                                Console.WriteLine("Matrix Flasher Detected!");
                                readNandBtn.IsEnabled = true;
                                writeNandBtn.IsEnabled = true;
                                writeEccBtn.IsEnabled = true;
                            }
                            else
                            {
                                Console.WriteLine("NAND-X Detected!");
                                readNandBtn.IsEnabled = true;
                                writeNandBtn.IsEnabled = true;
                                writeEccBtn.IsEnabled = true;
                            }
                            //nANDXToolStripMenuItem.Visible = true;
                            device = DEVICE.NAND_X;
                        }
                        else if (devic.Pid == 0x8338 && devic.Vid == 0x11D4) // JR-Programmer
                        {
                            Console.WriteLine("JR-Programmer Detected!");
                            readNandBtn.IsEnabled = true;
                            writeNandBtn.IsEnabled = true;
                            writeEccBtn.IsEnabled = true;
                            //jRPToolStripMenuItem.Visible = true;
                            device = DEVICE.JR_PROGRAMMER;
                        }
                    }
                }

                if (device == DEVICE.NO_DEVICE) // Must check this after everything else
                {
                    if (IsUsbDeviceConnected("AAAA", "8816") || IsUsbDeviceConnected("05E3", "0751")) // xFlasher eMMC
                    {
                        Console.WriteLine("xFlasher (eMMC) Detected!");
                        readNandBtn.IsEnabled = true;
                        writeNandBtn.IsEnabled = true;
                        writeEccBtn.IsEnabled = true;
                        //xFlasherToolStripMenuItem.Visible = true;
                        device = DEVICE.XFLASHER_EMMC;
                    }
                }
            }

            try // It'll fail if the thing doesn't exist
            {
                if (updateDevice != null)
                    updateDevice();
            }
            catch
            {
                // Do nothing
            }
        }
        #endregion

        #region Demon
        bool showingdemon = false;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var hwndSource = PresentationSource.FromVisual(this) as HwndSource;
            if (hwndSource != null)
            {
                hwndSource.AddHook(WndProc);
            }
        }
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            var message = (WindowMessage)msg;
            var subCode = (WindowMessageParameter)wParam.ToInt32();

            try
            {
                // The OnDeviceChange routine processes WM_DEVICECHANGE messages.
                if (message == WindowMessage.WM_DEVICECHANGE)
                {
                    if ((subCode == WindowMessageParameter.DBT_DEVICEREMOVECOMPLETE))
                    {
                        //Console.WriteLine(DemoN.BootloaderPathName);
                        if (DemoN.DemoN.DemonManagement.DeviceNameMatch(msg, DemoN.DemoN.DemonPathName))
                        {
                            DemoN.DemoN.DemonDetected = false;
                            showDemon(false);
                        }
                    }
                    else if (subCode == WindowMessageParameter.DBT_DEVICEARRIVAL)
                    {
                        if (!Classes.HID.BootloaderDetected) Classes.HID.FindBootloader();
                    }
                    else
                    {
                        if (!Classes.HID.BootloaderDetected) Classes.HID.FindBootloader();
                        if (!showingdemon) showDemon(DemoN.DemoN.FindDemon());
                    }
                }

                return IntPtr.Zero;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return IntPtr.Zero;
            }
        }

        private void showDemon(bool show) // TODO: This should be not seperate
        {
            if (variables.debugme) Console.WriteLine("ShowDemoN {0}", show);
            showingdemon = true;
            if (show)
            {
                Thread.Sleep(100);
                demon.get_mode();

                if (DemoN.DemoN.mode == DemoN.DemoN.Demon_Modes.FIRMWARE)
                {
                    demon.get_firmware();
                    demon.get_external_flash(false);
                }

                Console.WriteLine("Demon Detected!");
                readNandBtn.IsEnabled = true;
                writeNandBtn.IsEnabled = true;
                writeEccBtn.IsEnabled = true;
                //demoNToolStripMenuItem.Visible = true;
                //ModeStatus.Visible = true;
                //ModeVersion.Visible = true;
                //FWStatus.Visible = true;
                //FWVersion.Visible = true;
            }
            else
            {
                if (device == DEVICE.JR_PROGRAMMER_BOOTLOADER)
                {
                    Console.WriteLine("JR-Programmer (Bootloader) Detected!");
                    readNandBtn.IsEnabled = true;
                    writeNandBtn.IsEnabled = true;
                    writeEccBtn.IsEnabled = true;
                }
                else if (device == DEVICE.JR_PROGRAMMER)
                {
                    Console.WriteLine("JR-Programmer Detected!");
                    readNandBtn.IsEnabled = true;
                    writeNandBtn.IsEnabled = true;
                    writeEccBtn.IsEnabled = true;
                }
                else if (device == DEVICE.NAND_X)
                {
                    if (variables.mtxUsbMode)
                    {
                        Console.WriteLine("Matrix Flasher Detected!");
                        readNandBtn.IsEnabled = true;
                        writeNandBtn.IsEnabled = true;
                        writeEccBtn.IsEnabled = true;
                    }
                    else
                    {
                        Console.WriteLine("NAND-X Detected!");
                        readNandBtn.IsEnabled = true;
                        writeNandBtn.IsEnabled = true;
                        writeEccBtn.IsEnabled = true;
                    }
                }
                else if (device == DEVICE.XFLASHER_SPI)
                {
                    Console.WriteLine("xFlasher SPI Detected!");
                    readNandBtn.IsEnabled = true;
                    writeNandBtn.IsEnabled = true;
                    writeEccBtn.IsEnabled = true;
                }
                else if (device == DEVICE.XFLASHER_EMMC)
                {
                    Console.WriteLine("xFlasher eMMC Detected!");
                    readNandBtn.IsEnabled = true;
                    writeNandBtn.IsEnabled = true;
                    writeEccBtn.IsEnabled = true;
                }
                else if (device == DEVICE.PICOFLASHER)
                {
                    Console.WriteLine("PicoFlasher Detected!");
                    readNandBtn.IsEnabled = true;
                    writeNandBtn.IsEnabled = true;
                    writeEccBtn.IsEnabled = true;
                }
                else
                {
                    //nTools.setImage(null);
                }
                //Console.WriteLine(device);
                //ModeStatus.Visible = false;
                //ModeVersion.Visible = false;
                demonMenuItem.Visibility = Visibility.Hidden;
                //FWStatus.Visible = false;
                //FWVersion.Visible = false;
                //FlashStatus.Visible = false;
                //FlashVersion.Visible = false;
            }
            showingdemon = false;
        }
        private void onDevNotify(object sender, DeviceNotifyEventArgs e)
        {
            try
            {
                if (variables.debugme) Console.WriteLine("DevNotify - {0}", e.Device.Name);
                if (variables.debugme) Console.WriteLine("EventType - {0}", e.EventType);
                if (e.EventType == LibUsbDotNet.DeviceNotify.EventType.DeviceArrival)
                {
                    if (e.Device.IdVendor == 0x600D && e.Device.IdProduct == 0x7001) // PicoFlasher
                    {
                        if (!DemoN.DemoN.DemonDetected)
                        {
                            Console.WriteLine("PicoFlasher Detected!");
                            readNandBtn.IsEnabled = true;
                            writeNandBtn.IsEnabled = true;
                            writeEccBtn.IsEnabled = true;
                        }
                        //PicoFlasherToolStripMenuItem.Visible = true;
                        device = DEVICE.PICOFLASHER;
                    }
                    else if (e.Device.IdVendor == 0x0403 && e.Device.IdProduct == 0x6010) // xFlasher SPI
                    {
                        if (!DemoN.DemoN.DemonDetected)
                        {
                            Console.WriteLine("xFlasher Detected!");
                            readNandBtn.IsEnabled = true;
                            writeNandBtn.IsEnabled = true;
                            writeEccBtn.IsEnabled = true;
                        }
                        xFlasherMenuItem.Visibility = Visibility.Visible;
                        device = DEVICE.XFLASHER_SPI;
                        xflasher.initDevice();
                    }
                    else if (e.Device.IdVendor == 0xFFFF && e.Device.IdProduct == 0x004) // NAND-X
                    {
                        if (!DemoN.DemoN.DemonDetected)
                        {
                            if (variables.mtxUsbMode)
                            {
                                Console.WriteLine("Matrix Flasher Detected!");
                                readNandBtn.IsEnabled = true;
                                writeNandBtn.IsEnabled = true;
                                writeEccBtn.IsEnabled = true;
                            }
                            else
                            {
                                Console.WriteLine("NAND-X Detected!");
                                readNandBtn.IsEnabled = true;
                                writeNandBtn.IsEnabled = true;
                                writeEccBtn.IsEnabled = true;
                            }
                        }
                        nandxMenuItem.Visibility = Visibility.Visible;
                        device = DEVICE.NAND_X;
                    }
                    else if (e.Device.IdVendor == 0x11D4 && e.Device.IdProduct == 0x8338) // JR-Programmer
                    {
                        if (!DemoN.DemoN.DemonDetected)
                        {
                            Console.WriteLine("JR-Programmer Detected!");
                            readNandBtn.IsEnabled = true;
                            writeNandBtn.IsEnabled = true;
                            writeEccBtn.IsEnabled = true;
                        }
                        jrpBLMenuItem.Visibility = Visibility.Hidden;
                        jrpNormalMenuItem.Visibility = Visibility.Visible;
                        device = DEVICE.JR_PROGRAMMER;
                    }
                    else if (e.Device.IdVendor == 0x11D4 && e.Device.IdProduct == 0x8334) // JR-Programmer Bootloader
                    {
                        if (!DemoN.DemoN.DemonDetected)
                        {
                            Console.WriteLine("JR-Programmer (Bootloader) Detected!");
                            readNandBtn.IsEnabled = true;
                            writeNandBtn.IsEnabled = true;
                            writeEccBtn.IsEnabled = true;
                        }
                        jrpBLMenuItem.Visibility = Visibility.Visible;
                        jrpNormalMenuItem.Visibility = Visibility.Hidden;
                        device = DEVICE.JR_PROGRAMMER_BOOTLOADER;
                    }
                    else if ((e.Device.IdVendor == 0xAAAA && e.Device.IdProduct == 0x8816) || (e.Device.IdVendor == 0x05E3 && e.Device.IdProduct == 0x0751)) // xFlasher eMMC
                    {
                        if (!DemoN.DemoN.DemonDetected)
                        {
                            Console.WriteLine("xFlasher eMMC Detected!");
                            readNandBtn.IsEnabled = true;
                            writeNandBtn.IsEnabled = true;
                            writeEccBtn.IsEnabled = true;
                        }
                        jrpBLMenuItem.Visibility = Visibility.Visible;
                        device = DEVICE.XFLASHER_EMMC;
                    }
                }
                else if (e.EventType == LibUsbDotNet.DeviceNotify.EventType.DeviceRemoveComplete)
                {
                    if (e.Device.IdVendor == 0x600D && e.Device.IdProduct == 0x7001)
                    {
                        //PicoFlasherToolStripMenuItem.Visible = false;
                        device = DEVICE.NO_DEVICE;
                    }
                    else if (e.Device.IdVendor == 0x11d4 && e.Device.IdProduct == 0x8334)
                    {
                        Classes.HID.BootloaderDetected = false;
                        jrpBLMenuItem.Visibility = Visibility.Hidden;
                        device = 0;
                    }
                    else if (e.Device.IdVendor == 0xFFFF && e.Device.IdProduct == 0x004)
                    {
                        nandxMenuItem.Visibility = Visibility.Hidden;
                        device = 0;
                    }
                    else if (e.Device.IdVendor == 0x11d4 && e.Device.IdProduct == 0x8338)
                    {
                        jrpNormalMenuItem.Visibility = Visibility.Hidden;
                        device = DEVICE.NO_DEVICE;
                    }
                    else if ((e.Device.IdVendor == 0x0403 && e.Device.IdProduct == 0x6010) ||
                        (e.Device.IdVendor == 0x05E3 && e.Device.IdProduct == 0x0751) ||
                         (e.Device.IdVendor == 0xAAAA && e.Device.IdProduct == 0x8816))
                    {
                        xFlasherMenuItem.Visibility = Visibility.Hidden;
                        device = DEVICE.NO_DEVICE;
                    }
                }

                if (listInfo.Contains(ldInfo)) ldInfo.refreshDrives(true);
            }
            catch (Exception ex) { if (variables.debugme) Console.WriteLine(ex.ToString()); }
            try // It'll fail if the thing doesn't exist
            {
                if (updateDevice != null)
                    updateDevice();
            }
            catch
            {
                // Do nothing
            }
        }
        #endregion

        #region Small Stuff
        void movework()
        {
            if (variables.reading) return;
            Thread.Sleep(2000);
            variables.xefolder = System.IO.Path.Combine(Directory.GetParent(variables.outfolder).FullName, nand.ki.serial);

            //updateS((variables.filename1.Replace(variables.outfolder, variables.xefolder)));
            Console.WriteLine("Moving all files from output folder to {0}", variables.xefolder);
            Console.Write("");
            String l_sDirectoryName = variables.xefolder;
            DirectoryInfo l_dDirInfo = new DirectoryInfo(l_sDirectoryName);
            if (l_dDirInfo.Exists == false)
                Directory.CreateDirectory(l_sDirectoryName);
            List<String> MyFiles = Directory.GetFiles(variables.outfolder, "*.*", SearchOption.TopDirectoryOnly).ToList();
            List<String> myfolders = Directory.GetDirectories(variables.outfolder, "*.*", SearchOption.TopDirectoryOnly).ToList();
            foreach (string fold in myfolders)
            {
                try
                {

                    string name = System.IO.Path.GetFileName(fold);
                    // if (Directory.Exists(l_dDirInfo + "\\" + fold)) Directory.Delete(l_dDirInfo + "\\" + fold);
                    if (variables.debugme) Console.WriteLine("Moving {0}", fold);


                    if ((fold.Contains(nand.ki.serial)) || ((variables.custname != "") && (fold.Contains(variables.custname))))
                    {
                        System.IO.Directory.Move(fold, System.IO.Path.Combine(variables.xefolder, name));
                        variables.custname = "";
                    }

                }
                catch (System.IO.IOException e)
                {
                    Console.WriteLine(e.Message);
                }

            }
            foreach (string file in MyFiles)
            {
                if (variables.debugme) Console.WriteLine("Moving {0}", file);
                FileInfo mFile = new FileInfo(file);
                if (new FileInfo(l_dDirInfo + "\\" + mFile.Name).Exists == false)//to remove name collusion
                    mFile.MoveTo(l_dDirInfo + "\\" + mFile.Name);
                else
                {
                    string flname = System.IO.Path.GetFileNameWithoutExtension(mFile.Name);
                    int number = 1;
                    if (flname.Contains("(") && flname.Contains(")"))
                    {
                        //Console.WriteLine(flname.Substring(0,flname.IndexOf("(")));
                        //number = Convert.ToInt32(flname.Substring(flname.IndexOf("("), 1)) ;
                        string Nflname = flname.Substring(0, flname.IndexOf("("));

                        do
                        {
                            number++;
                        } while (File.Exists(l_dDirInfo + "\\" + Nflname + "(" + number + ")" + mFile.Extension));
                        if (!File.Exists(l_dDirInfo + "\\" + Nflname + "(" + number + ")" + mFile.Extension))
                        {
                            mFile.MoveTo(l_dDirInfo + "\\" + Nflname + "(" + number + ")" + mFile.Extension);
                        }
                    }
                    else
                    {
                        do
                        {
                            number++;
                        } while (File.Exists(l_dDirInfo + "\\" + System.IO.Path.GetFileNameWithoutExtension(mFile.Name) + "(" + number + ")" + mFile.Extension));
                        if (!File.Exists(l_dDirInfo + "\\" + System.IO.Path.GetFileNameWithoutExtension(mFile.Name) + "(" + number + ")" + mFile.Extension))
                        {
                            mFile.MoveTo(l_dDirInfo + "\\" + System.IO.Path.GetFileNameWithoutExtension(mFile.Name) + "(" + number + ")" + mFile.Extension);
                        }
                    }
                }
            }

            variables.filename1 = variables.filename1.Replace(variables.outfolder, variables.xefolder);
            Dispatcher.BeginInvoke(new Action(() => nandSrcTB.Text = variables.filename1));
            nand = new Nand.PrivateN(variables.filename1, variables.cpkey); // Re-init because folder changed
        }

        public void nand_init()
        {
            ThreadStart starter = delegate { nandinit(); };
            new Thread(starter).Start();
        }

        private void updatecptextbox()
        {
            if (variables.debugme) Console.WriteLine("Event wait");
            _event1.WaitOne();
            if (variables.debugme) Console.WriteLine("Event Started");
            if (variables.debugme) Console.WriteLine(variables.cpkey);
            Dispatcher.BeginInvoke(new Action(() => cpuKeyTB.Text = variables.cpkey));
        }

        private static void savekvinfo(string savefile)
        {
            try
            {
                if (!nand.ok) return;
                TextWriter tw = new StreamWriter(savefile);
                tw.WriteLine("*******************************************");
                tw.WriteLine("*******************************************");
                string console_type = "";
                if (nand.bl.CB_A >= 9188 && nand.bl.CB_A <= 9250)
                {
                    console_type = "Trinity";
                }
                else if (nand.bl.CB_A >= 13121 && nand.bl.CB_A <= 13200)
                {
                    console_type = "Corona";
                    if (nand.noecc) console_type += " 4GB";
                }
                else if (nand.bl.CB_A >= 6712 && nand.bl.CB_A <= 6780) console_type = "Jasper";
                else if (nand.bl.CB_A >= 4558 && nand.bl.CB_A <= 4590) console_type = "Zephyr";
                else if ((nand.bl.CB_A >= 1888 && nand.bl.CB_A <= 1960) || nand.bl.CB_A == 7373 || nand.bl.CB_A == 8192) console_type = "Xenon";
                else if (nand.bl.CB_A >= 5761 && nand.bl.CB_A <= 5780) console_type = "Falcon";
                else
                {
                    if (variables.smcmbtype < variables.console_types.Length && variables.smcmbtype >= 0) console_type = variables.console_types[variables.smcmbtype];
                }
                tw.WriteLine("Console Type: {0}", console_type);
                tw.WriteLine("");
                tw.WriteLine("Cpu Key: {0}", variables.cpkey);
                tw.WriteLine("");
                tw.WriteLine("KV Type: {0}", nand.ki.kvtype.Replace("0", ""));
                tw.WriteLine("");
                tw.WriteLine("MFR Date: {0}", nand.ki.mfdate);
                tw.WriteLine("");
                tw.WriteLine("Console ID: {0}", nand.ki.consoleid);
                tw.WriteLine("");
                tw.WriteLine("Serial: {0}", nand.ki.serial);
                tw.WriteLine("");
                string region = "";
                if (nand.ki.region == "02FE") region = "PAL/EU";
                else if (nand.ki.region == "00FF") region = "NTSC/US";
                else if (nand.ki.region == "01FE") region = "NTSC/JAP";
                else if (nand.ki.region == "01FF") region = "NTSC/JAP";
                else if (nand.ki.region == "01FC") region = "NTSC/KOR";
                else if (nand.ki.region == "0101") region = "NTSC/HK";
                else if (nand.ki.region == "0201") region = "PAL/AUS";
                else if (nand.ki.region == "7FFF") region = "DEVKIT";
                tw.WriteLine("Region: {0} | {1}", nand.ki.region, region);
                tw.WriteLine("");
                tw.WriteLine("Osig: {0}", nand.ki.osig);
                tw.WriteLine("");
                tw.WriteLine("DVD Key: {0}", nand.ki.dvdkey);
                tw.WriteLine("");
                tw.WriteLine("*******************************************");
                tw.WriteLine("*******************************************");
                tw.Close();
                Console.WriteLine("KV Info saved to file");
            }
            catch (Exception ex) { if (variables.debugme) Console.WriteLine(ex.ToString()); Console.WriteLine("Failed"); }
        }

        void comparenands()
        {
            if (variables.filename1 == null || variables.filename2 == null) { System.Windows.MessageBox.Show("Input all Files"); return; }
            if (!File.Exists(variables.filename1) || !File.Exists(variables.filename2)) return;
            else
            {
                FileInfo inf = new FileInfo(variables.filename1);
                string time = "";
                if (inf.Length > 64 * 1024 * 1024) time = "Takes a while on big nands";
                Console.WriteLine("Comparing...{0}", time);
                try
                {
                    byte[] temp1 = Nand.BadBlock.find_bad_blocks_b(variables.filename1, true);
                    byte[] temp2 = Nand.BadBlock.find_bad_blocks_b(variables.filename2, true);

                    string temp1_hash = Oper.GetMD5HashFromFile(temp1);
                    string temp2_hash = Oper.GetMD5HashFromFile(temp2);

                    temp1 = null;
                    temp2 = null;
                    //filecompareresult = FileEquals(filename1, filename2);
                    if (temp1_hash == temp2_hash)
                    {
                        Console.WriteLine("Nands are the same");
                        Console.WriteLine("");
                        try
                        {
                            SoundPlayer success = new SoundPlayer(Properties.Resources.chime);
                            if (variables.soundcompare != "") success.SoundLocation = variables.soundcompare;
                            success.Play();
                        }
                        catch (Exception ex) { if (variables.debugme) Console.WriteLine(ex.ToString()); };
                        try
                        {
                            string md5file = System.IO.Path.Combine(Directory.GetParent(variables.filename1).ToString(), "checksum.md5");
                            string hash1 = Oper.GetMD5HashFromFile(variables.filename1);
                            string hash2 = Oper.GetMD5HashFromFile(variables.filename2);
                            if (File.Exists(md5file))
                            {
                                File.AppendAllText(md5file, "\n");
                                File.AppendAllText(md5file, hash1 + " *" + System.IO.Path.GetFileName(variables.filename1) + "\n");
                                File.AppendAllText(md5file, hash1 + " *" + System.IO.Path.GetFileName(variables.filename2) + "\n");
                            }
                            else
                            {
                                using (StreamWriter file = new StreamWriter(System.IO.Path.Combine(Directory.GetParent(variables.filename1).ToString(), "checksum.md5")))
                                {
                                    file.WriteLine("# MD5 checksums generated by J-Runner");
                                    file.WriteLine("{0} *{1}", hash1, System.IO.Path.GetFileName(variables.filename1));
                                    file.WriteLine("{0} *{1}", hash2, System.IO.Path.GetFileName(variables.filename2));
                                }
                            }
                            if (variables.deletefiles)
                            {
                                File.Delete(variables.filename2);
                                nandExtraTB.Text = "";
                            }
                        }
                        catch (Exception ex) { if (variables.debugme) Console.WriteLine(ex.ToString()); }
                    }
                    else
                    {
                        try
                        {
                            SoundPlayer error = new SoundPlayer(Properties.Resources.Error);
                            if (variables.sounderror != "") error.SoundLocation = variables.sounderror;
                            error.Play();
                        }
                        catch (Exception ex) { if (variables.debugme) Console.WriteLine(ex.ToString()); };

                        if (System.Windows.MessageBox.Show("Files do not match!\nShow Differences?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Exclamation) == MessageBoxResult.Yes)
                        {
                            FileEquals(variables.filename1, variables.filename2);
                        }
                        nandSrcTB.Text = "";
                        nandExtraTB.Text = "";
                        variables.filename1 = "";
                        variables.filename2 = "";
                    }
                }
                catch (Exception ex) { Console.WriteLine(ex.InnerException.ToString()); }
            }
        }

        public static string parsecpukey(string filename)
        {
            if (System.IO.Path.GetExtension(filename) == ".txt")
            {
                Regex objAlphaPattern = new Regex("[a-fA-F0-9]{32}$");
                string[] cpu = File.ReadAllLines(filename);
                string cpukey = "";
                bool check = false;
                int i = 0;
                foreach (string line in cpu)
                {
                    if (objAlphaPattern.Match(line).Success) i++;
                    if (i > 1) check = true;
                }
                foreach (string line in cpu)
                {
                    if (check)
                    {
                        if (line.ToUpper().Contains("CPU"))
                        {
                            cpukey = (objAlphaPattern.Match(line).Value);
                        }
                    }
                    else
                    {
                        cpukey = (objAlphaPattern.Match(line).Value);
                        break;
                    }
                    //Console.WriteLine(objAlphaPattern.Match(line).Value);
                }
                if (Nand.Nand.VerifyKey(Oper.StringToByteArray(cpukey))) return cpukey;
                else return "";
            }
            else return "";
        }

        private long CRCbl(string filename)
        {
            crc32 crc = new crc32();
            long hashData = 0;
            if (File.Exists(filename))
            {
                byte[] fileb = File.ReadAllBytes(filename);
                fileb = editbl(fileb);
                hashData = crc.CRC(fileb);
            }
            return hashData;
        }
        private byte[] editbl(byte[] bl)
        {
            int length = Oper.ByteArrayToInt(Oper.returnportion(bl, 0xC, 4));
            if (bl[0] == 0x43 && bl[1] == 0x42)
            {
                for (int i = 0x10; i < 0x40; i++) bl[i] = 0x0;
            }
            else if (bl[0] == 0x43 && bl[1] == 0x44)
            {
                for (int i = 0x10; i < 0x20; i++) bl[i] = 0x0;
            }
            else if (bl[0] == 0x43 && bl[1] == 0x45)
            {
                for (int i = 0x10; i < 0x20; i++) bl[i] = 0x0;
            }
            else if (bl[0] == 0x43 && bl[1] == 0x46)
            {
                for (int i = 0x20; i < 0x230; i++) bl[i] = 0x0;
            }
            else if (bl[0] == 0x43 && bl[1] == 0x47)
            {
                for (int i = 0x10; i < 0x20; i++) bl[i] = 0x0;
            }
            return Oper.returnportion(bl, 0, length);
        }
        bool editblini(string file, string label, string cba, string cbb = "")
        {
            string bla;
            string blb;
            bool splitcb = true;
            if (String.IsNullOrWhiteSpace(cbb)) splitcb = false;
            if (!splitcb)
            {
                if (!File.Exists(System.IO.Path.Combine(variables.rootfolder, "common", "cb_" + cba + ".bin")))
                {
                    Console.WriteLine("{0} not found. Trying to download file..", "common/cb_" + cba + ".bin");
                }
                if (!File.Exists(System.IO.Path.Combine(variables.rootfolder, "common", "cb_" + cba + ".bin")))
                {
                    Console.WriteLine("{0} not found. Insert it manually on the common folder", "cb_" + cba + ".bin");
                    return false;
                }
                bla = "cb_" + cba + ".bin," + CRCbl(System.IO.Path.Combine(variables.rootfolder, "common", "cb_" + cba + ".bin")).ToString("x8");
                blb = "none,00000000";
            }
            else
            {
                if (!File.Exists(System.IO.Path.Combine(variables.rootfolder, "common", "cba_" + cba + ".bin")))
                {
                    Console.WriteLine("{0} not found. Trying to download file..", "common/cba_" + cba + ".bin");
                }
                if (!File.Exists(System.IO.Path.Combine(variables.rootfolder, "common", "cbb_" + cbb + ".bin")))
                {
                    Console.WriteLine("{0} not found. Trying to download file..", "common/cbb_" + cbb + ".bin");
                }
                if (!File.Exists(System.IO.Path.Combine(variables.rootfolder, "common", "cba_" + cba + ".bin")))
                {
                    Console.WriteLine("{0} not found. Insert it manually on the common folder", "cba_" + cba + ".bin");
                    return false;
                }
                if (!File.Exists(System.IO.Path.Combine(variables.rootfolder, "common", "cbb_" + cbb + ".bin")))
                {
                    Console.WriteLine("{0} not found. Insert it manually on the common folder", "cbb_" + cba + ".bin");
                    return false;
                }
                bla = "cba_" + cba + ".bin," + CRCbl(System.IO.Path.Combine(variables.rootfolder, "common", "cba_" + cba + ".bin")).ToString("x8");
                blb = "cbb_" + cbb + ".bin," + CRCbl(System.IO.Path.Combine(variables.rootfolder, "common", "cbb_" + cbb + ".bin")).ToString("x8");
            }
            Console.WriteLine("Editing File..");
            string[] lines = File.ReadAllLines(file);
            int i = 0;
            for (; i < lines.Length; i++)
            {
                if (lines[i] == "") continue;
                else if (lines[i].Contains('[') && lines[i].Contains(label) && lines[i].Contains(']')) break;
            }
            lines[i + 1] = bla;
            lines[i + 2] = blb;
            File.WriteAllLines(file, lines);
            Console.WriteLine("Done");
            return true;
        }

        static int FileEquals(string fileName1, string fileName2)
        {
            // Check the file size and CRC equality here.. if they are equal...    
            try
            {
                using (var file1 = new FileStream(fileName1, FileMode.Open))
                using (var file2 = new FileStream(fileName2, FileMode.Open))
                    return StreamEquals(file1, file2);
            }
            catch (System.IO.IOException)
            {
                return -1;
            }
        }
        static int StreamEquals(Stream stream1, Stream stream2)
        {
            const int bufferSize = 0x4200;
            int count = 0;
            byte[] buffer1 = new byte[bufferSize]; //buffer size
            byte[] buffer2 = new byte[bufferSize];
            while (true)
            {
                count += 0x4200;
                int count1 = stream1.Read(buffer1, 0, bufferSize);
                int count2 = stream2.Read(buffer2, 0, bufferSize);

                if (count1 != count2)
                    return 0;

                if (count1 == 0)
                    return 1;

                // You might replace the following with an efficient "memcmp"
                if (!buffer1.Take(count1).SequenceEqual(buffer2.Take(count2)))
                    Console.WriteLine("0x{0:X4}", (count - 0x4200) / 0x4200);
            }
        }

        #endregion

        #region Nand Manipulation

        public void newSession(bool partial = false)
        {
            if (!String.IsNullOrEmpty(variables.filename1))
            {
                if (!partial)
                {
                    cpuKeyTB.Text = "";
                    variables.boardtype = null;
                }

                nandSrcTB.Text = "";
                nandExtraTB.Text = "";
                variables.filename = "";
                variables.filename1 = "";
                variables.filename2 = "";
                variables.xefolder = "";
                variables.cpkey = "";
                variables.gotvalues = false;
                variables.twombread = false;
                variables.fulldump = false;
                variables.flashconfig = "";
                variables.changeldv = 0;
                variables.rghable = true;
                variables.rgh1able = true;
                nand = new Nand.PrivateN();
                nandInfo.clear();
            }

            if (!partial)
            {
                xPanel.clear();
                variables.ctyp = variables.ctypes[0];
                txtIP.Text = txtIP.Text.Remove(txtIP.Text.LastIndexOf('.')) + ".";
            }

            mainmainProgressBar.Value = mainmainProgressBar.Minimum;

            if (!partial)
            {
                saveToLog();
                consoleTB.Text = "";
            }
        }

        void erasevariables()
        {
            variables.fulldump = false; variables.twombread = false;
            variables.ctyp = variables.ctypes[0]; variables.gotvalues = false;
            variables.cpkey = "";
            //variables.outfolder = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "output");
            xPanel.setMBname("");
            cpuKeyTB.Text = "";
            variables.flashconfig = "";
            /*if (variables.changeldv != 0)
            {
                string cfldv = "cfldv=";
                string[] edit = { cfldv };
                string[] delete = { };
                parse_ini.edit_ini(Path.Combine(variables.pathforit, @"xeBuild\data\options.ini"), edit, delete);
             * */
            variables.changeldv = 0;
            //}
            //btnCheckBadBlocks.Visible = true;
        }

        void nandinit()
        {
            bool movedalready = false;
            if (String.IsNullOrEmpty(variables.filename1)) return;
            if (!File.Exists(variables.filename1))
            {
                System.Windows.MessageBox.Show("No file was selected!", "Can't", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            try
            {

                updateProgress(mainmainProgressBar.Minimum);
                if (System.IO.Path.GetExtension(variables.filename1) != ".bin") return;
                variables.gotvalues = true;

                bool sts = objAlphaPattern.IsMatch(variables.cpkey);

                string cpufile = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(variables.filename1), "cpukey.txt");
                if (File.Exists(cpufile) && !(variables.cpkey.Length == 32 && sts))
                {
                    variables.cpkey = parsecpukey(cpufile);
                }

                if (variables.cpkey.Length != 32 || !objAlphaPattern.IsMatch(variables.cpkey)) variables.cpkey = "";

                bool foundKey = !string.IsNullOrEmpty(variables.cpkey);
                bool gotKeyFromCrc = false;

                if (!foundKey)
                {
                    long filenameKvCrc = Nand.Nand.kvcrc(variables.filename1, true);

                    if (variables.debugme) Console.WriteLine("KV CRC: {0:X}", filenameKvCrc);
                    if (variables.debugme) Console.WriteLine("Searching Registry Entrys");
                    try
                    {
                        variables.cpkey = CpuKeyDB.getkey_s(filenameKvCrc, xPanel.getDataSet());
                        Dispatcher.BeginInvoke(new Action(() => cpuKeyTB.Text = variables.cpkey));
                        if (!string.IsNullOrEmpty(variables.cpkey)) gotKeyFromCrc = true;
                    }
                    catch (NullReferenceException ex) { Console.WriteLine(ex.ToString()); }
                }
                else Dispatcher.BeginInvoke(new Action(() => cpuKeyTB.Text = variables.cpkey));

                Console.WriteLine("Initializing {0}, please wait...", System.IO.Path.GetFileName(variables.filename1));
                nandInfo.change_tab();
                updateProgress(mainmainProgressBar.Maximum / 2);
                nand = new Nand.PrivateN(variables.filename1, variables.cpkey);
                if (!nand.ok) return;

                if (variables.debugme) Console.WriteLine("N Key: {0}, V Key: {1}", nand._cpukey, variables.cpkey);

                if (!foundKey && gotKeyFromCrc)
                {
                    if (variables.debugme) Console.WriteLine("Found key in registry");
                    nand.cpukeyverification(variables.cpkey);
                    if (variables.debugme) Console.WriteLine("allmove ", variables.allmove);
                    if (variables.debugme) Console.WriteLine(!variables.filename1.Contains(nand.ki.serial));
                    if (variables.debugme) Console.WriteLine(variables.filename1.Contains(variables.outfolder));
                    if ((variables.allmove) && (!variables.filename1.Contains(nand.ki.serial)) && (variables.filename1.Contains(variables.outfolder)))
                    {
                        if (!movedalready)
                        {
                            Thread Go = new Thread(movework);
                            Go.Start();
                            movedalready = true;
                        }
                    }
                }
                else if (foundKey)
                {
                    if (!CpuKeyDB.getkey_s(variables.cpkey, xPanel.getDataSet()))
                    {
                        if (variables.debugme) Console.WriteLine("Key verification");
                        if (nand.cpukeyverification(variables.cpkey))
                        {
                            Console.WriteLine("CPU Key is Correct");
                            if (variables.debugme) Console.WriteLine("Adding key to registry");
                            CpuKeyDB.regentries entry = new CpuKeyDB.regentries();
                            entry.kvcrc = nand.kvcrc().ToString("X");
                            entry.serial = nand.ki.serial;
                            entry.cpukey = variables.cpkey;
                            entry.extra = Nand.Nand.getConsoleName(nand, variables.flashconfig);
                            entry.dvdkey = nand.ki.dvdkey;
                            entry.osig = nand.ki.osig;
                            entry.region = nand.ki.region;

                            bool reg = CpuKeyDB.addkey_s(entry, xPanel.getDataSet());
                            if (variables.autoExtract && reg)
                            {
                                if (variables.debugme) Console.WriteLine("Auto File Extraction Initiated");
                                extractFilesFromNand();

                            }
                            if (reg) nandInfo.show_cpukey_tab();
                            Dispatcher.BeginInvoke(new Action(() => cpuKeyTB.Text = variables.cpkey));
                            if ((!variables.filename1.Contains(nand.ki.serial)) && (variables.filename1.Contains(variables.outfolder)))
                            {
                                if (!movedalready)
                                {
                                    Thread Go = new Thread(movework);
                                    Go.Start();
                                    movedalready = true;
                                }
                            }
                        }
                        else Console.WriteLine("Wrong CPU Key");
                    }
                }

                nandInfo.setNand(nand);
                updateProgress((mainmainProgressBar.Maximum / 4) * 3); // 75%

                if (nand.ki.serial.Length > 0) // Reset XeFolder
                {
                    string xePath = System.IO.Path.Combine(Directory.GetParent(variables.outfolder).FullName, nand.ki.serial);
                    if (Directory.Exists(xePath)) variables.xefolder = xePath;
                    else variables.xefolder = "";
                }
                else variables.xefolder = "";

                variables.rgh1able = Nand.ntable.isGlitch1Able(nand.bl.CB_A);

                if (variables.debugme) Console.WriteLine("----------------------");
                variables.ctyp = variables.ctypes[0];
                variables.ctyp = Nand.Nand.getConsole(nand, variables.flashconfig);
                xPanel.setMBname(variables.ctyp.Text);
                variables.rghable = true;

                /////////////////////////

                switch (Nand.ntable.getHackfromCB(nand.bl.CB_A))
                {
                    case variables.hacktypes.glitch:
                        xPanel.BeginInvoke(new Action(() => xPanel.setRbtnGlitchChecked(true)));
                        break;
                    case variables.hacktypes.glitch2:
                        xPanel.BeginInvoke(new Action(() => xPanel.setRbtnGlitch2Checked(true)));
                        break;
                    case variables.hacktypes.jtag:
                        xPanel.BeginInvoke(new Action(() => xPanel.setRbtnJtagChecked(true)));
                        break;
                    case variables.hacktypes.devgl:
                        if (xPanel.canDevGL(variables.boardtype))
                            xPanel.BeginInvoke(new Action(() => xPanel.setRbtnDevGLChecked(true)));
                        else
                            xPanel.BeginInvoke(new Action(() => xPanel.setRbtnRetailChecked(true)));
                        break;
                    default:
                        xPanel.BeginInvoke(new Action(() => xPanel.setRbtnRetailChecked(true)));
                        break;
                }

                GC.Collect();

                FileStream fs = new FileStream(variables.filename1, FileMode.Open);
                try
                {
                    byte[] check_XL_USB = new byte[0x5B230];
                    if (nand.noecc)
                    {
                        fs.Position = 0x8BA00;
                        fs.Read(check_XL_USB, 0, 0x58600); // 0x8BA00 - 0xE4000
                    }
                    else
                    {
                        fs.Position = 0x8FFD0;
                        fs.Read(check_XL_USB, 0, 0x5B230); // 0x8FFD0 - 0xEB200
                        check_XL_USB = Nand.Nand.unecc(check_XL_USB);
                    }

                    byte[] patches = new byte[0x1000];

                    if (nand.bigblock)
                    {
                        for (int i = 0; i < patches.Length; i++)
                        {
                            patches[i] = check_XL_USB[0x54600 + 0x10 + i]; // BB, 0xE0000
                        }
                    }
                    else
                    {
                        for (int i = 0; i < patches.Length; i++)
                        {
                            patches[i] = check_XL_USB[0x34600 + 0x10 + i]; // 16MB, 0xC0000
                        }
                    }

                    // Needs to be run twice for JTAG checking, no reliable way to check which it is
                    Nand.PatchParser patchParser = new Nand.PatchParser(patches);
                    bool patchResult = patchParser.parseAll();

                    if (!patchResult)
                    {
                        patches = new byte[0x1000];

                        for (int i = 0; i < patches.Length; i++)
                        {
                            patches[i] = check_XL_USB[0x59F0 + i]; // JTAG all sizes, 0x913F0
                        }

                        patchParser.enterData(patches);
                        patchParser.parseAll();
                    }

                    check_XL_USB = null;
                }
                catch
                {
                    if (variables.debugme) Console.WriteLine("Could not check for patches");
                }

                fs.Close();
                fs.Dispose();

                variables.gotvalues = !String.IsNullOrEmpty(variables.cpkey);
                Console.WriteLine("Nand Initialization Finished");
                Console.WriteLine("");

                updateProgress(mainProgressBar.Maximum);

                if (variables.debugme)
                    Console.WriteLine("allmove ", variables.allmove);
                if (variables.debugme)
                    Console.WriteLine(!variables.filename1.Contains(nand.ki.serial));
                if (variables.debugme)
                    Console.WriteLine(variables.filename1.Contains(variables.outfolder));
                if ((variables.allmove) && (!variables.filename1.Contains(nand.ki.serial)) && (variables.filename1.Contains(variables.outfolder)))
                {
                    if (!movedalready)
                    {
                        Thread Go = new Thread(movework);
                        Go.Start();
                        movedalready = true;
                    }
                }
            }

            catch (SystemException ex)
            {
                Console.WriteLine("Nand Initialization Failed: {0}", ex.GetType().ToString());
                Console.WriteLine("The dump may be incomplete or corrupt");
                if (variables.debugme) Console.WriteLine(ex.ToString());
                Console.WriteLine("");
                updateProgress(mainProgressBar.Minimum);
                return;
            }

            GC.Collect();
        }

        string load_ecc()
        {
            if (System.IO.Path.GetExtension(variables.filename1) == ".bin")
            {
                variables.tempfile = variables.filename1;
            }

            if (xPanel.getRgh3Checked())
            {
                string mhz = "";
                if (xPanel.getRgh3Mhz() == 10) mhz = "_10";

                switch (variables.ctyp.ID)
                {
                    case 1:
                        variables.filename1 = System.IO.Path.Combine(variables.rootfolder, "common", "ECC", variables.RGH3_trinity + ".ecc");
                        break;
                    case 2:
                        variables.filename1 = System.IO.Path.Combine(variables.rootfolder, "common", "ECC", variables.RGH3_falcon + mhz + ".ecc");
                        break;
                    case 3:
                    case 4:
                    case 5:
                        variables.filename1 = System.IO.Path.Combine(variables.rootfolder, "common", "ECC", variables.RGH3_jasper + mhz + ".ecc");
                        break;
                    case 6:
                    case 7:
                        variables.filename1 = System.IO.Path.Combine(variables.rootfolder, "common", "ECC", variables.RGH3_jasperBB + mhz + ".ecc");
                        break;
                    case 8:
                    case 9:
                        variables.filename1 = System.IO.Path.Combine(variables.rootfolder, "common", "ECC", variables.RGH3_trinity + ".ecc");
                        break;
                    case 10:
                        variables.filename1 = System.IO.Path.Combine(variables.rootfolder, "common", "ECC", variables.RGH3_corona + ".ecc");
                        break;
                    case 11:
                        variables.filename1 = System.IO.Path.Combine(variables.rootfolder, "common", "ECC", variables.RGH3_corona4GB + ".ecc");
                        break;
                    default:
                        return "";
                }
            }
            else
            {
                string wb = "";
                string smcp = "";
                string cr4 = "";
                if (xPanel.getWBChecked() > 0) wb = "_WB";
                if (xPanel.getSMCPChecked()) smcp = "_SMC+";
                else if (xPanel.getCR4Checked()) cr4 = "_CR4";

                switch (variables.ctyp.ID)
                {
                    case 1:
                        variables.filename1 = System.IO.Path.Combine(variables.rootfolder, "common", "ECC", variables.RGX_trinity + cr4 + smcp + ".ecc");
                        break;
                    case 2:
                        variables.filename1 = System.IO.Path.Combine(variables.rootfolder, "common", "ECC", variables.RGX_falcon + cr4 + smcp + ".ecc");
                        break;
                    case 3:
                        variables.filename1 = System.IO.Path.Combine(variables.rootfolder, "common", "ECC", variables.RGX_falcon + cr4 + smcp + ".ecc"); // Use Falcon
                        Console.WriteLine("Using Falcon type for Zephyr");
                        break;
                    case 4:
                    case 5:
                    case 6:
                    case 7:
                        variables.filename1 = System.IO.Path.Combine(variables.rootfolder, "common", "ECC", variables.RGX_jasper + cr4 + smcp + ".ecc");
                        break;
                    case 8:
                        variables.filename1 = System.IO.Path.Combine(variables.rootfolder, "common", "ECC", variables.RGX_xenon + ".ecc"); // No CR4 or SMC+
                        break;
                    case 9:
                        variables.filename1 = System.IO.Path.Combine(variables.rootfolder, "common", "ECC", variables.RGX_falcon + cr4 + smcp + ".ecc");
                        break;
                    case 10:
                        variables.filename1 = System.IO.Path.Combine(variables.rootfolder, "common", "ECC", variables.RGX_corona + wb + cr4 + smcp + ".ecc");
                        break;
                    case 11:
                        variables.filename1 = System.IO.Path.Combine(variables.rootfolder, "common", "ECC", variables.RGX_corona4GB + wb + cr4 + smcp + ".ecc");
                        break;
                    default:
                        return "";
                }
            }
            nandSrcTB.Text = variables.filename1;
            return variables.filename1;
        }

        void createecc_v2()
        {
            //Thread.CurrentThread.Join();

            if (xPanel.getRbtnRetailChecked()) Console.WriteLine("You are creating an ecc image and you have selected {0}!", variables.ttyp);
            else if (xPanel.getRbtnJtagChecked()) Console.WriteLine("You are creating an ecc image and you have selected {0}!", variables.ttyp);
            //savedir();

            if (File.Exists(variables.filename1))
            {
                if (variables.debugme) Console.WriteLine("Filename1 = {0}", variables.filename1);
                if (Path.GetExtension(variables.filename1) == ".bin")
                {
                    variables.tempfile = variables.filename1;
                    mainProgressBar.Value = mainProgressBar.Minimum;
                    int result = 0;
                    try
                    {
                        bool sts = objAlphaPattern.IsMatch(cpuKeyTB.Text);

                        ECC ecc = new ECC();
                        result = ecc.creatergh2ecc(variables.filename1, variables.outfolder, ref this.mainProgressBar, cpuKeyTB.Text);
                        /*
                        if (comboRGH.SelectedIndex == 0)
                        {
                            result = Nand.createeccimage(variables.filename1, variables.outfolder, ref this.mainProgressBar1);
                        }
                        else
                        {
                            Console.WriteLine("Constructing an rgh2 ecc image");
                            if (sts) result = ECC.creatergh2(variables.filename1, variables.outfolder, ref this.mainProgressBar1, cpukeytext.Text);
                            else result = ECC.creatergh2(variables.filename1, variables.outfolder, ref this.mainProgressBar1);
                        }
                        */
                    }
                    catch (Exception ex) { if (variables.debugme) Console.WriteLine(ex.ToString()); }
                    if (result == 1)
                    {
                        variables.filename1 = System.IO.Path.Combine(variables.outfolder, "glitch.ecc");
                        nandSrcTB.Text = variables.filename1;
                    }
                    else if (result == 5)
                    {
                        mainProgressBar.Value = mainProgressBar.Maximum;
                    }
                    else
                    {
                        Console.WriteLine("Failed to create ecc image");
                        Console.WriteLine("");
                    }
                }
            }
        }

        void createxell()
        {
            if (String.IsNullOrWhiteSpace(variables.filename1))
            {
                loadfile(ref variables.filename1, ref this.nandSrcTB, true);
                if (String.IsNullOrWhiteSpace(variables.filename1))
                {
                    System.Windows.MessageBox.Show("No file was selected!", "Can't", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            if (variables.ctyp.ID == -1) return;
            if (File.Exists(variables.filename1))
            {
                variables.tempfile = variables.filename1;
                if (variables.debugme) Console.WriteLine("Filename1 = {0}", variables.filename1);
                if (System.IO.Path.GetExtension(variables.filename1) == ".bin")
                {
                    byte[] Keyraw = Nand.Nand.getrawkv(variables.filename1);
                    long size1 = 0;
                    string xellfile;
                    if (variables.ctyp.ID == 1) return;
                    else if (variables.ctyp.ID == 8) xellfile = "xenon.bin";
                    else if (variables.ctyp.ID == 2)
                    {
                        if (xPanel.getAudClampChecked()) xellfile = "falcon_aud_clamp.bin";
                        else xellfile = "falcon.bin";
                    }
                    else if (variables.ctyp.ID == 3)
                    {
                        if (xPanel.getAudClampChecked()) xellfile = "zephyr_aud_clamp.bin";
                        else xellfile = "zephyr.bin";
                    }
                    else if (variables.ctyp.ID == 4 || variables.ctyp.ID == 5)
                    {
                        if (xPanel.getAudClampChecked()) xellfile = "jasper_aud_clamp.bin";
                        else xellfile = "jasper.bin";
                    }
                    else if (variables.ctyp.ID == 6 || variables.ctyp.ID == 7)
                    {
                        if (xPanel.getAudClampChecked()) xellfile = "jasper_bb_aud_clamp.bin";
                        else xellfile = "jasper_bb.bin";
                    }
                    else return;
                    if (variables.debugme) Console.WriteLine(xellfile);


                    byte[] xellous = Oper.openfile(Path.Combine(variables.rootfolder, "common\\xell\\" + xellfile), ref size1, 0);
                    if (variables.debugme) Console.WriteLine("{0} file loaded successfully", xellfile);
                    if (variables.debugme) Console.WriteLine("{0:X} | {1:X}", xellous.Length, Keyraw.Length);

                    Buffer.BlockCopy(Keyraw, 0, xellous, 0x4200, 0x4200);

                    if (xPanel.getRJtagChecked())
                    {
                        int layout = 0;
                        if (variables.ctyp.ID == 6 || variables.ctyp.ID == 7) layout = 2;
                        else if (variables.ctyp.ID == 4 || variables.ctyp.ID == 5) layout = 1;
                        byte[] SMC;
                        byte[] smc_len = new byte[4], smc_start = new byte[4];
                        Buffer.BlockCopy(xellous, 0x78, smc_len, 0, 4);
                        Buffer.BlockCopy(xellous, 0x7C, smc_start, 0, 4);
                        SMC = new byte[Oper.ByteArrayToInt(smc_len)];
                        Buffer.BlockCopy(Nand.Nand.unecc(xellous), Oper.ByteArrayToInt(smc_start), SMC, 0, Oper.ByteArrayToInt(smc_len));
                        SMC = Nand.Nand.addecc_v2(Nand.Nand.encrypt_SMC(Nand.Nand.patch_SMC(Nand.Nand.decrypt_SMC(SMC))), true, 0, layout);
                        Buffer.BlockCopy(SMC, 0, xellous, (Oper.ByteArrayToInt(smc_start) / 0x200) * 0x210, (Oper.ByteArrayToInt(smc_len) / 0x200) * 0x210);
                    }

                    variables.filename1 = Path.Combine(variables.outfolder, xellfile);
                    if (variables.debugme) Console.WriteLine(variables.filename1);
                    Oper.savefile(xellous, variables.filename1);
                    if (variables.debugme) Console.WriteLine("Saved Successfully");
                    nandSrcTB.Text = variables.filename1;
                    Console.WriteLine("XeLL file created successfully {0}", xellfile);
                    Console.WriteLine("");
                }
            }
        }

        public void extractFilesFromNand()
        {
            if (!nand.ok)
            {
                System.Windows.MessageBox.Show("No nand loaded in source", "Can't", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Console.WriteLine("Extracting Files...");
            string tmpout = "";
            if (variables.modder && variables.custname != "")
            {
                tmpout = System.IO.Path.Combine(getCurrentWorkingFolder(), "Extracts-" + variables.custname);
            }
            else
            {
                tmpout = System.IO.Path.Combine(getCurrentWorkingFolder(), "Extracts-" + nand.ki.serial);
            }

            if (Directory.Exists(tmpout) == false)
            {
                Directory.CreateDirectory(tmpout);
            }

            Console.WriteLine("Saving SMC_en.bin");
            Oper.savefile(Nand.Nand.encrypt_SMC(nand._smc), System.IO.Path.Combine(tmpout, "SMC_en.bin"));
            Console.WriteLine("Saving SMC_dec.bin");
            Oper.savefile(nand._smc, System.IO.Path.Combine(tmpout, "SMC_dec.bin"));
            Console.WriteLine("Saving KV_en.bin");
            Oper.savefile(nand._rawkv, System.IO.Path.Combine(tmpout, "KV_en.bin"));

            if (!String.IsNullOrEmpty(nand._cpukey))
            {
                Console.WriteLine("Saving KV_dec.bin");
                Oper.savefile(Nand.Nand.decryptkv(nand._rawkv, Oper.StringToByteArray(nand._cpukey)), System.IO.Path.Combine(tmpout, "KV_dec.bin"));
            }
            Console.WriteLine("Saving smc_config.bin");
            nand.getsmcconfig();
            Oper.savefile(nand._smc_config, System.IO.Path.Combine(tmpout, "smc_config.bin"));

            if (variables.ctyp.ID == 1 || variables.ctyp.ID == 10 || variables.ctyp.ID == 11)
            {
                byte[] t;
                Console.WriteLine("Working...");
                byte[] fcrt = nand.exctractFSfile("fcrt.bin");
                if (fcrt != null)
                {
                    Console.WriteLine("Saving fcrt_en.bin");
                    Oper.savefile(fcrt, System.IO.Path.Combine(tmpout, "fcrt_en.bin"));
                    byte[] fcrt_dec;
                    if (Nand.Nand.decrypt_fcrt(fcrt, Oper.StringToByteArray(nand._cpukey), out fcrt_dec))
                    {
                        Console.WriteLine("Saving fcrt_dec.bin");
                        File.WriteAllBytes(System.IO.Path.Combine(tmpout, "fcrt_dec.bin"), fcrt_dec);
                    }
                    t = responses(fcrt, Oper.StringToByteArray(nand._cpukey), nand.ki.dvdkey);

                    if (t != null)
                    {

                        Console.WriteLine("Saving C-R.bin");
                        File.WriteAllBytes(System.IO.Path.Combine(tmpout, "C-R.bin"), t);
                        Console.WriteLine("Saving key.bin");
                        File.WriteAllBytes(System.IO.Path.Combine(tmpout, "key.bin"), Oper.StringToByteArray(nand.ki.dvdkey));
                    }
                    else Console.WriteLine("Failed to create C-R.bin");
                }
                else Console.WriteLine("Failed to find fcrt.bin");
            }
            Console.WriteLine("Location: {0}", tmpout);
            Console.WriteLine("Done");
            Console.WriteLine("");
        }
        public static byte[] responses(byte[] fcrt, byte[] cpukey, string dvdkey = "")
        {
            byte[] fcrt_dec;
            if (Nand.Nand.decrypt_fcrt(fcrt, cpukey, out fcrt_dec))
            {
                byte[] rfct = new byte[0x1F6 * 0x13];
                Oper.removeByteArray(ref fcrt_dec, 0, 0x140);
                Random rnd = new Random();
                int[] randomNumbers = Enumerable.Range(0, 502).OrderBy(i => rnd.Next()).ToArray();
                int counter = 0;
                while (counter < (rfct.Length / 0x13))
                {
                    byte[] cr = Oper.returnportion(fcrt_dec, counter * 0x20, 0x20);
                    Oper.removeByteArray(ref cr, 2, 0x10 - 3);
                    Buffer.BlockCopy(cr, 0, rfct, randomNumbers[counter] * cr.Length, cr.Length);
                    counter++;
                }
                for (int i = 0; i < 0x1f6; i++)
                {
                    if (Oper.allsame(Oper.returnportion(fcrt_dec, i * 0x20, 0x10), 0x00)) continue;
                    for (int j = i + 1; j < 0x1f6; j++)
                    {
                        if (Oper.allsame(Oper.returnportion(fcrt_dec, j * 0x20, 0x10), 0x00)) continue;
                        if (rfct[i * 0x13] == rfct[j * 0x13] &&
                            rfct[(i * 0x13) + 1] == rfct[(j * 0x13) + 1] &&
                            rfct[(i * 0x13) + 2] == rfct[(j * 0x13) + 2])
                        {
                            if (variables.debugme) Console.WriteLine("You're FUCKED");
                        }
                    }
                }
                return encryptFirmware(rfct, variables.xor, rfct.Length);
            }
            return null;
        }
        private static byte swapBits(byte chunk, int[] bits)
        {
            byte result = 0;
            //var bit = (b & (1 << bits[i])) != 0;
            int i;
            for (i = 0; i < 8; i++)
            {
                byte bit = (byte)((chunk & (1 << bits[i])) >> bits[i]);
                result = (byte)((result << 1) | bit);
            }
            return result;
        }
        private static byte[] encryptFirmware(byte[] inputBuffer, byte[] XorList, int size)
        {
            int[] encryptBits = { 3, 2, 7, 6, 1, 0, 5, 4 };
            int i;
            byte bt, done;
            byte[] outputBuffer = new byte[size];
            for (i = 0; i < size; i++)
            {
                bt = (byte)(inputBuffer[i] ^ XorList[i]);
                done = swapBits(bt, encryptBits);
                outputBuffer[i] = done;
            }
            return outputBuffer;
        }

        public void createDonor(string con, string hack, string smc, string cpuk, string kvPath, string fcrtPath, string smcConfPath, int ldv, bool nofcrt)
        {
            newSession(true);

            Console.WriteLine("=======================");
            Console.WriteLine("Starting Donor Nand Creation");

            // Set Clean SMC if needed
            if (hack == "Retail" || hack == "Glitch" || hack == "DEVGL") xPanel.setCleanSMCChecked(true);
            if (hack == "Glitch2" || hack == "Glitch2m")
            {
                if (smc == "Glitch") xPanel.setCleanSMCChecked(true);
            }

            variables.cpkey = cpuk; // Copy CPU Key
            Dispatcher.BeginInvoke(new Action(() => cpuKeyTB.Text = cpuk));
            variables.highldv = ldv; // Copy LDV
            variables.changeldv = 2; // Enable Custom LDV

            Thread donorThread = new Thread(() =>
            {
                try
                {
                    Console.WriteLine("Copying Files Into Place...");

                    if (File.Exists(variables.xePath + "nanddump.bin")) File.Delete(variables.xePath + "nanddump.bin"); // Just in case

                    // Copy KV
                    if (kvPath == "donor")
                    {
                        string kv;
                        if (con.Contains("Trinity") || con.Contains("Corona")) kv = "slim_nofcrt";
                        else if (con.Contains("Xenon")) kv = "phat_t1";
                        else kv = "phat_t2";
                        File.Copy(System.IO.Path.Combine(variables.donorPath, kv + ".bin"), variables.xePath + "KV.bin", true);
                    }
                    else File.Copy(kvPath, variables.xePath + "KV.bin", true);
                    Console.WriteLine("Copied KV.bin");

                    // Copy FCRT and set nofcrt if needed
                    if (fcrtPath != "unneeded")
                    {
                        if (fcrtPath == "donor") File.Copy(System.IO.Path.Combine(variables.donorPath, "fcrt.bin"), variables.xePath + "fcrt.bin", true);
                        else File.Copy(fcrtPath, variables.xePath + "fcrt.bin", true);
                        xPanel.setNoFcrt(nofcrt);
                        Console.WriteLine("Copied fcrt.bin");
                    }
                    else
                    {
                        if (File.Exists(variables.xePath + "fcrt.bin")) File.Delete(variables.xePath + "fcrt.bin");
                        xPanel.setNoFcrt(false);
                    }

                    // Copy SMC - only needed for RGH3
                    if ((hack == "Glitch2" || hack == "Glitch2m") && smc == "RGH3")
                    {
                        if (con.Contains("Corona")) File.Copy(variables.xePath + "CORONA_CLEAN.bin", variables.xePath + "SMC.bin", true);
                        else if (con.Contains("Trinity")) File.Copy(variables.xePath + "TRINITY_CLEAN.bin", variables.xePath + "SMC.bin", true);
                        else if (con.Contains("Jasper")) File.Copy(variables.xePath + "JASPER_CLEAN.bin", variables.xePath + "SMC.bin", true);
                        else if (con.Contains("Falcon")) File.Copy(variables.xePath + "FALCON_CLEAN.bin", variables.xePath + "SMC.bin", true);
                        else if (con.Contains("Zephyr")) File.Copy(variables.xePath + "ZEPHYR_CLEAN.bin", variables.xePath + "SMC.bin", true); // Just in case we ever re-use this code for non RGH3
                        else if (con.Contains("Xenon")) File.Copy(variables.xePath + "XENON_CLEAN.bin", variables.xePath + "SMC.bin", true); // Just in case we ever re-use this code for non RGH3
                        Console.WriteLine("Copied SMC.bin");
                    }

                    // Copy SMC Config
                    if (smcConfPath == "donor")
                    {
                        string smcConfig;

                        // Catch all types
                        if (con.Contains("Corona")) smcConfig = "Corona";
                        else if (con.Contains("Jasper")) smcConfig = "Jasper";
                        else if (con.Contains("Trinity")) smcConfig = "Trinity";
                        else smcConfig = con;

                        File.Copy(System.IO.Path.Combine(variables.donorPath, "smc_config", smcConfig + ".bin"), variables.xePath + "smc_config.bin", true);
                    }
                    else File.Copy(smcConfPath, variables.xePath + "smc_config.bin", true);
                    Console.WriteLine("Copied smc_config.bin");

                    // Launch XeBuild
                    Thread.Sleep(1000);
                    nand = new Nand.PrivateN();
                    nand._cpukey = cpuKeyTB.Text;
                    string kvfile = System.IO.Path.Combine(variables.rootfolder, @"xebuild\data\kv.bin");
                    if (File.Exists(kvfile))
                    {
                        nand._rawkv = File.ReadAllBytes(kvfile);
                        nand.updatekvval();
                    }
                    xPanel.createxebuild_v2(true, nand, true);
                }
                catch
                {
                    Console.WriteLine("Donor Nand Creation Failed");
                    Console.WriteLine("");
                    return;
                }
            });
            donorThread.Start();
        }

        private void createDonorAdvanced()
        {
            newSession(true);
            nand = new Nand.PrivateN();
            nand._cpukey = cpuKeyTB.Text;
            string kvfile = System.IO.Path.Combine(variables.rootfolder, @"xebuild\data\kv.bin");
            if (File.Exists(kvfile))
            {
                nand._rawkv = File.ReadAllBytes(kvfile);
                nand.updatekvval();

            }
            ThreadStart starter = delegate { xPanel.createxebuild_v2(true, nand, false); };
            new Thread(starter).Start();
        }

        public void startKvDecrypt(string path, string key)
        {
            Thread decryptThread = new Thread(() =>
            {
                try
                {
                    if (File.Exists(path))
                    {
                        Console.WriteLine("Decrypting Keyvault...");
                        byte[] data = Nand.Nand.decryptkv(File.ReadAllBytes(path), Oper.StringToByteArray(key));
                        Thread.Sleep(250);
                        if (data != null)
                        {
                            string outPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path), System.IO.Path.GetFileNameWithoutExtension(path).Replace("_en", "") + "_dec" + System.IO.Path.GetExtension(path));
                            Oper.savefile(data, outPath);
                            Console.WriteLine("Decrypted Successfully: " + outPath);
                        }
                        else Console.WriteLine("Decrypt Failed");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Decrypt Failed");
                    Console.WriteLine(ex.Message);
                }
            });
            decryptThread.Start();
        }
        #endregion

        #region General device interactions with UI

        public void afterWriteEccCleanup()
        {
            if (variables.tempfile != "")
            {
                variables.filename1 = variables.tempfile;
                nandSrcTB.Text = variables.tempfile;
            }
        }

        #endregion

        #region xFlasher interactions with UI

        public void xFlasherInitNand(int i = 2)
        {
            if (i == 2 && File.Exists(variables.filename))
            {
                Dispatcher.BeginInvoke((Action)(() => nandSrcTB.Text = System.IO.Path.Combine(variables.filename)));
                variables.filename1 = variables.filename;
                nand_init();
            }
            if (i == 3 && File.Exists(variables.filename))
            {
                Dispatcher.BeginInvoke((Action)(() => nandExtraTB.Text = System.IO.Path.Combine(variables.filename)));
                variables.filename2 = variables.filename;
                new Thread(comparenands).Start();
            }
        }

        public void xFlasherNandSelShow(int xfseltype, bool bigblock = false)
        {
            variables.xfSelType = xfseltype;
            xFlasherNandSel xfselform = new xFlasherNandSel();
            xfselform.TopMost = true;
            xfselform.SizeClick += xFlasherSizeClick;
            xfselform.BigBlock(bigblock);
            xfselform.Show();
        }

        void xFlasherSizeClick(int size)
        {
            if (variables.xfSelType == 1)
            {
                xflasher.readNandAuto(size, nTools.getNumericIterations(), true);
                variables.xfSelType = 0;
            }
            else if (variables.xfSelType == 2)
            {
                xflasher.writeNand(size, variables.filename1);
                variables.xfSelType = 0;
            }
        }

        public void xFlasherBusy(int mode)
        {
            if (mode > 0)
            {
                Dispatcher.BeginInvoke(new Action(() => {
                    if (mode == 3) progressBarLabel.Content = "Erasing";
                    else if (mode == 2) progressBarLabel.Content = "Writing";
                    else if (mode == 1) progressBarLabel.Content = "Reading";
                }));
                //Dispatcher.BeginInvoke(new Action(() => mainmainProgressBar.Style = mainProgressBarStyle.Blocks));
            }
            else if (mode == -2)
            {
                Dispatcher.BeginInvoke(new Action(() => progressBarLabel.IsIndeterminate = true));
            }
            else if (mode == -1)
            {
                Dispatcher.BeginInvoke(new Action(() => progressBarLabel.Content = "Progress"));
                Dispatcher.BeginInvoke(new Action(() => {
                    mainProgressBar.Value = mainProgressBar.Minimum;
                }));
                Dispatcher.BeginInvoke(new Action(() => txtBlocks.Content = ""));
            }
            else
            {
                Dispatcher.BeginInvoke(new Action(() => progressBarLabel.Content = "Progress"));
                Dispatcher.BeginInvoke(new Action(() => {
                    mainProgressBar.Value = mainProgressBar.Maximum;
                }));
                Dispatcher.BeginInvoke(new Action(() => { txtBlocks.Content = ""; }));
            }
        }

        public void xFlasherBlocksUpdate(string str, int progress)
        {
            if (xflasher.inUse)
            {
                Dispatcher.BeginInvoke((Action)(() => txtBlocks.Content = str));
                if (progress >= 0) Dispatcher.BeginInvoke((Action)(() => mainProgressBar.Value = progress)); // Just in case
                else Dispatcher.BeginInvoke((Action)(() => mainProgressBar.Value = 0));
            }
        }

        #endregion

        #region PicoFlasher interactions with UI
        public void PicoFlasherInitNand(int idx)
        {
            if (idx == 0 && File.Exists(variables.filename))
            {
                Dispatcher.BeginInvoke((Action)(() => nandSrcTB.Text = System.IO.Path.Combine(variables.filename)));
                variables.filename1 = variables.filename;
                nand_init();
            }
            if (idx == 1 && File.Exists(variables.filename))
            {
                Dispatcher.BeginInvoke((Action)(() => nandExtraTB.Text = System.IO.Path.Combine(variables.filename)));
                variables.filename2 = variables.filename;
                new Thread(comparenands).Start();
            }
        }

        public void PicoFlasherBusy(int mode)
        {
            if (mode > 0)
            {
                Dispatcher.BeginInvoke(new Action(() => {
                    if (mode == 3) mainProgressBarLabel.Content = "Erasing";
                    else if (mode == 2) mainProgressBarLabel.Content = "Writing";
                    else if (mode == 1) mainProgressBarLabel.Content = "Reading";
                }));
                //Dispatcher.BeginInvoke(new Action(() => { mainProgressBar.Style = mainProgressBarStyle.Blocks; }));
            }
            else if (mode == -2)
            {
                Dispatcher.BeginInvoke(new Action(() => { mainmainProgressBar.IsIndeterminate = true; }));
            }
            else if (mode == -1)
            {
                Dispatcher.BeginInvoke(new Action(() => { mainProgressBarLabel.Content = "Progress"; }));
                Dispatcher.BeginInvoke(new Action(() => {
                    //mainProgressBar.Style = mainProgressBarStyle.Blocks;
                    mainmainProgressBar.Value = mainmainProgressBar.Minimum;
                }));
                Dispatcher.BeginInvoke(new Action(() => { txtBlocks.Content = ""; }));
            }
            else
            {
                Dispatcher.BeginInvoke(new Action(() => { mainProgressBarLabel.Content = "Progress"; }));
                Dispatcher.BeginInvoke(new Action(() => {
                    //mainProgressBar.Style = mainProgressBarStyle.Blocks;
                    mainmainProgressBar.Value = mainmainProgressBar.Maximum;
                }));
                Dispatcher.BeginInvoke(new Action(() => { txtBlocks.Content = ""; }));
            }
        }

        public void PicoFlasherBlocksUpdate(string str, int progress)
        {
            if (picoflasher.InUse)
            {
                Dispatcher.BeginInvoke((Action)(() => txtBlocks.Content = str));
                if (progress >= 0)
                    Dispatcher.BeginInvoke((Action)(() => mainProgressBar.Value = progress));
                else
                    Dispatcher.BeginInvoke((Action)(() => mainProgressBar.Value = 0));
            }
        }
        #endregion

        #region Matrix Flasher interactions with UI

        public void mtxBusy(int mode)
        {
            if (mode > 0)
            {
                progressBarLabel.Content = "Writing";
                Dispatcher.BeginInvoke((Action)(() => mainProgressBar.IsIndeterminate = true));
                txtBlocks.Content = "";
            }
            else
            {
                progressBarLabel.Content = "Progress";
                //Dispatcher.BeginInvoke((Action)(() => mainmainProgressBar.Style = mainProgressBarStyle.Blocks));
                Dispatcher.BeginInvoke((Action)(() => mainProgressBar.Value = mainProgressBar.Maximum));
                txtBlocks.Content = "";
            }
        }

        #endregion

        #region Flags
        [Flags]
        public enum WindowMessage : uint
        {
            WM_NULL = 0x0,
            WM_CREATE = 0x0001,
            WM_DESTROY = 0x0002,
            WM_MOVE = 0x0003,
            WM_SIZE = 0x0005,
            WM_ACTIVATE = 0x0006,
            WM_SETFOCUS = 0x0007,
            WM_KILLFOCUS = 0x0008,
            WM_ENABLE = 0x000a,
            WM_SETREDRAW = 0x000b,
            WM_SETTEXT = 0x000c,
            WM_GETTEXT = 0x000d,
            WM_GETTEXTLENGTH = 0x000e,
            WM_PAINT = 0x000f,
            WM_CLOSE = 0x0010,
            WM_QUERYENDSESSION = 0x0011,
            WM_QUIT = 0x0012,
            WM_QUERYOPEN = 0x0013,
            WM_ERASEBKGND = 0x0014,
            WM_SYSCOLORCHANGE = 0x0015,
            WM_ENDSESSION = 0x0016,
            WM_SHOWWINDOW = 0x0018,
            WM_CTLCOLOR = 0x0019,
            WM_WININICHANGE = 0x001a,
            WM_DEVMODECHANGE = 0x001b,
            WM_ACTIVATEAPP = 0x001c,
            WM_FONTCHANGE = 0x001d,
            WM_TIMECHANGE = 0x001e,
            WM_CANCELMODE = 0x001f,
            WM_SETCURSOR = 0x0020,
            WM_MOUSEACTIVATE = 0x0021,
            WM_CHILDACTIVATE = 0x0022,
            WM_QUEUESYNC = 0x0023,
            WM_GETMINMAXINFO = 0x0024,
            WM_PAINTICON = 0x0026,
            WM_ICONERASEBKGND = 0x0027,
            WM_NEXTDLGCTL = 0x0028,
            WM_SPOOLERSTATUS = 0x002a,
            WM_DRAWITEM = 0x002b,
            WM_MEASUREITEM = 0x002c,
            WM_DELETEITEM = 0x002d,
            WM_VKEYTOITEM = 0x002e,
            WM_CHARTOITEM = 0x002f,
            WM_SETFONT = 0x0030,
            WM_GETFONT = 0x0031,
            WM_SETHOTKEY = 0x0032,
            WM_GETHOTKEY = 0x0033,
            WM_QUERYDRAGICON = 0x0037,
            WM_COMPAREITEM = 0x0039,
            WM_GETOBJECT = 0x003d,
            WM_COMPACTING = 0x0041,
            WM_COMMNOTIFY = 0x0044,
            WM_WINDOWPOSCHANGING = 0x0046,
            WM_WINDOWPOSCHANGED = 0x0047,
            WM_POWER = 0x0048,
            WM_COPYGLOBALDATA = 0x0049,
            WM_COPYDATA = 0x004a,
            WM_CANCELJOURNAL = 0x004b,
            WM_NOTIFY = 0x004e,
            WM_INPUTLANGCHANGEREQUEST = 0x0050,
            WM_INPUTLANGCHANGE = 0x0051,
            WM_TCARD = 0x0052,
            WM_HELP = 0x0053,
            WM_USERCHANGED = 0x0054,
            WM_NOTIFYFORMAT = 0x0055,
            WM_CONTEXTMENU = 0x007b,
            WM_STYLECHANGING = 0x007c,
            WM_STYLECHANGED = 0x007d,
            WM_DISPLAYCHANGE = 0x007e,
            WM_GETICON = 0x007f,
            WM_SETICON = 0x0080,
            WM_NCCREATE = 0x0081,
            WM_NCDESTROY = 0x0082,
            WM_NCCALCSIZE = 0x0083,
            WM_NCHITTEST = 0x0084,
            WM_NCPAINT = 0x0085,
            WM_NCACTIVATE = 0x0086,
            WM_GETDLGCODE = 0x0087,
            WM_SYNCPAINT = 0x0088,
            WM_NCMOUSEMOVE = 0x00a0,
            WM_NCLBUTTONDOWN = 0x00a1,
            WM_NCLBUTTONUP = 0x00a2,
            WM_NCLBUTTONDBLCLK = 0x00a3,
            WM_NCRBUTTONDOWN = 0x00a4,
            WM_NCRBUTTONUP = 0x00a5,
            WM_NCRBUTTONDBLCLK = 0x00a6,
            WM_NCMBUTTONDOWN = 0x00a7,
            WM_NCMBUTTONUP = 0x00a8,
            WM_NCMBUTTONDBLCLK = 0x00a9,
            WM_NCXBUTTONDOWN = 0x00ab,
            WM_NCXBUTTONUP = 0x00ac,
            WM_NCXBUTTONDBLCLK = 0x00ad,
            SBM_SETPOS = 0x00e0,
            SBM_GETPOS = 0x00e1,
            SBM_SETRANGE = 0x00e2,
            SBM_GETRANGE = 0x00e3,
            SBM_ENABLE_ARROWS = 0x00e4,
            SBM_SETRANGEREDRAW = 0x00e6,
            SBM_SETSCROLLINFO = 0x00e9,
            SBM_GETSCROLLINFO = 0x00ea,
            SBM_GETSCROLLBARINFO = 0x00eb,
            WM_INPUT = 0x00ff,
            WM_KEYDOWN = 0x0100,
            WM_KEYFIRST = 0x0100,
            WM_KEYUP = 0x0101,
            WM_CHAR = 0x0102,
            WM_DEADCHAR = 0x0103,
            WM_SYSKEYDOWN = 0x0104,
            WM_SYSKEYUP = 0x0105,
            WM_SYSCHAR = 0x0106,
            WM_SYSDEADCHAR = 0x0107,
            WM_KEYLAST = 0x0108,
            WM_WNT_CONVERTREQUESTEX = 0x0109,
            WM_CONVERTREQUEST = 0x010a,
            WM_CONVERTRESULT = 0x010b,
            WM_INTERIM = 0x010c,
            WM_IME_STARTCOMPOSITION = 0x010d,
            WM_IME_ENDCOMPOSITION = 0x010e,
            WM_IME_COMPOSITION = 0x010f,
            WM_IME_KEYLAST = 0x010f,
            WM_INITDIALOG = 0x0110,
            WM_COMMAND = 0x0111,
            WM_SYSCOMMAND = 0x0112,
            WM_TIMER = 0x0113,
            WM_HSCROLL = 0x0114,
            WM_VSCROLL = 0x0115,
            WM_INITMENU = 0x0116,
            WM_INITMENUPOPUP = 0x0117,
            WM_SYSTIMER = 0x0118,
            WM_MENUSELECT = 0x011f,
            WM_MENUCHAR = 0x0120,
            WM_ENTERIDLE = 0x0121,
            WM_MENURBUTTONUP = 0x0122,
            WM_MENUDRAG = 0x0123,
            WM_MENUGETOBJECT = 0x0124,
            WM_UNINITMENUPOPUP = 0x0125,
            WM_MENUCOMMAND = 0x0126,
            WM_CHANGEUISTATE = 0x0127,
            WM_UPDATEUISTATE = 0x0128,
            WM_QUERYUISTATE = 0x0129,
            WM_CTLCOLORMSGBOX = 0x0132,
            WM_CTLCOLOREDIT = 0x0133,
            WM_CTLCOLORLISTBOX = 0x0134,
            WM_CTLCOLORBTN = 0x0135,
            WM_CTLCOLORDLG = 0x0136,
            WM_CTLCOLORSCROLLBAR = 0x0137,
            WM_CTLCOLORSTATIC = 0x0138,
            WM_MOUSEFIRST = 0x0200,
            WM_MOUSEMOVE = 0x0200,
            WM_LBUTTONDOWN = 0x0201,
            WM_LBUTTONUP = 0x0202,
            WM_LBUTTONDBLCLK = 0x0203,
            WM_RBUTTONDOWN = 0x0204,
            WM_RBUTTONUP = 0x0205,
            WM_RBUTTONDBLCLK = 0x0206,
            WM_MBUTTONDOWN = 0x0207,
            WM_MBUTTONUP = 0x0208,
            WM_MBUTTONDBLCLK = 0x0209,
            WM_MOUSELAST = 0x0209,
            WM_MOUSEWHEEL = 0x020a,
            WM_XBUTTONDOWN = 0x020b,
            WM_XBUTTONUP = 0x020c,
            WM_XBUTTONDBLCLK = 0x020d,
            WM_PARENTNOTIFY = 0x0210,
            WM_ENTERMENULOOP = 0x0211,
            WM_EXITMENULOOP = 0x0212,
            WM_NEXTMENU = 0x0213,
            WM_SIZING = 0x0214,
            WM_CAPTURECHANGED = 0x0215,
            WM_MOVING = 0x0216,
            WM_POWERBROADCAST = 0x0218,
            WM_DEVICECHANGE = 0x0219,
            WM_MDICREATE = 0x0220,
            WM_MDIDESTROY = 0x0221,
            WM_MDIACTIVATE = 0x0222,
            WM_MDIRESTORE = 0x0223,
            WM_MDINEXT = 0x0224,
            WM_MDIMAXIMIZE = 0x0225,
            WM_MDITILE = 0x0226,
            WM_MDICASCADE = 0x0227,
            WM_MDIICONARRANGE = 0x0228,
            WM_MDIGETACTIVE = 0x0229,
            WM_MDISETMENU = 0x0230,
            WM_ENTERSIZEMOVE = 0x0231,
            WM_EXITSIZEMOVE = 0x0232,
            WM_DROPFILES = 0x0233,
            WM_MDIREFRESHMENU = 0x0234,
            WM_IME_REPORT = 0x0280,
            WM_IME_SETCONTEXT = 0x0281,
            WM_IME_NOTIFY = 0x0282,
            WM_IME_CONTROL = 0x0283,
            WM_IME_COMPOSITIONFULL = 0x0284,
            WM_IME_SELECT = 0x0285,
            WM_IME_CHAR = 0x0286,
            WM_IME_REQUEST = 0x0288,
            WM_IMEKEYDOWN = 0x0290,
            WM_IME_KEYDOWN = 0x0290,
            WM_IMEKEYUP = 0x0291,
            WM_IME_KEYUP = 0x0291,
            WM_NCMOUSEHOVER = 0x02a0,
            WM_MOUSEHOVER = 0x02a1,
            WM_NCMOUSELEAVE = 0x02a2,
            WM_MOUSELEAVE = 0x02a3,
            WM_CUT = 0x0300,
            WM_COPY = 0x0301,
            WM_PASTE = 0x0302,
            WM_CLEAR = 0x0303,
            WM_UNDO = 0x0304,
            WM_RENDERFORMAT = 0x0305,
            WM_RENDERALLFORMATS = 0x0306,
            WM_DESTROYCLIPBOARD = 0x0307,
            WM_DRAWCLIPBOARD = 0x0308,
            WM_PAINTCLIPBOARD = 0x0309,
            WM_VSCROLLCLIPBOARD = 0x030a,
            WM_SIZECLIPBOARD = 0x030b,
            WM_ASKCBFORMATNAME = 0x030c,
            WM_CHANGECBCHAIN = 0x030d,
            WM_HSCROLLCLIPBOARD = 0x030e,
            WM_QUERYNEWPALETTE = 0x030f,
            WM_PALETTEISCHANGING = 0x0310,
            WM_PALETTECHANGED = 0x0311,
            WM_HOTKEY = 0x0312,
            WM_PRINT = 0x0317,
            WM_PRINTCLIENT = 0x0318,
            WM_APPCOMMAND = 0x0319,
            WM_HANDHELDFIRST = 0x0358,
            WM_HANDHELDLAST = 0x035f,
            WM_AFXFIRST = 0x0360,
            WM_AFXLAST = 0x037f,
            WM_PENWINFIRST = 0x0380,
            WM_RCRESULT = 0x0381,
            WM_HOOKRCRESULT = 0x0382,
            WM_GLOBALRCCHANGE = 0x0383,
            WM_PENMISCINFO = 0x0383,
            WM_SKB = 0x0384,
            WM_HEDITCTL = 0x0385,
            WM_PENCTL = 0x0385,
            WM_PENMISC = 0x0386,
            WM_CTLINIT = 0x0387,
            WM_PENEVENT = 0x0388,
            WM_PENWINLAST = 0x038f,
            DDM_SETFMT = 0x0400,
            DM_GETDEFID = 0x0400,
            NIN_SELECT = 0x0400,
            TBM_GETPOS = 0x0400,
            WM_PSD_PAGESETUPDLG = 0x0400,
            WM_USER = 0x0400,
            CBEM_INSERTITEMA = 0x0401,
            DDM_DRAW = 0x0401,
            DM_SETDEFID = 0x0401,
            HKM_SETHOTKEY = 0x0401,
            PBM_SETRANGE = 0x0401,
            RB_INSERTBANDA = 0x0401,
            SB_SETTEXTA = 0x0401,
            TB_ENABLEBUTTON = 0x0401,
            TBM_GETRANGEMIN = 0x0401,
            TTM_ACTIVATE = 0x0401,
            WM_CHOOSEFONT_GETLOGFONT = 0x0401,
            WM_PSD_FULLPAGERECT = 0x0401,
            CBEM_SETIMAGELIST = 0x0402,
            DDM_CLOSE = 0x0402,
            DM_REPOSITION = 0x0402,
            HKM_GETHOTKEY = 0x0402,
            PBM_SETPOS = 0x0402,
            RB_DELETEBAND = 0x0402,
            SB_GETTEXTA = 0x0402,
            TB_CHECKBUTTON = 0x0402,
            TBM_GETRANGEMAX = 0x0402,
            WM_PSD_MINMARGINRECT = 0x0402,
            CBEM_GETIMAGELIST = 0x0403,
            DDM_BEGIN = 0x0403,
            HKM_SETRULES = 0x0403,
            PBM_DELTAPOS = 0x0403,
            RB_GETBARINFO = 0x0403,
            SB_GETTEXTLENGTHA = 0x0403,
            TBM_GETTIC = 0x0403,
            TB_PRESSBUTTON = 0x0403,
            TTM_SETDELAYTIME = 0x0403,
            WM_PSD_MARGINRECT = 0x0403,
            CBEM_GETITEMA = 0x0404,
            DDM_END = 0x0404,
            PBM_SETSTEP = 0x0404,
            RB_SETBARINFO = 0x0404,
            SB_SETPARTS = 0x0404,
            TB_HIDEBUTTON = 0x0404,
            TBM_SETTIC = 0x0404,
            TTM_ADDTOOLA = 0x0404,
            WM_PSD_GREEKTEXTRECT = 0x0404,
            CBEM_SETITEMA = 0x0405,
            PBM_STEPIT = 0x0405,
            TB_INDETERMINATE = 0x0405,
            TBM_SETPOS = 0x0405,
            TTM_DELTOOLA = 0x0405,
            WM_PSD_ENVSTAMPRECT = 0x0405,
            CBEM_GETCOMBOCONTROL = 0x0406,
            PBM_SETRANGE32 = 0x0406,
            RB_SETBANDINFOA = 0x0406,
            SB_GETPARTS = 0x0406,
            TB_MARKBUTTON = 0x0406,
            TBM_SETRANGE = 0x0406,
            TTM_NEWTOOLRECTA = 0x0406,
            WM_PSD_YAFULLPAGERECT = 0x0406,
            CBEM_GETEDITCONTROL = 0x0407,
            PBM_GETRANGE = 0x0407,
            RB_SETPARENT = 0x0407,
            SB_GETBORDERS = 0x0407,
            TBM_SETRANGEMIN = 0x0407,
            TTM_RELAYEVENT = 0x0407,
            CBEM_SETEXSTYLE = 0x0408,
            PBM_GETPOS = 0x0408,
            RB_HITTEST = 0x0408,
            SB_SETMINHEIGHT = 0x0408,
            TBM_SETRANGEMAX = 0x0408,
            TTM_GETTOOLINFOA = 0x0408,
            CBEM_GETEXSTYLE = 0x0409,
            CBEM_GETEXTENDEDSTYLE = 0x0409,
            PBM_SETBARCOLOR = 0x0409,
            RB_GETRECT = 0x0409,
            SB_SIMPLE = 0x0409,
            TB_ISBUTTONENABLED = 0x0409,
            TBM_CLEARTICS = 0x0409,
            TTM_SETTOOLINFOA = 0x0409,
            CBEM_HASEDITCHANGED = 0x040a,
            RB_INSERTBANDW = 0x040a,
            SB_GETRECT = 0x040a,
            TB_ISBUTTONCHECKED = 0x040a,
            TBM_SETSEL = 0x040a,
            TTM_HITTESTA = 0x040a,
            WIZ_QUERYNUMPAGES = 0x040a,
            CBEM_INSERTITEMW = 0x040b,
            RB_SETBANDINFOW = 0x040b,
            SB_SETTEXTW = 0x040b,
            TB_ISBUTTONPRESSED = 0x040b,
            TBM_SETSELSTART = 0x040b,
            TTM_GETTEXTA = 0x040b,
            WIZ_NEXT = 0x040b,
            CBEM_SETITEMW = 0x040c,
            RB_GETBANDCOUNT = 0x040c,
            SB_GETTEXTLENGTHW = 0x040c,
            TB_ISBUTTONHIDDEN = 0x040c,
            TBM_SETSELEND = 0x040c,
            TTM_UPDATETIPTEXTA = 0x040c,
            WIZ_PREV = 0x040c,
            CBEM_GETITEMW = 0x040d,
            RB_GETROWCOUNT = 0x040d,
            SB_GETTEXTW = 0x040d,
            TB_ISBUTTONINDETERMINATE = 0x040d,
            TTM_GETTOOLCOUNT = 0x040d,
            CBEM_SETEXTENDEDSTYLE = 0x040e,
            RB_GETROWHEIGHT = 0x040e,
            SB_ISSIMPLE = 0x040e,
            TB_ISBUTTONHIGHLIGHTED = 0x040e,
            TBM_GETPTICS = 0x040e,
            TTM_ENUMTOOLSA = 0x040e,
            SB_SETICON = 0x040f,
            TBM_GETTICPOS = 0x040f,
            TTM_GETCURRENTTOOLA = 0x040f,
            RB_IDTOINDEX = 0x0410,
            SB_SETTIPTEXTA = 0x0410,
            TBM_GETNUMTICS = 0x0410,
            TTM_WINDOWFROMPOINT = 0x0410,
            RB_GETTOOLTIPS = 0x0411,
            SB_SETTIPTEXTW = 0x0411,
            TBM_GETSELSTART = 0x0411,
            TB_SETSTATE = 0x0411,
            TTM_TRACKACTIVATE = 0x0411,
            RB_SETTOOLTIPS = 0x0412,
            SB_GETTIPTEXTA = 0x0412,
            TB_GETSTATE = 0x0412,
            TBM_GETSELEND = 0x0412,
            TTM_TRACKPOSITION = 0x0412,
            RB_SETBKCOLOR = 0x0413,
            SB_GETTIPTEXTW = 0x0413,
            TB_ADDBITMAP = 0x0413,
            TBM_CLEARSEL = 0x0413,
            TTM_SETTIPBKCOLOR = 0x0413,
            RB_GETBKCOLOR = 0x0414,
            SB_GETICON = 0x0414,
            TB_ADDBUTTONSA = 0x0414,
            TBM_SETTICFREQ = 0x0414,
            TTM_SETTIPTEXTCOLOR = 0x0414,
            RB_SETTEXTCOLOR = 0x0415,
            TB_INSERTBUTTONA = 0x0415,
            TBM_SETPAGESIZE = 0x0415,
            TTM_GETDELAYTIME = 0x0415,
            RB_GETTEXTCOLOR = 0x0416,
            TB_DELETEBUTTON = 0x0416,
            TBM_GETPAGESIZE = 0x0416,
            TTM_GETTIPBKCOLOR = 0x0416,
            RB_SIZETORECT = 0x0417,
            TB_GETBUTTON = 0x0417,
            TBM_SETLINESIZE = 0x0417,
            TTM_GETTIPTEXTCOLOR = 0x0417,
            RB_BEGINDRAG = 0x0418,
            TB_BUTTONCOUNT = 0x0418,
            TBM_GETLINESIZE = 0x0418,
            TTM_SETMAXTIPWIDTH = 0x0418,
            RB_ENDDRAG = 0x0419,
            TB_COMMANDTOINDEX = 0x0419,
            TBM_GETTHUMBRECT = 0x0419,
            TTM_GETMAXTIPWIDTH = 0x0419,
            RB_DRAGMOVE = 0x041a,
            TBM_GETCHANNELRECT = 0x041a,
            TB_SAVERESTOREA = 0x041a,
            TTM_SETMARGIN = 0x041a,
            RB_GETBARHEIGHT = 0x041b,
            TB_CUSTOMIZE = 0x041b,
            TBM_SETTHUMBLENGTH = 0x041b,
            TTM_GETMARGIN = 0x041b,
            RB_GETBANDINFOW = 0x041c,
            TB_ADDSTRINGA = 0x041c,
            TBM_GETTHUMBLENGTH = 0x041c,
            TTM_POP = 0x041c,
            RB_GETBANDINFOA = 0x041d,
            TB_GETITEMRECT = 0x041d,
            TBM_SETTOOLTIPS = 0x041d,
            TTM_UPDATE = 0x041d,
            RB_MINIMIZEBAND = 0x041e,
            TB_BUTTONSTRUCTSIZE = 0x041e,
            TBM_GETTOOLTIPS = 0x041e,
            TTM_GETBUBBLESIZE = 0x041e,
            RB_MAXIMIZEBAND = 0x041f,
            TBM_SETTIPSIDE = 0x041f,
            TB_SETBUTTONSIZE = 0x041f,
            TTM_ADJUSTRECT = 0x041f,
            TBM_SETBUDDY = 0x0420,
            TB_SETBITMAPSIZE = 0x0420,
            TTM_SETTITLEA = 0x0420,
            MSG_FTS_JUMP_VA = 0x0421,
            TB_AUTOSIZE = 0x0421,
            TBM_GETBUDDY = 0x0421,
            TTM_SETTITLEW = 0x0421,
            RB_GETBANDBORDERS = 0x0422,
            MSG_FTS_JUMP_QWORD = 0x0423,
            RB_SHOWBAND = 0x0423,
            TB_GETTOOLTIPS = 0x0423,
            MSG_REINDEX_REQUEST = 0x0424,
            TB_SETTOOLTIPS = 0x0424,
            MSG_FTS_WHERE_IS_IT = 0x0425,
            RB_SETPALETTE = 0x0425,
            TB_SETPARENT = 0x0425,
            RB_GETPALETTE = 0x0426,
            RB_MOVEBAND = 0x0427,
            TB_SETROWS = 0x0427,
            TB_GETROWS = 0x0428,
            TB_GETBITMAPFLAGS = 0x0429,
            TB_SETCMDID = 0x042a,
            RB_PUSHCHEVRON = 0x042b,
            TB_CHANGEBITMAP = 0x042b,
            TB_GETBITMAP = 0x042c,
            MSG_GET_DEFFONT = 0x042d,
            TB_GETBUTTONTEXTA = 0x042d,
            TB_REPLACEBITMAP = 0x042e,
            TB_SETINDENT = 0x042f,
            TB_SETIMAGELIST = 0x0430,
            TB_GETIMAGELIST = 0x0431,
            TB_LOADIMAGES = 0x0432,
            TTM_ADDTOOLW = 0x0432,
            TB_GETRECT = 0x0433,
            TTM_DELTOOLW = 0x0433,
            TB_SETHOTIMAGELIST = 0x0434,
            TTM_NEWTOOLRECTW = 0x0434,
            TB_GETHOTIMAGELIST = 0x0435,
            TTM_GETTOOLINFOW = 0x0435,
            TB_SETDISABLEDIMAGELIST = 0x0436,
            TTM_SETTOOLINFOW = 0x0436,
            TB_GETDISABLEDIMAGELIST = 0x0437,
            TTM_HITTESTW = 0x0437,
            TB_SETSTYLE = 0x0438,
            TTM_GETTEXTW = 0x0438,
            TB_GETSTYLE = 0x0439,
            TTM_UPDATETIPTEXTW = 0x0439,
            TB_GETBUTTONSIZE = 0x043a,
            TTM_ENUMTOOLSW = 0x043a,
            TB_SETBUTTONWIDTH = 0x043b,
            TTM_GETCURRENTTOOLW = 0x043b,
            TB_SETMAXTEXTROWS = 0x043c,
            TB_GETTEXTROWS = 0x043d,
            TB_GETOBJECT = 0x043e,
            TB_GETBUTTONINFOW = 0x043f,
            TB_SETBUTTONINFOW = 0x0440,
            TB_GETBUTTONINFOA = 0x0441,
            TB_SETBUTTONINFOA = 0x0442,
            TB_INSERTBUTTONW = 0x0443,
            TB_ADDBUTTONSW = 0x0444,
            TB_HITTEST = 0x0445,
            TB_SETDRAWTEXTFLAGS = 0x0446,
            TB_GETHOTITEM = 0x0447,
            TB_SETHOTITEM = 0x0448,
            TB_SETANCHORHIGHLIGHT = 0x0449,
            TB_GETANCHORHIGHLIGHT = 0x044a,
            TB_GETBUTTONTEXTW = 0x044b,
            TB_SAVERESTOREW = 0x044c,
            TB_ADDSTRINGW = 0x044d,
            TB_MAPACCELERATORA = 0x044e,
            TB_GETINSERTMARK = 0x044f,
            TB_SETINSERTMARK = 0x0450,
            TB_INSERTMARKHITTEST = 0x0451,
            TB_MOVEBUTTON = 0x0452,
            TB_GETMAXSIZE = 0x0453,
            TB_SETEXTENDEDSTYLE = 0x0454,
            TB_GETEXTENDEDSTYLE = 0x0455,
            TB_GETPADDING = 0x0456,
            TB_SETPADDING = 0x0457,
            TB_SETINSERTMARKCOLOR = 0x0458,
            TB_GETINSERTMARKCOLOR = 0x0459,
            TB_MAPACCELERATORW = 0x045a,
            TB_GETSTRINGW = 0x045b,
            TB_GETSTRINGA = 0x045c,
            TAPI_REPLY = 0x0463,
            ACM_OPENA = 0x0464,
            BFFM_SETSTATUSTEXTA = 0x0464,
            CDM_FIRST = 0x0464,
            CDM_GETSPEC = 0x0464,
            IPM_CLEARADDRESS = 0x0464,
            WM_CAP_UNICODE_START = 0x0464,
            ACM_PLAY = 0x0465,
            BFFM_ENABLEOK = 0x0465,
            CDM_GETFILEPATH = 0x0465,
            IPM_SETADDRESS = 0x0465,
            PSM_SETCURSEL = 0x0465,
            UDM_SETRANGE = 0x0465,
            WM_CHOOSEFONT_SETLOGFONT = 0x0465,
            ACM_STOP = 0x0466,
            BFFM_SETSELECTIONA = 0x0466,
            CDM_GETFOLDERPATH = 0x0466,
            IPM_GETADDRESS = 0x0466,
            PSM_REMOVEPAGE = 0x0466,
            UDM_GETRANGE = 0x0466,
            WM_CAP_SET_CALLBACK_ERRORW = 0x0466,
            WM_CHOOSEFONT_SETFLAGS = 0x0466,
            ACM_OPENW = 0x0467,
            BFFM_SETSELECTIONW = 0x0467,
            CDM_GETFOLDERIDLIST = 0x0467,
            IPM_SETRANGE = 0x0467,
            PSM_ADDPAGE = 0x0467,
            UDM_SETPOS = 0x0467,
            WM_CAP_SET_CALLBACK_STATUSW = 0x0467,
            BFFM_SETSTATUSTEXTW = 0x0468,
            CDM_SETCONTROLTEXT = 0x0468,
            IPM_SETFOCUS = 0x0468,
            PSM_CHANGED = 0x0468,
            UDM_GETPOS = 0x0468,
            CDM_HIDECONTROL = 0x0469,
            IPM_ISBLANK = 0x0469,
            PSM_RESTARTWINDOWS = 0x0469,
            UDM_SETBUDDY = 0x0469,
            CDM_SETDEFEXT = 0x046a,
            PSM_REBOOTSYSTEM = 0x046a,
            UDM_GETBUDDY = 0x046a,
            PSM_CANCELTOCLOSE = 0x046b,
            UDM_SETACCEL = 0x046b,
            EM_CONVPOSITION = 0x046c,
            PSM_QUERYSIBLINGS = 0x046c,
            UDM_GETACCEL = 0x046c,
            MCIWNDM_GETZOOM = 0x046d,
            PSM_UNCHANGED = 0x046d,
            UDM_SETBASE = 0x046d,
            PSM_APPLY = 0x046e,
            UDM_GETBASE = 0x046e,
            PSM_SETTITLEA = 0x046f,
            UDM_SETRANGE32 = 0x046f,
            PSM_SETWIZBUTTONS = 0x0470,
            UDM_GETRANGE32 = 0x0470,
            WM_CAP_DRIVER_GET_NAMEW = 0x0470,
            PSM_PRESSBUTTON = 0x0471,
            UDM_SETPOS32 = 0x0471,
            WM_CAP_DRIVER_GET_VERSIONW = 0x0471,
            PSM_SETCURSELID = 0x0472,
            UDM_GETPOS32 = 0x0472,
            PSM_SETFINISHTEXTA = 0x0473,
            PSM_GETTABCONTROL = 0x0474,
            PSM_ISDIALOGMESSAGE = 0x0475,
            MCIWNDM_REALIZE = 0x0476,
            PSM_GETCURRENTPAGEHWND = 0x0476,
            MCIWNDM_SETTIMEFORMATA = 0x0477,
            PSM_INSERTPAGE = 0x0477,
            MCIWNDM_GETTIMEFORMATA = 0x0478,
            PSM_SETTITLEW = 0x0478,
            WM_CAP_FILE_SET_CAPTURE_FILEW = 0x0478,
            MCIWNDM_VALIDATEMEDIA = 0x0479,
            PSM_SETFINISHTEXTW = 0x0479,
            WM_CAP_FILE_GET_CAPTURE_FILEW = 0x0479,
            MCIWNDM_PLAYTO = 0x047b,
            WM_CAP_FILE_SAVEASW = 0x047b,
            MCIWNDM_GETFILENAMEA = 0x047c,
            MCIWNDM_GETDEVICEA = 0x047d,
            PSM_SETHEADERTITLEA = 0x047d,
            WM_CAP_FILE_SAVEDIBW = 0x047d,
            MCIWNDM_GETPALETTE = 0x047e,
            PSM_SETHEADERTITLEW = 0x047e,
            MCIWNDM_SETPALETTE = 0x047f,
            PSM_SETHEADERSUBTITLEA = 0x047f,
            MCIWNDM_GETERRORA = 0x0480,
            PSM_SETHEADERSUBTITLEW = 0x0480,
            PSM_HWNDTOINDEX = 0x0481,
            PSM_INDEXTOHWND = 0x0482,
            MCIWNDM_SETINACTIVETIMER = 0x0483,
            PSM_PAGETOINDEX = 0x0483,
            PSM_INDEXTOPAGE = 0x0484,
            DL_BEGINDRAG = 0x0485,
            MCIWNDM_GETINACTIVETIMER = 0x0485,
            PSM_IDTOINDEX = 0x0485,
            DL_DRAGGING = 0x0486,
            PSM_INDEXTOID = 0x0486,
            DL_DROPPED = 0x0487,
            PSM_GETRESULT = 0x0487,
            DL_CANCELDRAG = 0x0488,
            PSM_RECALCPAGESIZES = 0x0488,
            MCIWNDM_GET_SOURCE = 0x048c,
            MCIWNDM_PUT_SOURCE = 0x048d,
            MCIWNDM_GET_DEST = 0x048e,
            MCIWNDM_PUT_DEST = 0x048f,
            MCIWNDM_CAN_PLAY = 0x0490,
            MCIWNDM_CAN_WINDOW = 0x0491,
            MCIWNDM_CAN_RECORD = 0x0492,
            MCIWNDM_CAN_SAVE = 0x0493,
            MCIWNDM_CAN_EJECT = 0x0494,
            MCIWNDM_CAN_CONFIG = 0x0495,
            IE_GETINK = 0x0496,
            IE_MSGFIRST = 0x0496,
            MCIWNDM_PALETTEKICK = 0x0496,
            IE_SETINK = 0x0497,
            IE_GETPENTIP = 0x0498,
            IE_SETPENTIP = 0x0499,
            IE_GETERASERTIP = 0x049a,
            IE_SETERASERTIP = 0x049b,
            IE_GETBKGND = 0x049c,
            IE_SETBKGND = 0x049d,
            IE_GETGRIDORIGIN = 0x049e,
            IE_SETGRIDORIGIN = 0x049f,
            IE_GETGRIDPEN = 0x04a0,
            IE_SETGRIDPEN = 0x04a1,
            IE_GETGRIDSIZE = 0x04a2,
            IE_SETGRIDSIZE = 0x04a3,
            IE_GETMODE = 0x04a4,
            IE_SETMODE = 0x04a5,
            IE_GETINKRECT = 0x04a6,
            WM_CAP_SET_MCI_DEVICEW = 0x04a6,
            WM_CAP_GET_MCI_DEVICEW = 0x04a7,
            WM_CAP_PAL_OPENW = 0x04b4,
            WM_CAP_PAL_SAVEW = 0x04b5,
            IE_GETAPPDATA = 0x04b8,
            IE_SETAPPDATA = 0x04b9,
            IE_GETDRAWOPTS = 0x04ba,
            IE_SETDRAWOPTS = 0x04bb,
            IE_GETFORMAT = 0x04bc,
            IE_SETFORMAT = 0x04bd,
            IE_GETINKINPUT = 0x04be,
            IE_SETINKINPUT = 0x04bf,
            IE_GETNOTIFY = 0x04c0,
            IE_SETNOTIFY = 0x04c1,
            IE_GETRECOG = 0x04c2,
            IE_SETRECOG = 0x04c3,
            IE_GETSECURITY = 0x04c4,
            IE_SETSECURITY = 0x04c5,
            IE_GETSEL = 0x04c6,
            IE_SETSEL = 0x04c7,
            CDM_LAST = 0x04c8,
            IE_DOCOMMAND = 0x04c8,
            MCIWNDM_NOTIFYMODE = 0x04c8,
            IE_GETCOMMAND = 0x04c9,
            IE_GETCOUNT = 0x04ca,
            IE_GETGESTURE = 0x04cb,
            MCIWNDM_NOTIFYMEDIA = 0x04cb,
            IE_GETMENU = 0x04cc,
            IE_GETPAINTDC = 0x04cd,
            MCIWNDM_NOTIFYERROR = 0x04cd,
            IE_GETPDEVENT = 0x04ce,
            IE_GETSELCOUNT = 0x04cf,
            IE_GETSELITEMS = 0x04d0,
            IE_GETSTYLE = 0x04d1,
            MCIWNDM_SETTIMEFORMATW = 0x04db,
            EM_OUTLINE = 0x04dc,
            MCIWNDM_GETTIMEFORMATW = 0x04dc,
            EM_GETSCROLLPOS = 0x04dd,
            EM_SETSCROLLPOS = 0x04de,
            EM_SETFONTSIZE = 0x04df,
            MCIWNDM_GETFILENAMEW = 0x04e0,
            MCIWNDM_GETDEVICEW = 0x04e1,
            MCIWNDM_GETERRORW = 0x04e4,
            FM_GETFOCUS = 0x0600,
            FM_GETDRIVEINFOA = 0x0601,
            FM_GETSELCOUNT = 0x0602,
            FM_GETSELCOUNTLFN = 0x0603,
            FM_GETFILESELA = 0x0604,
            FM_GETFILESELLFNA = 0x0605,
            FM_REFRESH_WINDOWS = 0x0606,
            FM_RELOAD_EXTENSIONS = 0x0607,
            FM_GETDRIVEINFOW = 0x0611,
            FM_GETFILESELW = 0x0614,
            FM_GETFILESELLFNW = 0x0615,
            WLX_WM_SAS = 0x0659,
            SM_GETSELCOUNT = 0x07e8,
            UM_GETSELCOUNT = 0x07e8,
            WM_CPL_LAUNCH = 0x07e8,
            SM_GETSERVERSELA = 0x07e9,
            UM_GETUSERSELA = 0x07e9,
            WM_CPL_LAUNCHED = 0x07e9,
            SM_GETSERVERSELW = 0x07ea,
            UM_GETUSERSELW = 0x07ea,
            SM_GETCURFOCUSA = 0x07eb,
            UM_GETGROUPSELA = 0x07eb,
            SM_GETCURFOCUSW = 0x07ec,
            UM_GETGROUPSELW = 0x07ec,
            SM_GETOPTIONS = 0x07ed,
            UM_GETCURFOCUSA = 0x07ed,
            UM_GETCURFOCUSW = 0x07ee,
            UM_GETOPTIONS = 0x07ef,
            UM_GETOPTIONS2 = 0x07f0,
            OCMBASE = 0x2000,
            OCM_CTLCOLOR = 0x2019,
            OCM_DRAWITEM = 0x202b,
            OCM_MEASUREITEM = 0x202c,
            OCM_DELETEITEM = 0x202d,
            OCM_VKEYTOITEM = 0x202e,
            OCM_CHARTOITEM = 0x202f,
            OCM_COMPAREITEM = 0x2039,
            OCM_NOTIFY = 0x204e,
            OCM_COMMAND = 0x2111,
            OCM_HSCROLL = 0x2114,
            OCM_VSCROLL = 0x2115,
            OCM_CTLCOLORMSGBOX = 0x2132,
            OCM_CTLCOLOREDIT = 0x2133,
            OCM_CTLCOLORLISTBOX = 0x2134,
            OCM_CTLCOLORBTN = 0x2135,
            OCM_CTLCOLORDLG = 0x2136,
            OCM_CTLCOLORSCROLLBAR = 0x2137,
            OCM_CTLCOLORSTATIC = 0x2138,
            OCM_PARENTNOTIFY = 0x2210,
            WM_APP = 0x8000,
            WM_RASDIALEVENT = 0xcccd
        }

        /// <summary>
        /// From https://msdn.microsoft.com/en-us/library/windows/desktop/aa372716(v=vs.85).aspx
        /// </summary>
        [Flags]
        public enum WindowMessageParameter : uint
        {
            PBT_APMQUERYSUSPEND = 0x0,
            PBT_APMBATTERYLOW = 0x9, // Notifies applications that the battery power is low.
            PBT_APMOEMEVENT = 0xb, // Notifies applications that the APM BIOS has signalled  an APM OEM event.
            PBT_APMQUERYSTANDBY = 0x0001, // 
            PBT_APMPOWERSTATUSCHANGE = 0xa, // Notifies applications of a change in the power status of the computer, such as a switch from battery power to A/C. The system also broadcasts this event when remaining battery power slips below the threshold specified by the user or if the battery power changes by a specified percentage.
            PBT_APMQUERYSUSPENDFAILED = 0x218, // Notifies applications that permission to suspend the computer was denied.
            PBT_APMRESUMEAUTOMATIC = 0x12, // Notifies applications that the system is resuming from sleep or hibernation. If the system detects any user activity after broadcasting PBT_APMRESUMEAUTOMATIC, it will broadcast a PBT_APMRESUMESUSPEND event to let applications know they can resume full interaction with the user.
            PBT_APMRESUMECRITICAL = 0x6, // Notifies applications that the system has resumed operation.
            PBT_APMRESUMESUSPEND = 0x7, // Notifies applications that the system has resumed operation after being suspended.
            PBT_APMSUSPEND = 0x4, // Notifies applications that the computer is about to enter a suspended state. 
            PBT_POWERSETTINGCHANGE = 0x8013, // Notifies applications that a power setting change event occurred.
            WM_POWER = 0x48, // Notifies applications that the system, typically a battery-powered personal computer, is about to enter a suspended mode.
            WM_POWERBROADCAST = 0x218, // Notifies applications that a power-management event has occurred.
            BROADCAST_QUERY_DENY = 0x424D5144,
            DBT_DEVICEARRIVAL = 0X8000,
            DBT_DEVICEREMOVECOMPLETE = 0X8004,
            DBT_DEVTYP_DEVICEINTERFACE = 5,
            DBT_DEVTYP_HANDLE = 6,
            DEVICE_NOTIFY_ALL_INTERFACE_CLASSES = 4,
            DEVICE_NOTIFY_SERVICE_HANDLE = 1,
            DEVICE_NOTIFY_WINDOW_HANDLE = 0
    }

        #endregion
    }
}
