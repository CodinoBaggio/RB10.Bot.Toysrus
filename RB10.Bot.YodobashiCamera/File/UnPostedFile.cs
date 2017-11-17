using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RB10.Bot.YodobashiCamera.File
{
    class UnPostedFile
    {
        private string _filePath;
        private List<string> _fileContents;

        public UnPostedFile(string filePath)
        {
            _filePath = filePath;
            _fileContents = new List<string>();

            if (System.IO.File.Exists(filePath))
            {
                _fileContents = System.IO.File.ReadLines(filePath, Encoding.GetEncoding("shift-jis")).ToList();
            }
        }

        public string Get(string janCode)
        {
            return _fileContents.Where(x => x == janCode).FirstOrDefault();
        }

        public void Add(string janCode)
        {
            _fileContents.Add(janCode);
        }

        public void Write(List<string> hitJanCode, List<string> unHitJanCode)
        {
            // サイト検索でヒットしなかったJANコードを未掲載に追加
            _fileContents.AddRange(unHitJanCode);

            // サイト検索でヒットしたJANコードを未掲載から除外
            List<string> newUnPosted = new List<string>();
            foreach (var item in _fileContents)
            {
                var q = hitJanCode.Where(x => x == item);
                if (q.Count() == 0)
                {
                    newUnPosted.Add(item);
                }
            }

            // ファイル書き込み
            if (0 < newUnPosted.Count) System.IO.File.WriteAllLines(_filePath, newUnPosted.Distinct().ToList());
        }
    }
}
