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
            public string ProductName { get; set; } = "商品登録なし";
            public string OnlineStock { get; set; } = "-";
            public int StoreStockCount { get; set; } = -1;
            public int StoreLessStockCount { get; set; } = -1;
        }

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
                    results.Add(result);
                    result.JanCode = janCode;

                    try
                    {
                        var url = $"https://www.toysrus.co.jp/search/?q={janCode}";
                        var req = (HttpWebRequest)WebRequest.Create(url);

                        // html取得文字列
                        string html;

                        using (var res = (HttpWebResponse)req.GetResponse())
                        using (var resSt = res.GetResponseStream())
                        using (var sr = new StreamReader(resSt, Encoding.UTF8))
                        {
                            html = sr.ReadToEnd();
                        }

                        var parser = new HtmlParser();
                        var doc = parser.Parse(html);

                        var productName = doc.GetElementById("DISP_GOODS_NM");
                        if (productName == null)
                        {
                            ReportStatus(janCode, "商品登録がありません。", ReportState.Warning);
                            continue;
                        }
                        else
                        {
                            result.ProductName = productName.InnerHtml;
                        }

                        var stock = doc.GetElementById("isStock");
                        if (stock == null)
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
                        using (var res = (HttpWebResponse)req.GetResponse())
                        using (var resSt = res.GetResponseStream())
                        using (var sr = new StreamReader(resSt, Encoding.UTF8))
                        {
                            html = sr.ReadToEnd();
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
                sb.AppendLine("JANコード,商品名,オンライン在庫,店舗在庫あり,店舗在庫わずか");
                foreach (var result in results)
                {
                    sb.AppendLine($"{result.JanCode},{result.ProductName},{result.OnlineStock},{result.StoreStockCount},{result.StoreLessStockCount}");
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
