using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using xbtools.Classes;

namespace xbtools.Panels
{
    public partial class NandInfo : UserControl
    {
        private Nand.PrivateN nand;
        public delegate void DragDropC(string filename);
        public event DragDropC DragDropChanged;
        private DateTime mfr;

        public NandInfo()
        {
            InitializeComponent();
        }

        public NandInfo(Nand.PrivateN Nand)
        {
            InitializeComponent();
            fcrtLabel.Visibility = Visibility.Hidden;
            label2bla.Content = "2BL [CB_A]";
            setNand(Nand);
        }

        public void clear()
        {
            // Nand Info
            textBox2BLa.Text = "";
            textBox2BLb.Text = "";
            textBox4BL.Text = "";
            textBox5BL.Text = "";
            textBox6BL_p0.Text = "";
            textBox6BL_p1.Text = "";
            textBox7BL_p0.Text = "";
            textBox7BL_p1.Text = "";
            textBoxldv_0.Text = "";
            textBoxldv_1.Text = "";
            textBoxpd_0.Text = "";
            textBoxpd_1.Text = "";
            textBoxldv_cb.Text = "";
            textBoxpd_cb.Text = "";
            textBoxCbType.Text = "";
            textBoxConsole.Text = "";
            //textBox2BLb.Enabled = true;
            //label2blb.Visible = true;
            //label2bla.Visible = true;
            label2bla.Content = "2BL [CB_A]";

            // KV Info
            viewButton.Content = "View: Native";
            textBoxConsole.Text = "";
            textBoxConsoleID.Text = "";
            textBoxDVDKey.Text = "";
            textBoxOSIG.Text = "";
            textBoxSerial.Text = "";
            textBoxKVType.Text = "";
            textBoxRegion.Text = "";
            textBoxMFRDate.Text = "";
            fcrtLabel.Visibility = Visibility.Hidden;

            // Bad Blocks
            badBlockTxt.Text = "";

            // Reset Tab
            tabControl1.SelectedIndex = 0;
        }

        public void populateInfo()
        {
            if (nand.ok)
            {
                Dispatcher.BeginInvoke(new Action(() => {
                    // Nand Info
                    if (nand.bl.CB_A > 0) textBox2BLa.Text = nand.bl.CB_A.ToString();
                    else textBox2BLa.Text = "";
                    if (nand.bl.CB_B > 0) textBox2BLb.Text = nand.bl.CB_B.ToString();
                    else textBox2BLb.Text = "";
                    if (nand.bl.CD > 0) textBox4BL.Text = nand.bl.CD.ToString();
                    else textBox4BL.Text = "";
                    if (nand.bl.CE > 0) textBox5BL.Text = nand.bl.CE.ToString();
                    else textBox5BL.Text = "";
                    if (nand.bl.CF_0 > 0) textBox6BL_p0.Text = nand.bl.CF_0.ToString();
                    else textBox6BL_p0.Text = "";
                    if (nand.bl.CF_1 > 0) textBox6BL_p1.Text = nand.bl.CF_1.ToString();
                    else textBox6BL_p1.Text = "";
                    if (nand.bl.CG_0 > 0) textBox7BL_p0.Text = nand.bl.CG_0.ToString();
                    else textBox7BL_p0.Text = "";
                    if (nand.bl.CG_1 > 0) textBox7BL_p1.Text = nand.bl.CG_1.ToString();
                    else textBox7BL_p1.Text = "";
                    if (nand.bl.CF_0 > 0 || nand.bl.CG_0 > 0) textBoxldv_0.Text = nand.uf.ldv_p0.ToString();
                    else textBoxldv_0.Text = "";
                    if (nand.bl.CF_1 > 0 || nand.bl.CG_1 > 0) textBoxldv_1.Text = nand.uf.ldv_p1.ToString();
                    else textBoxldv_1.Text = "";
                    textBoxpd_0.Text = nand.uf.pd_0;
                    textBoxpd_1.Text = nand.uf.pd_1;
                    if (nand.bl.CB_A > 0 || nand.bl.CB_B > 0) textBoxldv_cb.Text = nand.uf.ldv_cb.ToString();
                    else textBoxldv_cb.Text = "";
                    textBoxpd_cb.Text = nand.uf.pd_cb;

                    if (nand.bl.CB_B != 0)
                    {
                        textBox2BLb.Text = nand.bl.CB_B.ToString();
                        textBoxCbType.Text = "Split CB";
                        textBox2BLb.IsEnabled = true;
                        label2blb.Visibility = Visibility.Visible;
                        label2bla.Visibility = Visibility.Visible;
                        label2bla.Content = "2BL [CB_A]";
                    }
                    else
                    {
                        textBox2BLb.IsEnabled = false;
                        textBoxCbType.Text = "Single CB";
                        label2blb.Visibility = Visibility.Hidden;
                        label2bla.Content = "2BL";
                    }

                    string name = Nand.Nand.getConsoleName(nand, variables.flashconfig);
                    textBoxConsole.Text = name;

                    // KV Info
                    textBoxConsole2.Text = name;

                    if (!String.IsNullOrWhiteSpace(nand._cpukey) && nand.ki.serial.Length > 0)
                    {
                        string mfrraw = nand.ki.mfdate;
                        try
                        {
                            DateTime.TryParseExact(mfrraw, "MM-dd-yy", null, DateTimeStyles.None, out mfr);
                            textBoxMFRDate.Text = mfr.Date.ToString("MM/dd/yyyy");
                        }
                        catch
                        {
                            textBoxMFRDate.Text = mfrraw;
                        }
                        try
                        {
                            if (viewButton.Content == "View: Native") textBoxConsoleID.Text = Nand.Nand.consoleID_KV_to_friendly(nand.ki.consoleid);
                            else textBoxConsoleID.Text = nand.ki.consoleid;
                        }
                        catch
                        {
                            textBoxConsoleID.Text = "";
                        }
                        textBoxDVDKey.Text = nand.ki.dvdkey;
                        textBoxOSIG.Text = nand.ki.osig;
                        textBoxSerial.Text = nand.ki.serial;
                        textBoxKVType.Text = nand.ki.kvtype.Replace("0", " ");
                        textBoxRegion.Text = "0x" + nand.ki.region + "   |   ";
                        if (nand.ki.region == "02FE") textBoxRegion.Text += "PAL/EU";
                        else if (nand.ki.region == "00FF") textBoxRegion.Text += "NTSC/US";
                        else if (nand.ki.region == "01FE") textBoxRegion.Text += "NTSC/JAP";
                        else if (nand.ki.region == "01FF") textBoxRegion.Text += "NTSC/JAP";
                        else if (nand.ki.region == "01FC") textBoxRegion.Text += "NTSC/KOR";
                        else if (nand.ki.region == "0101") textBoxRegion.Text += "NTSC/HK";
                        else if (nand.ki.region == "0201") textBoxRegion.Text += "PAL/AUS";
                        else if (nand.ki.region == "7FFF") textBoxRegion.Text += "DEVKIT";
                        fcrtLabel.Visibility = nand.ki.fcrtflag ? Visibility.Visible : Visibility.Hidden;
                    }
                    else
                    {
                        textBoxConsoleID.Text = "";
                        textBoxDVDKey.Text = "";
                        textBoxOSIG.Text = "";
                        textBoxSerial.Text = "";
                        textBoxKVType.Text = "";
                        textBoxRegion.Text = "";
                        textBoxMFRDate.Text = "";
                        fcrtLabel.Visibility = Visibility.Hidden;
                    }
                }));

                // Bad Blocks
                nand.getbadblocks();
                if (nand.bad_blocks.Count != 0)
                {
                    string text = "";
                    int blocksize = nand.bigblock ? 0x21000 : 0x4200;
                    int reservestartpos = nand.bigblock ? 0x1E0 : 0x3E0;
                    foreach (int bblock in nand.bad_blocks)
                    {
                        text += ("• Bad Block ID @ 0x" + bblock.ToString("X") + " [Offset: 0x" + ((bblock) * blocksize).ToString("X") + "]");
                        text += Environment.NewLine;
                    }
                    if (nand.remapped_blocks.Count != 0)
                    {
                        text += Environment.NewLine;
                        text += Environment.NewLine;
                        int i = 0;
                        foreach (int bblock in nand.remapped_blocks)
                        {
                            if (bblock != -1)
                            {
                                text += ("• Bad Block ID @ 0x" + nand.bad_blocks[i].ToString("X") + " Found @ 0x" + (reservestartpos + bblock).ToString("X") + "[Offset: 0x" + (blocksize * (reservestartpos + bblock)).ToString("X") + "]");
                                text += Environment.NewLine;
                            }
                            i++;
                        }
                    }
                    else text += ("Remapped Blocks Don't Exist");
                    add_badblocks_tab(text);
                }
                else add_badblocks_tab("No Bad Blocks");
            }
        }

