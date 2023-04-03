using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace ColumnDimensioner
{
    public class ColumnDimensionerSettings
    {
        public string DimensionColumnsButtonName { get; set; }
        public string SelectedDimensionTypeName { get; set; }
        public bool IndentationFirstRowDimensionsIsChecked { get; set; }
        public string IndentationFirstRowDimensions { get; set; }
        public bool IndentationSecondRowDimensionsIsChecked { get; set; }
        public string IndentationSecondRowDimensions { get; set; }


        public ColumnDimensionerSettings GetSettings()
        {
            ColumnDimensionerSettings columnDimensionerSettings = null;
            string assemblyPathAll = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string fileName = "ColumnDimensionerSettings.xml";
            string assemblyPath = assemblyPathAll.Replace("ColumnDimensioner.dll", fileName);

            if (File.Exists(assemblyPath))
            {
                using (FileStream fs = new FileStream(assemblyPath, FileMode.Open))
                {
                    XmlSerializer xSer = new XmlSerializer(typeof(ColumnDimensionerSettings));
                    columnDimensionerSettings = xSer.Deserialize(fs) as ColumnDimensionerSettings;
                    fs.Close();
                }
            }
            else
            {
                columnDimensionerSettings = null;
            }

            return columnDimensionerSettings;
        }

        public void SaveSettings()
        {
            string assemblyPathAll = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string fileName = "ColumnDimensionerSettings.xml";
            string assemblyPath = assemblyPathAll.Replace("ColumnDimensioner.dll", fileName);

            if (File.Exists(assemblyPath))
            {
                File.Delete(assemblyPath);
            }

            using (FileStream fs = new FileStream(assemblyPath, FileMode.Create))
            {
                XmlSerializer xSer = new XmlSerializer(typeof(ColumnDimensionerSettings));
                xSer.Serialize(fs, this);
                fs.Close();
            }
        }
    }
}
