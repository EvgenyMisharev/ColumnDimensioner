using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
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

namespace ColumnDimensioner
{
    public partial class ColumnDimensionerWPF : Window
    {
        public string DimensionColumnsButtonName;
        public DimensionType SelectedDimensionType;

        public bool IndentationFirstRowDimensionsIsChecked;
        public string IndentationFirstRowDimensions;

        public bool IndentationSecondRowDimensionsIsChecked;
        public string IndentationSecondRowDimensions;
        ColumnDimensionerSettings ColumnDimensionerSettingsItem = null;
        public ColumnDimensionerWPF(List <DimensionType> dimensionTypesList)
        {
            ColumnDimensionerSettingsItem = new ColumnDimensionerSettings().GetSettings();
            InitializeComponent();
            comboBox_DimensionType.ItemsSource = dimensionTypesList;
            comboBox_DimensionType.DisplayMemberPath = "Name";

            if(ColumnDimensionerSettingsItem != null)
            {
                if (ColumnDimensionerSettingsItem.DimensionColumnsButtonName == "radioButton_VisibleInView")
                {
                    radioButton_VisibleInView.IsChecked = true;
                }
                else
                {
                    radioButton_Selected.IsChecked = true;
                }

                if (dimensionTypesList.FirstOrDefault(dt => dt.Name == ColumnDimensionerSettingsItem.SelectedDimensionTypeName) != null)
                {
                    comboBox_DimensionType.SelectedItem = dimensionTypesList.FirstOrDefault(dt => dt.Name == ColumnDimensionerSettingsItem.SelectedDimensionTypeName);
                }
                else
                {
                    comboBox_DimensionType.SelectedItem = comboBox_DimensionType.Items[0];
                }

                checkBox_IndentationFirstRowDimensions.IsChecked = ColumnDimensionerSettingsItem.IndentationFirstRowDimensionsIsChecked;
                textBox_IndentationFirstRowDimensions.Text = ColumnDimensionerSettingsItem.IndentationFirstRowDimensions;

                checkBox_IndentationSecondRowDimensions.IsChecked = ColumnDimensionerSettingsItem.IndentationSecondRowDimensionsIsChecked;
                textBox_IndentationSecondRowDimensions.Text = ColumnDimensionerSettingsItem.IndentationSecondRowDimensions;
            }
            else
            {
                comboBox_DimensionType.SelectedItem = comboBox_DimensionType.Items[0];
                checkBox_IndentationFirstRowDimensions.IsChecked = true;
                textBox_IndentationFirstRowDimensions.Text = "700";

                checkBox_IndentationSecondRowDimensions.IsChecked = true;
                textBox_IndentationSecondRowDimensions.Text = "1400";
            }
        }
        private void btn_Ok_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            this.DialogResult = true;
            this.Close();
        }
        private void ColumnDimensionerWPF_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Space)
            {
                SaveSettings();
                this.DialogResult = true;
                this.Close();
            }

            else if (e.Key == Key.Escape)
            {
                this.DialogResult = false;
                this.Close();
            }
        }
        private void btn_Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
        private void SaveSettings()
        {
            ColumnDimensionerSettingsItem = new ColumnDimensionerSettings();
            DimensionColumnsButtonName = (this.groupBox_DimensionColumns.Content as System.Windows.Controls.Grid)
                .Children.OfType<RadioButton>()
                .FirstOrDefault(rb => rb.IsChecked.Value == true)
                .Name;
            ColumnDimensionerSettingsItem.DimensionColumnsButtonName = DimensionColumnsButtonName;

            SelectedDimensionType = comboBox_DimensionType.SelectedItem as DimensionType;
            ColumnDimensionerSettingsItem.SelectedDimensionTypeName = SelectedDimensionType.Name;

            IndentationFirstRowDimensionsIsChecked = (bool)checkBox_IndentationFirstRowDimensions.IsChecked;
            ColumnDimensionerSettingsItem.IndentationFirstRowDimensionsIsChecked = IndentationFirstRowDimensionsIsChecked;

            IndentationFirstRowDimensions = textBox_IndentationFirstRowDimensions.Text;
            ColumnDimensionerSettingsItem.IndentationFirstRowDimensions = IndentationFirstRowDimensions;

            IndentationSecondRowDimensionsIsChecked = (bool)checkBox_IndentationSecondRowDimensions.IsChecked;
            ColumnDimensionerSettingsItem.IndentationSecondRowDimensionsIsChecked = IndentationSecondRowDimensionsIsChecked;

            IndentationSecondRowDimensions = textBox_IndentationSecondRowDimensions.Text;
            ColumnDimensionerSettingsItem.IndentationSecondRowDimensions = IndentationSecondRowDimensions;
            ColumnDimensionerSettingsItem.SaveSettings();
        }

        private void checkBox_IndentationFirstRowDimensions_Checked(object sender, RoutedEventArgs e)
        {
            if((bool)checkBox_IndentationFirstRowDimensions.IsChecked)
            {
                label_IndentationFirstRowDimensions.IsEnabled = true;
                textBox_IndentationFirstRowDimensions.IsEnabled = true;
                label_IndentationFirstRowDimensionsMM.IsEnabled = true;
            }
            else
            {
                label_IndentationFirstRowDimensions.IsEnabled = false;
                textBox_IndentationFirstRowDimensions.IsEnabled = false;
                label_IndentationFirstRowDimensionsMM.IsEnabled = false;
            }
        }

        private void checkBox_IndentationSecondRowDimensions_Checked(object sender, RoutedEventArgs e)
        {
            if((bool)checkBox_IndentationSecondRowDimensions.IsChecked)
            {
                label_IndentationSecondRowDimensions.IsEnabled = true;
                textBox_IndentationSecondRowDimensions.IsEnabled = true;
                label_IndentationSecondRowDimensionsMM.IsEnabled = true;
            }
            else
            {
                label_IndentationSecondRowDimensions.IsEnabled = false;
                textBox_IndentationSecondRowDimensions.IsEnabled = false;
                label_IndentationSecondRowDimensionsMM.IsEnabled = false;
            }
        }
    }
}
