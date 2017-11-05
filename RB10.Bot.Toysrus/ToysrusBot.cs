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

namespace RB10.Bot.Toysrus
{
    class ToysrusBot
    {
        private class SearchResult
        {
            public string JanCode { get; set; }
            public string ProductName { get; set; } = "";
            public string Price { get; set; } = "";
            public string OnlineStock { get; set; } = "-";
            public int StoreStockCount { get; set; } = -1;
            public int StoreLessStockCount { get; set; } = -1;
            public string ImageUrl { get; set; } = "";
        }

        private const int TIME_OUT = 100000;
        private System.Text.RegularExpressions.Regex _exist = new System.Text.RegularExpressions.Regex("<div class=\"status\">在庫あり</div>");
        private System.Text.RegularExpressions.Regex _lessExist = new System.Text.RegularExpressions.Regex("<div class=\"status\">在庫わずか</div>");

        #region 状態通知イベント

        public enum ReportState
        {
            Information,
            Warning,
            Error,
            Exception
        }

        public class ExecutingStateEventArgs : EventArgs
        {
            public string JanCode { get; set; }
            public string Message { get; set; }
            public ReportState ReportState { get; set; }
        }

        public delegate void ExecutingStateEventHandler(object sender, ExecutingStateEventArgs e);
        public event ExecutingStateEventHandler ExecutingStateChanged;
        private CancellationTokenSource _tokenSource;
        public CancellationToken CancelToken { get; private set; }

        #endregion

        public void Start(string janCodeFileName, string saveFileName)
        {
            _tokenSource = new CancellationTokenSource();
            CancelToken = _tokenSource.Token;

            Task.Run(() => Run(janCodeFileName, saveFileName), CancelToken);
        }

