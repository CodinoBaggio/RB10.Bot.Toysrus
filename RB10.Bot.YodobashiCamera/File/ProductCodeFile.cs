using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RB10.Bot.YodobashiCamera.File
{
    class ProductCodeFile
    {
        private string _filePath;
        private List<(string JanCode, string ProductCode, string ProductName, bool IsMulti)> _fileContents;

        public ProductCodeFile(string filePath)
        {
            _filePath = filePath;
            _fileContents = new List<(string JanCode, string ProductCode, string ProductName, bool IsMulti)>();
            if (System.IO.File.Exists(filePath))
            {
                foreach (var line in System.IO.File.ReadAllLines(filePath, Encoding.GetEncoding("shift-jis")))
                {
                    var items = line.Split(",".ToCharArray());
                    _fileContents.Add((items[0].Trim(), items[1].Trim(), items[2].Trim(), Convert.ToBoolean(items[3].Trim())));
                }
            }
        }

        public (string JanCode, string ProductCode, string ProductName, bool IsMulti) GetProductCode(string janCode)
        {
            return _fileContents.Where(x => x.JanCode == janCode).FirstOrDefault();
        }

        public void Add(string janCode, string productCode, string productName, bool isMulti)
        {
            _fileContents.Add((janCode, productCode, productName, isMulti));
        }
    }
}
