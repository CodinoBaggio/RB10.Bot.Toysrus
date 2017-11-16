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
            public string JanCode { get; set; }
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
            List<(string ProductName, DateTime ReleaseDate)> inputProducts = new List<(string ProductName, DateTime ReleaseDate)>();
            foreach (var line in File.ReadAllLines(productNameFile, Encoding.GetEncoding("shift-jis")))
            {
                var items = line.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                if(items.Length == 2)
                {
                    inputProducts.Add((items[0], Convert.ToDateTime(items[1])));
                }
                else
                {
                    inputProducts.Add((items[0], DateTime.MinValue));
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

                        using (var writer = File.AppendText(saveFileName))
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
                // ファイル読み込み
                List<string> lines = System.IO.File.ReadLines(janCodeFileName, Encoding.GetEncoding("shift-jis")).ToList();
                List<InputJanCode> inputJanCodes = new List<InputJanCode>();
                foreach (var line in lines.Where(x => x != "" && !x.StartsWith("//")))
                {
                    var items = line.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                    InputJanCode input = new InputJanCode();
                    if (items.Length == 2)
                    {
                        input.JanCode = items[0].Trim();
                        input.ReleaseDate = Convert.ToDateTime(items[1].Trim());
                        inputJanCodes.Add(input);
                    }
                    else if(items.Length == 1)
                    {
                        input.JanCode = items[0].Trim();
                        input.ReleaseDate = DateTime.MinValue;
                        inputJanCodes.Add(input);
                    }
                }
                Notify("入力情報", "JANコードファイルを読み込みました。", NotifyStatus.Information, ProcessStatus.End);

                // 未掲載CSV読み込み
                List<string> unPosted = new List<string>();
                if (!includeUnPosted && System.IO.File.Exists(@".\未掲載.csv"))
                {
                    unPosted = System.IO.File.ReadLines(@".\未掲載.csv", Encoding.GetEncoding("shift-jis")).ToList();
                    Notify("未掲載情報", "未掲載ファイルを読み込みました。", NotifyStatus.Information, ProcessStatus.End);
                }

                // JANコードの選別
                List<string> janCodes = ToScreening(inputJanCodes, unPosted);

                // 情報取得
                List<SearchResult> results = new List<SearchResult>();
                foreach (var janCode in janCodes)
                {
                    var result = new SearchResult();
                    result.JanCode = janCode;
                    results.Add(result);

                    Notify(janCode, "情報取得を開始しました。", NotifyStatus.Information, ProcessStatus.Start);

                    try
                    {
                        // html取得文字列
                        string html = GetHtml($"http://www.yodobashi.com/?word={janCode}");

                        var parser = new HtmlParser();
                        var doc = parser.Parse(html);

                        var productName = doc.GetElementById("DISP_GOODS_NM");
                        if (productName == null)
                        {
                            Notify(janCode, "商品がありません。", NotifyStatus.Warning, ProcessStatus.End);
                            continue;
                        }
                        else
                        {
                            result.IsSiteHit = true;
                            result.ProductName = "\"" + productName.InnerHtml + "\"";
                        }

                        var price = doc.GetElementsByClassName("inTax");
                        if (price.Count() == 0 || (price.First() as AngleSharp.Dom.Html.IHtmlElement).IsHidden)
                        {
                            Notify(janCode, "税込価格がありません。", NotifyStatus.Warning, ProcessStatus.Processing);
                        }
                        else
                        {
                            result.Price = price.First().InnerHtml.Substring(0, price.First().InnerHtml.IndexOf("円")).Replace(",", "");
                        }

                        var image = doc.GetElementById("slideshow-01");
                        if (image == null || (image as AngleSharp.Dom.Html.IHtmlAnchorElement).IsHidden)
                        {
                            Notify(janCode, "商品画像URLが取得できません。", NotifyStatus.Warning, ProcessStatus.Processing);
                            result.ImageUrl = "不明";
                        }
                        else
                        {
                            result.ImageUrl = "https://www.toysrus.co.jp" + (image as AngleSharp.Dom.Html.IHtmlAnchorElement).PathName;
                        }

                        var isLotManegeYes = doc.GetElementById("isLotManegeYes");
                        if (isLotManegeYes == null || (isLotManegeYes as AngleSharp.Dom.Html.IHtmlSpanElement).IsHidden)
                        {
                            var stock = doc.GetElementById("isStock");
                            if (stock == null || (stock as AngleSharp.Dom.Html.IHtmlSpanElement).IsHidden)
                            {
                                Notify(janCode, "在庫状況が確認できません。", NotifyStatus.Warning, ProcessStatus.Processing);
                                result.OnlineStock = "不明";
                            }
                            else
                            {
                                var stockStatus = stock.Children[0].Children.Where(x => (x as AngleSharp.Dom.Html.IHtmlSpanElement).IsHidden == false);
                                if (stockStatus.Count() == 0)
                                {
                                    Notify(janCode, "在庫状況が確認できません。", NotifyStatus.Warning, ProcessStatus.Processing);
                                    result.OnlineStock = "不明";
                                }
                                else
                                {
                                    var f = stockStatus.First().InnerHtml;
                                    result.OnlineStock = f;
                                }
                            }
                        }
                        else
                        {
                            var isLot_a = doc.GetElementById("isLot_a") as AngleSharp.Dom.Html.IHtmlLabelElement;
                            if(!isLot_a.IsHidden) result.OnlineStock = "予約受付中";
                            var isLot_b = doc.GetElementById("isLot_b") as AngleSharp.Dom.Html.IHtmlLabelElement;
                            if (!isLot_b.IsHidden) result.OnlineStock = "予約受付終了間近";
                            var isLot_c = doc.GetElementById("isLot_b") as AngleSharp.Dom.Html.IHtmlLabelElement;
                            if (!isLot_c.IsHidden) result.OnlineStock = "予約受付終了";
                            var isLot_d = doc.GetElementById("isLot_b") as AngleSharp.Dom.Html.IHtmlLabelElement;
                            if (!isLot_d.IsHidden) result.OnlineStock = "注文不可";
                        }

                        var sku = doc.GetElementsByName("MAIN_SKU");
                        if (sku == null)
                        {
                            Notify(janCode, "トイザらスの商品コードが取得できなかったため、店舗在庫の取得ができません。", NotifyStatus.Warning, ProcessStatus.End);
                            continue;
                        }

                        var storeUrl = $"https://www.toysrus.co.jp/disp/CSfGoodsPageRealShop_001.jsp?sku={(sku[0] as AngleSharp.Dom.Html.IHtmlInputElement).Value}&shopCd=";
                        html = GetHtml(storeUrl);
                        doc = parser.Parse(html);
                        var source = doc.Source.Text;

                        int existCount = _exist.Matches(source).Count;
                        int lessExistCount = _lessExist.Matches(source).Count;
                        result.StoreStockCount = existCount;
                        result.StoreLessStockCount = lessExistCount;

                        Notify(janCode, "情報を取得しました。", NotifyStatus.Information, ProcessStatus.End);
                    }
                    catch (Exception ex)
                    {
                        Notify(janCode, ex.ToString(), NotifyStatus.Exception, ProcessStatus.End);
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
                CreateUnPostedFile(unPosted, results, includeUnPosted);
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

        public async Task StartAsync(string janCodeFileName, string saveFileName)
        {
            try
            {
                // ファイル読み込み
                List<string> janFile = System.IO.File.ReadLines(janCodeFileName, Encoding.GetEncoding("shift-jis")).ToList();
                Notify("入力", "JANコードファイルを読み込みました。", NotifyStatus.Information, ProcessStatus.End);

                // JANコードを1000ずつに分割
                int start = 0;
                List<List<string>> janCodesList = new List<List<string>>();
                while (true)
                {
                    List<string> janCodes = janFile.Skip(start).Take(100).ToList();
                    janCodesList.Add(janCodes);
                    start += 100;
                    if (janFile.Count < start)
                    {
                        break;
                    }
                }

                // スクレイピング非同期処理実行
                var tasks = janCodesList.Select(x => Task.Run(() =>
                {
                    return Scrape(x);
                }));
                var results = await Task.WhenAll(tasks);

                // 結果取得
                List<SearchResult> outputs = new List<SearchResult>();
                foreach (var result in results)
                {
                    outputs.AddRange(result);
                }

                // ファイル出力
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("JANコード,商品名,税込価格,オンライン在庫,店舗在庫あり,店舗在庫わずか,商品画像URL");
                foreach (var result in outputs)
                {
                    sb.AppendLine($"{result.JanCode},{result.ProductName},{result.Price},{result.OnlineStock},{result.StoreStockCount},{result.StoreLessStockCount},{result.ImageUrl}");
                }

                if (0 < outputs.Count)
                {
                    System.IO.File.WriteAllText(saveFileName, sb.ToString(), Encoding.GetEncoding("shift-jis"));
                    Notify("出力", "結果ファイルを出力しました。", NotifyStatus.Information, ProcessStatus.End);
                }
            }
            catch (Exception ex)
            {
                Notify("-", ex.ToString(), NotifyStatus.Exception, ProcessStatus.End);
            }
            finally
            {
                Notify("-", "すべての処理が完了しました。", NotifyStatus.Information, ProcessStatus.End);
            }
        }

        private List<SearchResult> Scrape(List<string> janCodes)
        {
            // 情報取得
            List<SearchResult> results = new List<SearchResult>();
            foreach (var janCode in janCodes)
            {
                var result = new SearchResult();
                result.JanCode = janCode;
                Notify(janCode, "情報取得を開始しました。", NotifyStatus.Information, ProcessStatus.Start);


                try
                {
                    // html取得文字列
                    string html = GetHtml($"https://www.toysrus.co.jp/search/?q={janCode}");

                    var parser = new HtmlParser();
                    var doc = parser.Parse(html);

                    var productName = doc.GetElementById("DISP_GOODS_NM");
                    if (productName == null)
                    {
                        Notify(janCode, "商品がありません。", NotifyStatus.Warning, ProcessStatus.End);
                        continue;
                    }
                    else
                    {
                        result.ProductName = productName.InnerHtml;
                    }
                    results.Add(result);

                    var price = doc.GetElementsByClassName("inTax");
                    if (price.Count() == 0 || (price.First() as AngleSharp.Dom.Html.IHtmlElement).IsHidden)
                    {
                        Notify(janCode, "税込価格がありません。", NotifyStatus.Warning, ProcessStatus.Processing);
                    }
                    else
                    {
                        result.Price = price.First().InnerHtml.Substring(0, price.First().InnerHtml.IndexOf("円")).Replace(",", "");
                    }

                    var image = doc.GetElementById("slideshow-01");
                    if (image == null || (image as AngleSharp.Dom.Html.IHtmlAnchorElement).IsHidden)
                    {
                        Notify(janCode, "商品画像URLが取得できません。", NotifyStatus.Warning, ProcessStatus.Processing);
                        result.ImageUrl = "不明";
                    }
                    else
                    {
                        result.ImageUrl = "https://www.toysrus.co.jp" + (image as AngleSharp.Dom.Html.IHtmlAnchorElement).PathName;
                    }

                    var stock = doc.GetElementById("isStock");
                    if (stock == null || (stock as AngleSharp.Dom.Html.IHtmlSpanElement).IsHidden)
                    {
                        Notify(janCode, "在庫状況が確認できません。", NotifyStatus.Warning, ProcessStatus.Processing);
                        result.OnlineStock = "不明";
                    }
                    else
                    {
                        var stockStatus = stock.Children[0].Children.Where(x => (x as AngleSharp.Dom.Html.IHtmlSpanElement).IsHidden == false);
                        if (stockStatus.Count() == 0)
                        {
                            Notify(janCode, "在庫状況が確認できません。", NotifyStatus.Warning, ProcessStatus.Processing);
                            result.OnlineStock = "不明";
                        }
                        else
                        {
                            var f = stockStatus.First().InnerHtml;
                            result.OnlineStock = f;
                        }
                    }

                    var sku = doc.GetElementsByName("MAIN_SKU");
                    if (sku == null)
                    {
                        Notify(janCode, "トイザらスの商品コードが取得できなかったため、店舗在庫の取得ができません。", NotifyStatus.Warning, ProcessStatus.End);
                        continue;
                    }

                    var storeUrl = $"https://www.toysrus.co.jp/disp/CSfGoodsPageRealShop_001.jsp?sku={(sku[0] as AngleSharp.Dom.Html.IHtmlInputElement).Value}&shopCd=";
                    html = GetHtml(storeUrl);
                    doc = parser.Parse(html);

                    var source = doc.Source.Text;

                    int existCount = _exist.Matches(source).Count;
                    int lessExistCount = _lessExist.Matches(source).Count;
                    result.StoreStockCount = existCount;
                    result.StoreLessStockCount = lessExistCount;

                    Notify(janCode, "情報を取得しました。", NotifyStatus.Information, ProcessStatus.End);
                }
                catch (Exception ex)
                {
                    Notify(janCode, ex.ToString(), NotifyStatus.Exception, ProcessStatus.End);
                }
            }

            return results;
        }

        public async Task StartAsync2(string janCodeFileName, string saveFileName)
        {
            try
            {
                // ファイル読み込み
                List<string> janFile = System.IO.File.ReadLines(janCodeFileName, Encoding.GetEncoding("shift-jis")).ToList();
                Notify("入力", "JANコードファイルを読み込みました。", NotifyStatus.Information, ProcessStatus.End);

                // スクレイピング非同期処理実行
                var tasks = janFile.Select(x => Task.Run(() =>
                {
                    return Scrape(x);
                }));
                var results = await Task.WhenAll(tasks);

                // 結果取得
                List<SearchResult> outputs = new List<SearchResult>();
                outputs.AddRange(results.Where(x => x.ProductName != ""));

                // ファイル出力
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("JANコード,商品名,税込価格,オンライン在庫,店舗在庫あり,店舗在庫わずか,商品画像URL");
                foreach (var result in outputs)
                {
                    sb.AppendLine($"{result.JanCode},{result.ProductName},{result.Price},{result.OnlineStock},{result.StoreStockCount},{result.StoreLessStockCount},{result.ImageUrl}");
                }

                if (0 < outputs.Count)
                {
                    System.IO.File.WriteAllText(saveFileName, sb.ToString(), Encoding.GetEncoding("shift-jis"));
                    Notify("出力", "結果ファイルを出力しました。", NotifyStatus.Information, ProcessStatus.End);
                }
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

        private SearchResult Scrape(string janCode)
        {
            var result = new SearchResult();
            result.JanCode = janCode;
            Notify(janCode, "情報取得を開始しました。", NotifyStatus.Information, ProcessStatus.Start);

            try
            {
                // html取得文字列
                string html = GetHtml($"https://www.toysrus.co.jp/search/?q={janCode}");

                var parser = new HtmlParser();
                var doc = parser.Parse(html);

                var productName = doc.GetElementById("DISP_GOODS_NM");
                if (productName == null)
                {
                    Notify(janCode, "商品がありません。", NotifyStatus.Warning, ProcessStatus.End);
                    return result;
                }
                else
                {
                    result.ProductName = productName.InnerHtml;
                }

                var price = doc.GetElementsByClassName("inTax");
                if (price.Count() == 0 || (price.First() as AngleSharp.Dom.Html.IHtmlElement).IsHidden)
                {
                    Notify(janCode, "税込価格がありません。", NotifyStatus.Warning, ProcessStatus.Processing);
                }
                else
                {
                    result.Price = price.First().InnerHtml.Substring(0, price.First().InnerHtml.IndexOf("円")).Replace(",", "");
                }

                var image = doc.GetElementById("slideshow-01");
                if (image == null || (image as AngleSharp.Dom.Html.IHtmlAnchorElement).IsHidden)
                {
                    Notify(janCode, "商品画像URLが取得できません。", NotifyStatus.Warning, ProcessStatus.Processing);
                    result.ImageUrl = "不明";
                }
                else
                {
                    result.ImageUrl = "https://www.toysrus.co.jp" + (image as AngleSharp.Dom.Html.IHtmlAnchorElement).PathName;
                }

                var stock = doc.GetElementById("isStock");
                if (stock == null || (stock as AngleSharp.Dom.Html.IHtmlSpanElement).IsHidden)
                {
                    Notify(janCode, "在庫状況が確認できません。", NotifyStatus.Warning, ProcessStatus.Processing);
                    result.OnlineStock = "不明";
                }
                else
                {
                    var stockStatus = stock.Children[0].Children.Where(x => (x as AngleSharp.Dom.Html.IHtmlSpanElement).IsHidden == false);
                    if (stockStatus.Count() == 0)
                    {
                        Notify(janCode, "在庫状況が確認できません。", NotifyStatus.Warning, ProcessStatus.Processing);
                        result.OnlineStock = "不明";
                    }
                    else
                    {
                        var f = stockStatus.First().InnerHtml;
                        result.OnlineStock = f;
                    }
                }

                var sku = doc.GetElementsByName("MAIN_SKU");
                if (sku == null)
                {
                    Notify(janCode, "トイザらスの商品コードが取得できなかったため、店舗在庫の取得ができません。", NotifyStatus.Warning, ProcessStatus.End);
                    return result;
                }

                var storeUrl = $"https://www.toysrus.co.jp/disp/CSfGoodsPageRealShop_001.jsp?sku={(sku[0] as AngleSharp.Dom.Html.IHtmlInputElement).Value}&shopCd=";
                html = GetHtml(storeUrl);
                doc = parser.Parse(html);

                var source = doc.Source.Text;

                int existCount = _exist.Matches(source).Count;
                int lessExistCount = _lessExist.Matches(source).Count;
                result.StoreStockCount = existCount;
                result.StoreLessStockCount = lessExistCount;

                Notify(janCode, "情報を取得しました。", NotifyStatus.Information, ProcessStatus.End);
            }
            catch (Exception ex)
            {
                Notify(janCode, ex.ToString(), NotifyStatus.Exception, ProcessStatus.End);
            }

            return result;
        }

        private List<string> ToScreening(List<InputJanCode> inputJanCodes, List<string> unPosted)
        {
            List<string> ret = new List<string>();
            foreach (var inputJanCode in inputJanCodes)
            {
                if(0 < (inputJanCode.ReleaseDate - DateTime.Now).TotalHours)
                {
                    // 発売開始前であればサイト検索対象する
                    ret.Add(inputJanCode.JanCode);
                }
                else
                {
                    var q = unPosted.Where(x => x == inputJanCode.JanCode);
                    if (q.Count() == 0)
                    {
                        // 未掲載になければサイト検索対象にする
                        ret.Add(inputJanCode.JanCode);
                    }
                }
            }
            return ret;
        }

        private void CreateUnPostedFile(List<string> unPosted, List<SearchResult> results, bool includeUnPosted)
        {
            // サイト検索でヒットしなかったJANコードを未掲載に追加
            unPosted.AddRange(results.Where(x => !x.IsSiteHit).Select(x => x.JanCode).ToList());

            if (includeUnPosted && System.IO.File.Exists(@".\未掲載.csv"))
            {
                var lines = System.IO.File.ReadAllLines(@".\未掲載.csv");
                unPosted.AddRange(lines.Where(x => x != ""));
            }

            // サイト検索でヒットしたJANコードを未掲載から除外
            var siteHitJanCodes = results.Where(x => x.IsSiteHit).Select(x => x.JanCode).ToList();
            List<string> newUnPosted = new List<string>();
            foreach (var item in unPosted)
            {
                var q = siteHitJanCodes.Where(x => x == item);
                if(q.Count() == 0)
                {
                    newUnPosted.Add(item);
                }
            }

            // ファイル書き込み
            if (0 < newUnPosted.Count) System.IO.File.WriteAllLines(@".\未掲載.csv", newUnPosted.Distinct().ToList());
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
                JanCode = janCode,
                Message = message,
                NotifyStatus = reportState,
                ProcessStatus = processState
            };
            OnExecutingStateChanged(eventArgs);
        }
    }
}
