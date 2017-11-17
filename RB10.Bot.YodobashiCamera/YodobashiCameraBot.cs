using AngleSharp.Parser.Html;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RB10.Bot.YodobashiCamera
{
    class YodobashiCameraBot
    {
        private class InputJanCode
        {
            public string JanCode { get; set; }
            public DateTime ReleaseDate { get; set; }
        }

        private class SearchResult
        {
            public string JanCode { get; set; }
            public string ProductName { get; set; } = "";
            public string Price { get; set; } = "";
            public string OnlineStock { get; set; } = "-";
            public int StoreStockCount { get; set; } = -1;
            public int StoreLessStockCount { get; set; } = -1;
            public string ImageUrl { get; set; } = "";
            public bool IsSiteHit { get; set; }
        }

        private const int TIME_OUT = 100000;

        private System.Text.RegularExpressions.Regex _productReg = new System.Text.RegularExpressions.Regex("/product/(?<productNo>.*)/");
        private System.Text.RegularExpressions.Regex _exist = new System.Text.RegularExpressions.Regex("在庫あり");
        private System.Text.RegularExpressions.Regex _lessExist = new System.Text.RegularExpressions.Regex("在庫わずか");

        #region 状態通知イベント

        public enum NotifyStatus
        {
            Information,
            Warning,
            Error,
            Exception
        }

        public enum ProcessStatus
        {
            Start,
            Processing,
            End
        }

        public class ExecutingStateEventArgs : EventArgs
        {
            public string Info { get; set; }
            public string Message { get; set; }
            public NotifyStatus NotifyStatus { get; set; }
            public ProcessStatus ProcessStatus { get; set; }
        }

        public delegate void ExecutingStateEventHandler(object sender, ExecutingStateEventArgs e);
        public event ExecutingStateEventHandler ExecutingStateChanged;
        private CancellationTokenSource _tokenSource;
        public CancellationToken CancelToken { get; private set; }

        #endregion

        public void StartProductCodeFile(string productNameFile, int delay = 0)
        {
            _tokenSource = new CancellationTokenSource();
            CancelToken = _tokenSource.Token;

            Task.Run(() => CreateProductCodeFile(productNameFile, delay), CancelToken);
        }

        public void CreateProductCodeFile(string productNameFile, int delay)
        {
            List<(string JanCode, DateTime ReleaseDate, string ProductName)> inputProducts = new List<(string JanCode, DateTime ReleaseDate, string ProductName)>();
            foreach (var line in System.IO.File.ReadAllLines(productNameFile, Encoding.GetEncoding("shift-jis")))
            {
                var items = line.Split(",".ToCharArray());
                if(items.Length == 3)
                {
                    inputProducts.Add((items[0].Trim(), items[1].Trim() != "" ? Convert.ToDateTime(items[1].Trim()) : DateTime.MinValue, items[2].Trim()));
                }
                else
                {
                    continue;
                }
            }

            var saveFileName = Path.Combine(System.Windows.Forms.Application.StartupPath, Path.GetFileNameWithoutExtension(productNameFile) + "_ProductCode.csv");
            foreach (var inputProduct in inputProducts)
            {
                try
                {
                    string html = GetHtml($"http://www.yodobashi.com/?word={inputProduct.ProductName}");
                    var parser = new HtmlParser();
                    var doc = parser.Parse(html);

                    var productList = doc.GetElementsByClassName("js_productListPostTag js-clicklog js-analysis-schRlt");

                    foreach (var product in productList)
                    {
                        var element = product as AngleSharp.Dom.Html.IHtmlAnchorElement;
                        var code = element.PathName;
                        string productNo = string.Empty;
                        var match = _productReg.Match(code);
                        if (match.Success)
                        {
                            productNo = match.Groups["productNo"].Value;
                        }
                        else
                        {
                            continue;
                        }

                        using (var writer = System.IO.File.AppendText(saveFileName))
                        {
                            writer.WriteLine($"{productNo},{inputProduct.ReleaseDate.ToString("yyyy/MM/dd")}");
                        }
                        //string stockHtml = GetHtml($"http://www.yodobashi.com/ec/product/stock/{productNo}/");
                    }
                }
                catch (Exception ex)
                {
                    Notify(inputProduct.ProductName, ex.ToString(), NotifyStatus.Exception, ProcessStatus.End);
                }
                finally
                {
                    if (0 < delay) Task.Delay(delay).Wait();
                }
            }


        }

        public void Start(string janCodeFileName, string saveFileName, int delay = 0, bool includeUnPosted = false)
        {
            _tokenSource = new CancellationTokenSource();
            CancelToken = _tokenSource.Token;

            Task.Run(() => Run(janCodeFileName, saveFileName, delay, includeUnPosted), CancelToken);
        }

        public void Run(string janCodeFileName, string saveFileName, int delay, bool includeUnPosted)
        {
            try
            {
                // 入力ファイル読み込み
                List<(string JanCode, DateTime ReleaseDate, string ProductName)> inputJanCodes = new List<(string JanCode, DateTime ReleaseDate, string ProductName)>();
                bool isError = false;
                foreach (var line in System.IO.File.ReadAllLines(janCodeFileName, Encoding.GetEncoding("shift-jis")))
                {
                    var items = line.Split(",".ToCharArray());
                    if (items.Length == 3)
                    {
                        inputJanCodes.Add((items[0].Trim(), items[1].Trim() != "" ? Convert.ToDateTime(items[1].Trim()) : DateTime.MinValue, items[2].Trim()));
                    }
                    else
                    {
                        isError = true;
                        continue;
                    }
                }
                if (isError)
                {
                    Notify("入力情報", "JANコードファイルのフォーマットが正しくありません。", NotifyStatus.Error, ProcessStatus.End);
                }
                else
                {
                    Notify("入力情報", "JANコードファイルを読み込みました。", NotifyStatus.Information, ProcessStatus.End);
                }

                // 未掲載CSV読み込み
                var unPostedFile = new File.UnPostedFile(@".\未掲載.csv");
                if (System.IO.File.Exists(@".\未掲載.csv"))
                {
                    Notify("未掲載情報", "未掲載ファイルを読み込みました。", NotifyStatus.Information, ProcessStatus.End);
                }

                // 商品コードファイル読み込み
                var productCodeFilePath = Path.Combine(System.Windows.Forms.Application.StartupPath, Path.GetFileNameWithoutExtension(janCodeFileName) + "_ProductCode.csv");
                var productCodeFile = new File.ProductCodeFile(productCodeFilePath);
                if (System.IO.File.Exists(productCodeFilePath))
                {
                    Notify("商品コードファイル", "商品コードファイルを読み込みました。", NotifyStatus.Information, ProcessStatus.End);
                }

                // 情報取得
                List<SearchResult> results = new List<SearchResult>();
                foreach (var inputJanCode in inputJanCodes)
                {
                    var result = new SearchResult();
                    result.JanCode = inputJanCode.JanCode;
                    result.ProductName = inputJanCode.ProductName;
                    results.Add(result);

                    Notify(inputJanCode.JanCode, "情報取得を開始しました。", NotifyStatus.Information, ProcessStatus.Start);

                    try
                    {
                        var isApplySearch = IsApplySearch(inputJanCode, unPostedFile, includeUnPosted);

                        var product = productCodeFile.GetProductCode(inputJanCode.JanCode);
                        string productCode = "";
                        if (product.JanCode == null)
                        {
                            // 商品名検索
                            productCode = SearchByProductName(inputJanCode, ref result, ref productCodeFile);

                            if(productCode == "")
                            {
                                // 商品がヒットしない場合、発売日より前であれば未掲載ファイルに追加する
                                if (0 < (inputJanCode.ReleaseDate - DateTime.Now).TotalHours)
                                {
                                    unPostedFile.Add(inputJanCode.JanCode);
                                }

                                Notify(inputJanCode.JanCode, "商品がありません。", NotifyStatus.Warning, ProcessStatus.End);
                                continue;
                            }

                            // ウェイト
                            Task.Delay(delay).Wait();
                        }
                        else
                        {
                            productCode = product.ProductCode;
                        }

                        // 商品情報取得
                        GetProductDetail(inputJanCode, productCode, ref result);
                        
                        Notify(inputJanCode.JanCode, "情報を取得しました。", NotifyStatus.Information, ProcessStatus.End);
                    }
                    catch (Exception ex)
                    {
                        Notify(inputJanCode.JanCode, ex.ToString(), NotifyStatus.Exception, ProcessStatus.End);
                    }
                    finally
                    {
                        if (0 < delay) Task.Delay(delay).Wait();
                    }
                }

                // ファイル出力
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("JANコード,商品名,税込価格,オンライン在庫,店舗在庫あり,店舗在庫わずか,商品画像URL");
                foreach (var result in results.Where(x => x.IsSiteHit))
                {
                    sb.AppendLine($"{result.JanCode},{result.ProductName},{result.Price},{result.OnlineStock},{result.StoreStockCount},{result.StoreLessStockCount},{result.ImageUrl}");
                }

                if (0 < results.Where(x => x.IsSiteHit).Count())
                {
                    System.IO.File.WriteAllText(saveFileName, sb.ToString(), Encoding.GetEncoding("shift-jis"));
                    Notify("出力情報", "結果ファイルを出力しました。", NotifyStatus.Information, ProcessStatus.End);
                }

                // 未掲載ファイル作成
                unPostedFile.Write(results.Where(x => x.IsSiteHit).Select(x => x.JanCode).ToList(), results.Where(x => !x.IsSiteHit).Select(x => x.JanCode).ToList());

                // 商品コードファイル作成
                throw new NotImplementedException();
            }
            catch (Exception ex)
            {
                Notify("例外エラー", ex.ToString(), NotifyStatus.Exception, ProcessStatus.End);
            }
            finally
            {
                Notify("-", "すべての処理が完了しました。", NotifyStatus.Information, ProcessStatus.End);
            }
        }

        private bool IsApplySearch((string JanCode, DateTime ReleaseDate, string ProductName) inputJanCode, File.UnPostedFile unPostedFile, bool includeUnPosted)
        {
            if (includeUnPosted) return true;

            bool ret = false;
            if (0 < (inputJanCode.ReleaseDate - DateTime.Now).TotalHours)
            {
                // 発売開始前であればサイト検索対象する
                ret = true;
            }
            else
            {
                var q = unPostedFile.Get(inputJanCode.JanCode);
                if (q == null)
                {
                    // 未掲載になければサイト検索対象にする
                    ret = true;
                }
            }

            return ret;
        }

        private string SearchByProductName((string JanCode, DateTime ReleaseDate, string ProductName) inputJanCode, ref SearchResult result, ref File.ProductCodeFile productCodeFile)
        {
            string ret = "";
            string word = string.Join("+", inputJanCode.ProductName.Split(new string[] { " ", "　" }, StringSplitOptions.RemoveEmptyEntries));
            string html = GetHtml($"http://www.yodobashi.com/?word={word}");

            var parser = new HtmlParser();
            var doc = parser.Parse(html);

            var productList = doc.GetElementsByClassName("js_productListPostTag js-clicklog js-analysis-schRlt");
            if(productList.Count() == 0)
            {
                result.IsSiteHit = false;
                return ret;
            }
            else
            {
                result.IsSiteHit = true;

                var element = productList.First() as AngleSharp.Dom.Html.IHtmlAnchorElement;
                var code = element.PathName;
                string productNo = string.Empty;
                var match = _productReg.Match(code);
                if (match.Success)
                {
                    productNo = match.Groups["productNo"].Value;
                    ret = productNo;
                }

                // 検索結果の商品名を取得
                string searchedProductName = "";
                throw new NotImplementedException();

                // 商品コードファイルに追加
                productCodeFile.Add(inputJanCode.JanCode, productNo, searchedProductName, 1 < productList.Count());
            }

            return ret;
        }

        private void GetProductDetail((string JanCode, DateTime ReleaseDate, string ProductName) inputJanCode, string productCode, ref SearchResult result)
        {
            string html = GetHtml($"http://www.yodobashi.com/product/{productCode}/");
            var parser = new HtmlParser();
            var doc = parser.Parse(html);


            throw new NotImplementedException();




        }

        private string GetHtml(string url)
        {
            HttpWebRequest req = null;

            try
            {
                req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "GET";
                req.Timeout = TIME_OUT;
                req.UserAgent = Properties.Settings.Default.UserAgent;
                //req.Proxy = null;

                // html取得文字列
                string html = "";

                using (var res = (HttpWebResponse)req.GetResponse())
                using (var resSt = res.GetResponseStream())
                using (var sr = new StreamReader(resSt, Encoding.UTF8))
                {
                    html = sr.ReadToEnd();
                }

                return html;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                if (req != null) req.Abort();
            }
        }

        protected virtual void OnExecutingStateChanged(ExecutingStateEventArgs e)
        {
            if (ExecutingStateChanged != null)
                ExecutingStateChanged.Invoke(this, e);
        }

        protected void Notify(string janCode, string message, NotifyStatus reportState, ProcessStatus processState = ProcessStatus.Start)
        {
            var eventArgs = new ExecutingStateEventArgs()
            {
                Info = janCode,
                Message = message,
                NotifyStatus = reportState,
                ProcessStatus = processState
            };
            OnExecutingStateChanged(eventArgs);
        }
    }
}
