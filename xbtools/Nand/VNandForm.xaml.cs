using Microsoft.Win32;
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
using System.Windows.Shapes;
using xbtools.Classes;

namespace xbtools.Nand
{
    /// <summary>
    /// Interaction logic for VNandForm.xaml
    /// </summary>
    public partial class VNandForm : Window
    {

        public string filename;
        public List<int> BadBlocks = new List<int>();
        public string flashconfig;
        public consoles console;
        private int blockselected;
        public VNandForm()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            foreach (consoles c in variables.ctypes)
            {
                if (c.ID == -1 || c.ID == 11) continue;
                listBoxConsoles.Items.Add(c.Text);
            }
            foreach (string config in variables.flashconfigs)
            {
                listBoxConfigs.Items.Add(config);
            }
        }

        private void btnAddBadBlock_Click(object sender, RoutedEventArgs e)
        {
            if (!String.IsNullOrWhiteSpace(txtBadBlock.Text))
            {
                int result = 0;
                if (int.TryParse(txtBadBlock.Text, System.Globalization.NumberStyles.HexNumber, new CultureInfo("en-US"), out result))
                {
                    listBadBlocks.Items.Add(txtBadBlock.Text);
                    BadBlocks.Add(result);
                }
            }
        }

        private void btnRemoveBadBlock_Click(object sender, RoutedEventArgs e)
        {
            int result = 0;
            if (int.TryParse(listBadBlocks.Items[blockselected].ToString(), System.Globalization.NumberStyles.HexNumber, new CultureInfo("en-US"), out result)) if (BadBlocks.Contains(result)) BadBlocks.Remove(result);
            listBadBlocks.Items.RemoveAt(blockselected);
        }

        private void btnSaveTo_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog sf = new SaveFileDialog();
            if (sf.ShowDialog() == true)
            {
                textBox1.Text = sf.FileName;
                filename = sf.FileName;
            }
        }

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void listBoxConsoles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            listBoxConfigs.Items.Clear();
            foreach (consoles c in variables.ctypes)
            {
                if (c.ID == -1) continue;
                if (listBoxConsoles.Items[listBoxConsoles.SelectedIndex].ToString() == c.Text) console = c;
            }

            if (console.ID == 1 || console.ID == 4) listBoxConfigs.Items.Add("00023010");
            else if (console.ID == 10) listBoxConfigs.Items.Add("00043000");
            else if (console.ID == 6 || console.ID == 7)
            {
                listBoxConfigs.Items.Add("008A3020");
                listBoxConfigs.Items.Add("00AA3020");
            }
            else listBoxConfigs.Items.Add("01198010");
        }

        private void listBoxConfigs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            flashconfig = listBoxConfigs.Items[listBoxConfigs.SelectedIndex].ToString();
        }

        private void listBadBlocks_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            blockselected = listBadBlocks.SelectedIndex;
        }
    }
}