        delegate void AddBadBlockTab(string text);
        private void add_badblocks_tab(string text)
        {
            if (badBlockTxt.Dispatcher.CheckAccess())
            {
                AddBadBlockTab s = new AddBadBlockTab(add_badblocks_tab);
                Dispatcher.Invoke(s, new object[] { text });
            }
            else
            {
                badBlockTxt.Text = text;
            }
        }

        public void setNand(Nand.PrivateN Nand)
        {
            this.nand = Nand;
            populateInfo();
        }

        delegate void ShowCpuKeyTab();
        public void show_cpukey_tab()
        {
            if (tabControl1.Dispatcher.CheckAccess())
            {
                ShowCpuKeyTab s = new ShowCpuKeyTab(show_cpukey_tab);
                Dispatcher.Invoke(s);
            }
            else
            {
                this.tabControl1.SelectedIndex = 1;
            }
        }

        public void change_tab()
        {
            this.tabControl1.Dispatcher.BeginInvoke((Action)(() => tabControl1.SelectedIndex = 0));
        }

        private void NandInfo_Load(object sender, EventArgs e)
        {
            fcrtLabel.Visibility = Visibility.Hidden;
            label2bla.Content = "2BL [CB_A]";
        }

        private void NandInfo_DragDrop(object sender, DragEventArgs e)
        {
            string[] s = (string[])e.Data.GetData(DataFormats.FileDrop, false);
            DragDropChanged(s[0]);
        }

        private void NandInfo_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.All;
            else
                e.Effects = DragDropEffects.None;
        }

        private void btnAdvanced_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Sorry, this feature isn't implemented yet.");
            /*
            if (nand != null && nand.ok)
            {
                HexEdit.KVViewer k = new HexEdit.KVViewer(Nand.Nand.decryptkv(nand._rawkv, Oper.StringToByteArray(nand._cpukey)));
                k.ShowDialog();
            }
            */
        }

        private void btnConsoleId_Click(object sender, EventArgs e)
        {
            if (viewButton.Content != "View: Native")
            {
                viewButton.Content = "View: Native";
                if (textBoxConsoleID.Text != "") textBoxConsoleID.Text = Nand.Nand.consoleID_KV_to_friendly(nand.ki.consoleid);
            }
            else
            {
                viewButton.Content = "View: Raw";
                if (textBoxConsoleID.Text != "") textBoxConsoleID.Text = nand.ki.consoleid;
            }
        }
    }
}
