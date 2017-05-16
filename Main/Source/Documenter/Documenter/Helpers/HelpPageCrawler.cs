//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using Abot.Crawler;
//using Abot.Poco;
//using System.IO;
//using System.Net;

//namespace Documenter
//{
//    public class HelpPageCrawler
//    {
//        public const String rootPath = "Q:\\Test\\ApiServices";
//        public const String siteName = "mdm";
//        public const string wikiprefix = ""; ///Wiki/Page/

//        public static void Crawl()
//        {
//            Directory.CreateDirectory(rootPath);

//            CrawlConfiguration crawlConfig = new CrawlConfiguration();
//            crawlConfig.CrawlTimeoutSeconds = 100;
//            crawlConfig.MaxConcurrentThreads = 10;
//            crawlConfig.MaxPagesToCrawl = 1000;
//            crawlConfig.LoginUser = "svcuser";
//            crawlConfig.LoginPassword = "Cnas2014";
//            crawlConfig.IsAlwaysLogin = true;
//            crawlConfig.IsExternalPageCrawlingEnabled = false;
//            crawlConfig.DownloadableContentTypes += ",text/css";
//            crawlConfig.MaxCrawlDepth = 3;
//            crawlConfig.MaxPagesToCrawlPerDomain = 1000;
//            crawlConfig.IsForcedLinkParsingEnabled = true;
//            crawlConfig.MaxRetryCount = 1;

//            PoliteWebCrawler crawler = new PoliteWebCrawler(crawlConfig, null, null, null, null, null, null, null, null);

//            crawler.PageCrawlCompleted += Crawler_PageCrawlCompleted;
//            crawler.PageCrawlStarting += Crawler_PageCrawlStarting;

//            Console.WriteLine("Crawling api help page");

//            CrawlResult result = crawler.Crawl(new Uri("https://services-test.rema.no/mdm/api/v1/help"));
//        }

//        private static void Crawler_PageCrawlStarting(object sender, PageCrawlStartingArgs e)
//        {
//            PageToCrawl pageToCrawl = e.PageToCrawl;
//        }

//        static void Crawler_PageCrawlCompleted(object sender, PageCrawlCompletedArgs e)
//        {
//            CrawledPage crawledPage = e.CrawledPage;
//            Console.WriteLine("Page crawled: " + crawledPage.Uri.LocalPath);

//            String path = "";
//            if (crawledPage.Uri.LocalPath.Contains("ResourceModel"))
//            {
//                String[] str = crawledPage.Uri.Query.Split('=');
//                if (str.Length > 1)
//                    path = rootPath + "\\Api£Mdm€Api£ResourceModel£" + str[1];
//                else
//                    path = rootPath + "\\Api£Mdm€Api£" + Path.GetFileName(crawledPage.Uri.LocalPath);
//            }
//            else
//                path = rootPath + "\\Api£Mdm€Api£" + Path.GetFileName(crawledPage.Uri.LocalPath);
//            try
//            {
//                File.WriteAllText(path, crawledPage.Content.Text
//                                        .Replace("/" + siteName + "/api/v1/Help/Api/", wikiprefix + "Api£Mdm€Api£")
//                                        .Replace("/" + siteName + "/api/v1/Areas/HelpPage/HelpPage.css", "HelpPage.css")
//                                        .Replace("/" + siteName + "/api/v1/Help/ResourceModel?modelName=", "Api£Mdm€Api£ResourceModel£")
//                                        );
//            }
//            catch (Exception ex)
//            {

//            }
//        }

//    }
//}