        public void Run(string janCodeFileName, string saveFileName)
        {
            try
            {
                // ファイル読み込み
                List<string> janCodes = System.IO.File.ReadLines(janCodeFileName, Encoding.GetEncoding("shift-jis")).ToList();

                // 情報取得
                List<SearchResult> results = new List<SearchResult>();
                foreach (var janCode in janCodes)
                {
                    var result = new SearchResult();
                    result.JanCode = janCode;

                    try
                    {
                        var url = $"https://www.toysrus.co.jp/search/?q={janCode}";
                        var req = (HttpWebRequest)WebRequest.Create(url);
                        req.Timeout = TIME_OUT;
                        //req.Proxy = null;

                        // html取得文字列
                        string html;

                        try
                        {
                            using (var res = (HttpWebResponse)req.GetResponse())
                            using (var resSt = res.GetResponseStream())
                            using (var sr = new StreamReader(resSt, Encoding.UTF8))
                            {
                                html = sr.ReadToEnd();
                            }
                        }
                        catch (Exception)
                        {
                            throw;
                        }
                        finally
                        {
                            if (req != null) req.Abort();
                        }

                        var parser = new HtmlParser();
                        var doc = parser.Parse(html);

                        var productName = doc.GetElementById("DISP_GOODS_NM");
                        if (productName == null)
                        {
                            ReportStatus(janCode, "商品がありません。", ReportState.Warning);
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
                            ReportStatus(janCode, "税込価格がありません。", ReportState.Warning);
                            continue;
                        }
                        else
                        {
                            result.Price = price.First().InnerHtml.Substring(0, price.First().InnerHtml.IndexOf("円")).Replace(",", "");
                        }

                        var image = doc.GetElementById("slideshow-01");
                        if (image == null || (image as AngleSharp.Dom.Html.IHtmlAnchorElement).IsHidden)
                        {
                            ReportStatus(janCode, "商品画像URLが取得できません。", ReportState.Warning);
                            result.ImageUrl = "不明";
                        }
                        else
                        {
                            result.ImageUrl = "https://www.toysrus.co.jp" + (image as AngleSharp.Dom.Html.IHtmlAnchorElement).PathName;
                        }

                        var stock = doc.GetElementById("isStock");
                        if (stock == null || (stock as AngleSharp.Dom.Html.IHtmlSpanElement).IsHidden)
                        {
                            ReportStatus(janCode, "在庫状況が確認できません。", ReportState.Warning);
                            result.OnlineStock = "不明";
                        }
                        else
                        {
                            var stockStatus = stock.Children[0].Children.Where(x => (x as AngleSharp.Dom.Html.IHtmlSpanElement).IsHidden == false);
                            if (stockStatus.Count() == 0)
                            {
                                ReportStatus(janCode, "在庫状況が確認できません。", ReportState.Warning);
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
                            ReportStatus(janCode, "トイザらスの商品コードが取得できなかったため、店舗在庫の取得ができません。", ReportState.Warning);
                            continue;
                        }
                        var storeUrl = $"https://www.toysrus.co.jp/disp/CSfGoodsPageRealShop_001.jsp?sku={(sku[0] as AngleSharp.Dom.Html.IHtmlInputElement).Value}&shopCd=";

                        req = (HttpWebRequest)WebRequest.Create(storeUrl);
                        req.Timeout = TIME_OUT;
                        //req.Proxy = null;

                        try
                        {
                            using (var res = (HttpWebResponse)req.GetResponse())
                            using (var resSt = res.GetResponseStream())
                            using (var sr = new StreamReader(resSt, Encoding.UTF8))
                            {
                                html = sr.ReadToEnd();
                            }
                        }
                        catch (Exception)
                        {
                            throw;
                        }
                        finally
                        {
                            if (req != null) req.Abort();
                        }

                        doc = parser.Parse(html);

                        var source = doc.Source.Text;

                        int existCount = _exist.Matches(source).Count;
                        int lessExistCount = _lessExist.Matches(source).Count;
                        result.StoreStockCount = existCount;
                        result.StoreLessStockCount = lessExistCount;

                        ReportStatus(janCode, "情報を取得しました。", ReportState.Information);
                    }
                    catch (Exception ex)
                    {
                        ReportStatus(janCode, ex.ToString(), ReportState.Exception);
                    }
                }

                // ファイル出力
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("JANコード,商品名,税込価格,オンライン在庫,店舗在庫あり,店舗在庫わずか,商品画像URL");
                foreach (var result in results)
                {
                    sb.AppendLine($"{result.JanCode},{result.ProductName},{result.Price},{result.OnlineStock},{result.StoreStockCount},{result.StoreLessStockCount},{result.ImageUrl}");
                }
                if (0 < results.Count) System.IO.File.WriteAllText(saveFileName, sb.ToString(), Encoding.GetEncoding("shift-jis"));
            }
            catch (Exception ex)
            {
                ReportStatus("-", ex.ToString(), ReportState.Exception);
            }
            finally
            {
                ReportStatus("-", "すべての処理が完了しました。", ReportState.Information);
            }
        }

        public async Task StartAsync(string janCodeFileName, string saveFileName)
        {
            try
            {
                // ファイル読み込み
                List<string> janFile = System.IO.File.ReadLines(janCodeFileName, Encoding.GetEncoding("shift-jis")).ToList();

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
                if (0 < outputs.Count) System.IO.File.WriteAllText(saveFileName, sb.ToString(), Encoding.GetEncoding("shift-jis"));
            }
            catch (Exception ex)
            {
                ReportStatus("-", ex.ToString(), ReportState.Exception);
            }
            finally
            {
                ReportStatus("-", "すべての処理が完了しました。", ReportState.Information);
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

                try
                {
                    var url = $"https://www.toysrus.co.jp/search/?q={janCode}";
                    var req = (HttpWebRequest)WebRequest.Create(url);
                    req.Timeout = TIME_OUT;
                    //req.Proxy = null;

                    // html取得文字列
                    string html;

                    try
                    {
                        using (var res = (HttpWebResponse)req.GetResponse())
                        using (var resSt = res.GetResponseStream())
                        using (var sr = new StreamReader(resSt, Encoding.UTF8))
                        {
                            html = sr.ReadToEnd();
                        }
                    }
                    catch (Exception)
                    {
                        throw;
                    }
                    finally
                    {
                        if (req != null) req.Abort();
                    }

                    var parser = new HtmlParser();
                    var doc = parser.Parse(html);

                    var productName = doc.GetElementById("DISP_GOODS_NM");
                    if (productName == null)
                    {
                        ReportStatus(janCode, "商品がありません。", ReportState.Warning);
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
                        ReportStatus(janCode, "税込価格がありません。", ReportState.Warning);
                        continue;
                    }
                    else
                    {
                        result.Price = price.First().InnerHtml.Substring(0, price.First().InnerHtml.IndexOf("円")).Replace(",", "");
                    }

                    var image = doc.GetElementById("slideshow-01");
                    if (image == null || (image as AngleSharp.Dom.Html.IHtmlAnchorElement).IsHidden)
                    {
                        ReportStatus(janCode, "商品画像URLが取得できません。", ReportState.Warning);
                        result.ImageUrl = "不明";
                    }
                    else
                    {
                        result.ImageUrl = "https://www.toysrus.co.jp" + (image as AngleSharp.Dom.Html.IHtmlAnchorElement).PathName;
                    }

                    var stock = doc.GetElementById("isStock");
                    if (stock == null || (stock as AngleSharp.Dom.Html.IHtmlSpanElement).IsHidden)
                    {
                        ReportStatus(janCode, "在庫状況が確認できません。", ReportState.Warning);
                        result.OnlineStock = "不明";
                    }
                    else
                    {
                        var stockStatus = stock.Children[0].Children.Where(x => (x as AngleSharp.Dom.Html.IHtmlSpanElement).IsHidden == false);
                        if (stockStatus.Count() == 0)
                        {
                            ReportStatus(janCode, "在庫状況が確認できません。", ReportState.Warning);
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
                        ReportStatus(janCode, "トイザらスの商品コードが取得できなかったため、店舗在庫の取得ができません。", ReportState.Warning);
                        continue;
                    }
                    var storeUrl = $"https://www.toysrus.co.jp/disp/CSfGoodsPageRealShop_001.jsp?sku={(sku[0] as AngleSharp.Dom.Html.IHtmlInputElement).Value}&shopCd=";

                    req = (HttpWebRequest)WebRequest.Create(storeUrl);
                    req.Timeout = TIME_OUT;
                    //req.Proxy = null;

                    try
                    {
                        using (var res = (HttpWebResponse)req.GetResponse())
                        using (var resSt = res.GetResponseStream())
                        using (var sr = new StreamReader(resSt, Encoding.UTF8))
                        {
                            html = sr.ReadToEnd();
                        }
                    }
                    catch (Exception)
                    {
                        throw;
                    }
                    finally
                    {
                        if (req != null) req.Abort();
                    }

                    doc = parser.Parse(html);

                    var source = doc.Source.Text;

                    int existCount = _exist.Matches(source).Count;
                    int lessExistCount = _lessExist.Matches(source).Count;
                    result.StoreStockCount = existCount;
                    result.StoreLessStockCount = lessExistCount;

                    ReportStatus(janCode, "情報を取得しました。", ReportState.Information);
                }
                catch (Exception ex)
                {
                    ReportStatus(janCode, ex.ToString(), ReportState.Exception);
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
                if (0 < outputs.Count) System.IO.File.WriteAllText(saveFileName, sb.ToString(), Encoding.GetEncoding("shift-jis"));
            }
            catch (Exception ex)
            {
                ReportStatus("-", ex.ToString(), ReportState.Exception);
            }
            finally
            {
                ReportStatus("-", "すべての処理が完了しました。", ReportState.Information);
            }
        }

        private SearchResult Scrape(string janCode)
        {
            var result = new SearchResult();
            result.JanCode = janCode;

            try
            {
                var url = $"https://www.toysrus.co.jp/search/?q={janCode}";
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.Timeout = TIME_OUT;
                //req.Proxy = null;

                // html取得文字列
                string html;

                try
                {
                    using (var res = (HttpWebResponse)req.GetResponse())
                    using (var resSt = res.GetResponseStream())
                    using (var sr = new StreamReader(resSt, Encoding.UTF8))
                    {
                        html = sr.ReadToEnd();
                    }
                }
                catch (Exception)
                {
                    throw;
                }
                finally
                {
                    if (req != null) req.Abort();
                }

                var parser = new HtmlParser();
                var doc = parser.Parse(html);

                var productName = doc.GetElementById("DISP_GOODS_NM");
                if (productName == null)
                {
                    ReportStatus(janCode, "商品がありません。", ReportState.Warning);
                    return result;
                }
                else
                {
                    result.ProductName = productName.InnerHtml;
                }

                var price = doc.GetElementsByClassName("inTax");
                if (price.Count() == 0 || (price.First() as AngleSharp.Dom.Html.IHtmlElement).IsHidden)
                {
                    ReportStatus(janCode, "税込価格がありません。", ReportState.Warning);
                    return result;
                }
                else
                {
                    result.Price = price.First().InnerHtml.Substring(0, price.First().InnerHtml.IndexOf("円")).Replace(",", "");
                }

                var image = doc.GetElementById("slideshow-01");
                if (image == null || (image as AngleSharp.Dom.Html.IHtmlAnchorElement).IsHidden)
                {
                    ReportStatus(janCode, "商品画像URLが取得できません。", ReportState.Warning);
                    result.ImageUrl = "不明";
                }
                else
                {
                    result.ImageUrl = "https://www.toysrus.co.jp" + (image as AngleSharp.Dom.Html.IHtmlAnchorElement).PathName;
                }

                var stock = doc.GetElementById("isStock");
                if (stock == null || (stock as AngleSharp.Dom.Html.IHtmlSpanElement).IsHidden)
                {
                    ReportStatus(janCode, "在庫状況が確認できません。", ReportState.Warning);
                    result.OnlineStock = "不明";
                }
                else
                {
                    var stockStatus = stock.Children[0].Children.Where(x => (x as AngleSharp.Dom.Html.IHtmlSpanElement).IsHidden == false);
                    if (stockStatus.Count() == 0)
                    {
                        ReportStatus(janCode, "在庫状況が確認できません。", ReportState.Warning);
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
                    ReportStatus(janCode, "トイザらスの商品コードが取得できなかったため、店舗在庫の取得ができません。", ReportState.Warning);
                    return result;
                }
                var storeUrl = $"https://www.toysrus.co.jp/disp/CSfGoodsPageRealShop_001.jsp?sku={(sku[0] as AngleSharp.Dom.Html.IHtmlInputElement).Value}&shopCd=";

                req = (HttpWebRequest)WebRequest.Create(storeUrl);
                req.Timeout = TIME_OUT;
                //req.Proxy = null;

                try
                {
                    using (var res = (HttpWebResponse)req.GetResponse())
                    using (var resSt = res.GetResponseStream())
                    using (var sr = new StreamReader(resSt, Encoding.UTF8))
                    {
                        html = sr.ReadToEnd();
                    }
                }
                catch (Exception)
                {
                    throw;
                }
                finally
                {
                    if (req != null) req.Abort();
                }

                doc = parser.Parse(html);

                var source = doc.Source.Text;

                int existCount = _exist.Matches(source).Count;
                int lessExistCount = _lessExist.Matches(source).Count;
                result.StoreStockCount = existCount;
                result.StoreLessStockCount = lessExistCount;

                ReportStatus(janCode, "情報を取得しました。", ReportState.Information);
            }
            catch (Exception ex)
            {
                ReportStatus(janCode, ex.ToString(), ReportState.Exception);
            }

            return result;
        }

        protected virtual void OnExecutingStateChanged(ExecutingStateEventArgs e)
        {
            if (ExecutingStateChanged != null)
                ExecutingStateChanged.Invoke(this, e);
        }

        protected void ReportStatus(string janCode, string message, ReportState reportState)
        {
            var eventArgs = new ExecutingStateEventArgs()
            {
                JanCode = janCode,
                Message = message,
                ReportState = reportState
            };
            OnExecutingStateChanged(eventArgs);
        }
    }
}
