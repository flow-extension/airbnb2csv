using CefSharp;
using CefSharp.Wpf;
using CsvHelper;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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

namespace WpfApplication1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        const string OUTPUT_FILE = @"D:\output.csv";

        private WebView _webView;
        private ManualResetEvent _manualResetEvent = new ManualResetEvent(false);
        private List<AirBNBRecord> _records = new List<AirBNBRecord>();

        public MainWindow()
        {
            InitializeComponent();

            InitChrome();
        }

        private void InitChrome()
        {
            var settings = new CefSharp.Settings
            {
                PackLoadingDisabled = true,
            };
            if (CEF.Initialize(settings))
            {
                _webView = new WebView();
                _webView.Loaded += _webView_Loaded;
                gridBrowser.Children.Add(_webView);
            }
        }

        void _webView_Loaded(object sender, RoutedEventArgs e)
        {
            Navigate("airbnb.com");
        }

        void _webView_Initialized(object sender, EventArgs e)
        {
        }

        public void Navigate(string url)
        {
            _webView.Load(url);
        }

        public string HTMLDocument
        {
            get
            {
                return _webView.EvaluateScript(@"document.getElementsByTagName ('html')[0].innerHTML").ToString();
            }
        }


        private void WaitSecondsThenSet(int seconds)
        {
            Thread.Sleep(seconds * 1000);
            _manualResetEvent.Set();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            Task.Factory.StartNew(() =>
                {
                    ScrapeSearchResults();
                    ScrapeAppartmentsProfiles();
                    ExportToCSV();
                    MessageBox.Show("Complete!");
                });
        } 

        private void ExportToCSV()
        {
            StreamWriter writer = new StreamWriter(OUTPUT_FILE);
            var csvWriter = new CsvWriter(writer);
            csvWriter.Configuration.Encoding = Encoding.UTF8;

            foreach (var item in _records)
            {
                csvWriter.WriteField(item.Headline);
                csvWriter.WriteField(item.Price);
                csvWriter.WriteField(item.ReviewCount);
                csvWriter.WriteField(item.Link);
                csvWriter.WriteField(item.Total);
                csvWriter.WriteField(item.Policy);
                csvWriter.NextRecord();
            }

            writer.Close();
        }

        private void ScrapeSearchResults()
        {
            string searchURL = _webView.EvaluateScript(@"document.URL").ToString();
            bool noMorePages = false;
            int pageNum = 1;

            while (!noMorePages)
            {
                string html = this.HTMLDocument;

                HtmlAgilityPack.HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(html);

                var propertyListItems = doc.DocumentNode.Descendants("div").Where(d =>
                    d.Attributes.Contains("data-lat"));
                
                // Do not continue if the current page has no results
                noMorePages = propertyListItems.Count() == 0;
                if (noMorePages) break;

                foreach (var item in propertyListItems)
                {
                    try
                    {
                        string name = item.Attributes["data-name"].Value;
                        string url = string.Format("https://www.airbnb.com{0}", item.Attributes["data-url"].Value);
                        string propertyId = item.Attributes["data-id"].Value;
                        int indexOfReviews = item.InnerHtml.IndexOf(" reviews");

                        var priceElement = item.Descendants("span")
                                .Where(el => el.Attributes.Contains("class") && el.Attributes["class"].Value.Contains("price-amount"))
                                .FirstOrDefault();
                        int price = int.Parse(priceElement.InnerHtml);

                        int reviewCount = 0;
                        if (indexOfReviews > -1)
                        {
                            string cutTemp = item.InnerHtml.Substring(0, indexOfReviews);
                            int beforeNumberPosition = cutTemp.LastIndexOf(' ');
                            string strReviewCount = cutTemp.Substring(beforeNumberPosition + 1, cutTemp.Length - beforeNumberPosition - 1);
                            reviewCount = int.Parse(strReviewCount);
                        }

                        // TODO: Add more details to record
                        _records.Add(new AirBNBRecord()
                        {
                            Headline = name,
                            Link = url,
                            ReviewCount = reviewCount,
                            Price = price,
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Scraping item failed: {1}", ex);
                    }
                }

                // Wait X seconds before navigating to next page
                _manualResetEvent.Reset();
                this.Navigate(searchURL + string.Format("&page={0}", pageNum + 1));
                WaitSecondsThenSet(Properties.Settings.Default.DelayBeforeNavigationSeconds);
                _manualResetEvent.WaitOne();

                pageNum++;
            }
        }

        private void ScrapeAppartmentsProfiles()
        {
            foreach (var item in _records)
            {
                try
                {
                    _manualResetEvent.Reset();
                    this.Navigate(item.Link);
                    WaitSecondsThenSet(Properties.Settings.Default.DelayBeforeNavigationSeconds);
                    _manualResetEvent.WaitOne();

                    string html = this.HTMLDocument;
                    HtmlAgilityPack.HtmlDocument doc = new HtmlDocument();
                    doc.LoadHtml(html);

                    var totalElement = doc.DocumentNode.Descendants("td")
                        .Where(el => el.Attributes.Contains("class") && el.Attributes["class"].Value.Contains("table-cell-price"))
                        .FirstOrDefault();
                    if (totalElement != null)
                        item.Total = totalElement.InnerText;

                    var policyElement = doc.DocumentNode.Descendants("a")
                        .Where(el => el.Attributes.Contains("id") && el.Attributes["id"].Value == "cancellation-policy")
                        .FirstOrDefault();
                    if (policyElement != null)
                        item.Policy = policyElement.InnerHtml;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Scraping item failed: {1}", ex);
                }
            }
        }
    }
}
