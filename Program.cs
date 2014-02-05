using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using LINQtoCSV;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Support.UI;

[assembly: log4net.Config.XmlConfigurator(Watch = true)]

namespace TestSeleniumStandalone
{
    class Program
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        static private IWebDriver driver;
        static private StringBuilder verificationErrors;
        static private string baseURL;
        static private bool acceptNextAlert = true;
        
        static void Main(string[] args)
        {
            SetupTest();
            TheUnassignedForTodayTest();
            TeardownTest();
        }

        static void SetupTest()
        {
            driver = new FirefoxDriver();
            baseURL = "https://foxtelsatellite.bsa.com.au";
            verificationErrors = new StringBuilder();
        }

        static void TeardownTest()
        {
            try
            {
                driver.Quit();
            }
            catch (Exception)
            {
                // Ignore errors if unable to close the browser
            }
        }

        private static void TheUnassignedForTodayTest()
        {
            try
            {
                CsvContext cc = new CsvContext();

                CsvFileDescription fileDescription = new CsvFileDescription
                {
                    SeparatorChar = '\t', // default is ','
                    FirstLineHasColumnNames = true,
                    EnforceCsvColumnAttribute = true, // default is false
                    FileCultureName = "en-AU" // default is the current culture
                };

                List<Record> records = cc.Read<Record>("Records.csv", fileDescription).ToList();

                driver.Navigate().GoToUrl(baseURL + "/");
                driver.Manage().Timeouts().ImplicitlyWait(new TimeSpan(0, 0, 30));
                driver.Manage().Timeouts().SetPageLoadTimeout(new TimeSpan(0, 0, 30));
                driver.Manage().Timeouts().SetScriptTimeout(new TimeSpan(0, 0, 30));
                driver.FindElement(By.Id("login-form_InstallerCode")).Clear();
                driver.FindElement(By.Id("login-form_InstallerCode")).SendKeys("H0Z");
                driver.FindElement(By.Id("login-form_Password")).Clear();
                driver.FindElement(By.Id("login-form_Password")).SendKeys("BSA9388");
                driver.FindElement(By.Id("login-form_LoginButton")).Click();
                driver.FindElement(By.Id("Work")).Click();
                driver.FindElement(By.Id("Unassigned For Today")).Click();

                var table = driver.FindElement(By.ClassName("table_table"));
                var elements = table.FindElements(By.CssSelector("tbody tr td"));
                if (elements.Count > 0)
                {
                    int rowCount = elements.Count/7;
                    StringBuilder sb = new StringBuilder();
                    for (int row = 0; row < rowCount; row++)
                    {
                        var orderNo = elements[0 + row * 7].Text;
                        var status = elements[1 + row * 7].Text;
                        var confirmed = elements[2 + row * 7].Text;
                        var start = elements[3 + row * 7].Text;
                        var end = elements[4 + row * 7].Text;
                        var type = elements[5 + row * 7].Text;
                        var suburb = elements[6 + row * 7].Text;
                        if (status == "Un-Assign")
                        {
                            if (records.Any(r => r.OrderNo == orderNo))
                            {
                                continue;
                            }
                            sb.AppendFormat("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}",
                                orderNo,
                                status,
                                confirmed,
                                start,
                                end,
                                type,
                                suburb
                                );
                            sb.AppendLine();
                            var newRecord = new Record
                            {
                                OrderNo = orderNo,
                                Status = status,
                                Confirmed = confirmed,
                                Start = start,
                                End = end,
                                Type = type,
                                Suburb = suburb
                            };
                            records.Add(newRecord);
                        }
                    }
                    if (sb.Length > 0)
                    {
                        cc.Write(records, "Records.csv", fileDescription);

                        log.Info("Sending notification email");
                        MailMessage mailMessage = new MailMessage();
                        mailMessage.Subject = "Unassigned For Today";
                        mailMessage.To.Add(Properties.Settings.Default.MailTo);
                        mailMessage.Body = sb.ToString();
                        SmtpClient mailer = new SmtpClient();
                        mailer.Send(mailMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error("TheUnassignedForTodayTest Error", ex);
            }
        }

        private static bool IsElementPresent(By by)
        {
            try
            {
                driver.FindElement(by);
                return true;
            }
            catch (NoSuchElementException)
            {
                return false;
            }
        }

        private bool IsAlertPresent()
        {
            try
            {
                driver.SwitchTo().Alert();
                return true;
            }
            catch (NoAlertPresentException)
            {
                return false;
            }
        }

        private string CloseAlertAndGetItsText()
        {
            try
            {
                IAlert alert = driver.SwitchTo().Alert();
                string alertText = alert.Text;
                if (acceptNextAlert)
                {
                    alert.Accept();
                }
                else
                {
                    alert.Dismiss();
                }
                return alertText;
            }
            finally
            {
                acceptNextAlert = true;
            }
        }
    }

    public class Record
    {
        [CsvColumn(FieldIndex = 1, Name = "OrderNo")]
        public string OrderNo { get; set; }
        [CsvColumn(FieldIndex = 2, Name = "Status")]
        public string Status { get; set; }
        [CsvColumn(FieldIndex = 3, Name = "Confirmed")]
        public string Confirmed { get; set; }
        [CsvColumn(FieldIndex = 4, Name = "Start")]
        public string Start { get; set; }
        [CsvColumn(FieldIndex = 5, Name = "End")]
        public string End { get; set; }
        [CsvColumn(FieldIndex = 6, Name = "Type")]
        public string Type { get; set; }
        [CsvColumn(FieldIndex = 7, Name = "Suburb")]
        public string Suburb { get; set; }
    }
}
