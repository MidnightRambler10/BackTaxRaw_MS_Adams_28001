using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using System.Threading;
using HtmlAgilityPack;
using Microsoft.Data.SqlClient;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace BackTaxRaw_MS_Adams_28001
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            List<Lien> liens = LoadLiens();

            if (liens.Count < 1)
            {
                SendEmailNotification(
                    "No Open Adams County Liens Found",
                    "No open tax liens were loaded for FIPCode 28001 for the current auction year. Scraper processing has been stopped."
                );

                return;
            }

            var options = new ChromeOptions();
            options.AddArgument("--start-maximized");

            IWebDriver driver = new ChromeDriver(options);
            driver = OpenUrl(driver);

            IterateLiens(driver, liens);
        }

        private static IWebDriver OpenUrl(IWebDriver driver)
        {
            driver.Navigate().GoToUrl("https://www.deltacomputersystems.com/MS/MS01/plinkquerym.html");
            return driver;
        }

        private static void IterateLiens(IWebDriver driver, List<Lien> liens)
        {
            List<(Lien Lien, string ErrorMessage, DateTime ErrorDateTime)> errorList =
                new List<(Lien Lien, string ErrorMessage, DateTime ErrorDateTime)>();
           

            foreach (Lien lien in liens)
            {
                try
                {
                    LienResult result = Scrape(driver, lien);
                    Console.WriteLine(
                        $"PPIN {result.Lien.AdvNum}: PARCEL {result.PARCEL}, ADDRESS {result.ADDRESS}, " +
                        $"OWNER {result.OWNER}, ACRES {result.ACRES}, LAND VALUE {result.LAND_VALUE}, " +
                        $"IMPROVEMENTS {result.IMPROVEMENTS}, TOTAL VALUE {result.TOTAL_VALUE}, " +
                        $"ASSESSED {result.ASSESSED}, PPIN {result.PPIN}, TOWNSHIP {result.TOWNSHIP}, " +
                        $"LEGAL {result.LEGAL}, TAX DISTRICT {result.TAX_DISTRICT}, " +
                        $"SECTION {result.SECTION}, RANGE {result.RANGE}, TAX YEAR {result.TAX_YEAR}, " +
                        $"RECORDS LAST UPDATED {result.RECORDS_LAST_UPDATED}"
                    );
                    driver = OpenUrl(driver);
                }
                catch (Exception ex)
                {
                    errorList.Add((
                        lien,
                        ex.Message,
                        DateTime.Now
                    ));

                    driver = OpenUrl(driver);

                    continue;
                }
            }

            if (errorList.Count > 0)
            {
                StringBuilder body = new StringBuilder();
                body.Append("<div style=\"font-family:Arial,Helvetica,sans-serif;color:#222;\">");
                body.Append("<h2 style=\"color:#b42318;margin-bottom:8px;\">Adams County Tax Lien Scraper Errors</h2>");
                body.Append($"<p style=\"margin:0 0 16px;\"><strong>Total failed liens:</strong> {errorList.Count}</p>");
                body.Append("<table style=\"border-collapse:collapse;width:100%;max-width:900px;\">");
                body.Append("<thead><tr style=\"background-color:#f2f4f7;\">");
                body.Append("<th style=\"border:1px solid #d0d5dd;padding:10px;text-align:left;\">Tax Lien ID</th>");
                body.Append("<th style=\"border:1px solid #d0d5dd;padding:10px;text-align:left;\">Date/Time</th>");
                body.Append("<th style=\"border:1px solid #d0d5dd;padding:10px;text-align:left;\">Error Message</th>");
                body.Append("</tr></thead><tbody>");

                foreach ((Lien Lien, string ErrorMessage, DateTime ErrorDateTime) error in errorList)
                {
                    body.Append("<tr>");
                    body.Append($"<td style=\"border:1px solid #d0d5dd;padding:10px;\">{WebUtility.HtmlEncode(error.Lien.TaxLienID)}</td>");
                    body.Append($"<td style=\"border:1px solid #d0d5dd;padding:10px;white-space:nowrap;\">{error.ErrorDateTime:yyyy-MM-dd HH:mm:ss}</td>");
                    body.Append($"<td style=\"border:1px solid #d0d5dd;padding:10px;\">{WebUtility.HtmlEncode(error.ErrorMessage)}</td>");
                    body.Append("</tr>");
                }

                body.Append("</tbody></table></div>");

                SendEmailNotification(
                    "Adams County Tax Lien Scraper - Errors",
                    body.ToString()
                );
            }

            driver.Quit();
        }

     

        private static LienResult Scrape(IWebDriver driver, Lien lien)
        {
            IWebElement ppin =
                driver.FindElement(By.Name("HTMPPIN"));

            ppin.SendKeys(lien.AdvNum);

            IWebElement submitButton =
                driver.FindElement(By.Name("HTMSUBMIT"));

            submitButton.Click();

            Thread.Sleep(3000);

            IReadOnlyCollection<IWebElement> ppinLinks =
                driver.FindElements(
                    By.XPath("//table[.//td//*[normalize-space()='PPIN']]//tr[position() > 1]/td[1]/a")
                );

            if (ppinLinks.Count == 0)
            {
                throw new Exception(
                    $"No property result found for PPIN {lien.AdvNum}."
                );
            }

            ppinLinks.First().Click();

            Thread.Sleep(2000);

            IWebElement parcelValue = driver.FindElement(
                By.XPath("//td[normalize-space(.)='PARCEL']/following-sibling::td[1]")
            );

            IWebElement addressValue = driver.FindElement(
                By.XPath("//td[normalize-space(.)='ADDRESS']/following-sibling::td[1]")
            );

            IWebElement ownerValue = driver.FindElement(
                By.XPath("//td[normalize-space(.)='OWNER']/following-sibling::td[1]")
            );

            IWebElement acresValue = driver.FindElement(
                By.XPath("//td[normalize-space(.)='ACRES']/following-sibling::td[1]")
            );

            IWebElement landValue = driver.FindElement(
                By.XPath("//td[normalize-space(.)='LAND VALUE']/following-sibling::td[1]")
            );

            IWebElement improvementsValue = driver.FindElement(
                By.XPath("//td[normalize-space(.)='IMPROVEMENTS']/following-sibling::td[1]")
            );

            IWebElement totalValue = driver.FindElement(
                By.XPath("//td[normalize-space(.)='TOTAL VALUE']/following-sibling::td[1]")
            );

            IWebElement assessedValue = driver.FindElement(
                By.XPath("//td[normalize-space(.)='ASSESSED']/following-sibling::td[1]")
            );

            IWebElement ppinValue = driver.FindElement(
                By.XPath("//td[normalize-space(.)='PPIN']/following-sibling::td[1]")
            );

            IWebElement townshipValue = driver.FindElement(
                By.XPath("//td[normalize-space(.)='TOWNSHIP']/following-sibling::td[1]")
            );

            IWebElement legalValue = driver.FindElement(
                By.XPath("//td[normalize-space(.)='LEGAL']/following-sibling::td[1]")
            );

            IWebElement taxDistrictValue = driver.FindElement(
                By.XPath("//td[normalize-space(.)='TAX DISTRICT']/following-sibling::td[1]")
            );

            IWebElement sectionValue = driver.FindElement(
                By.XPath("//td[normalize-space(.)='SECTION']/following-sibling::td[1]")
            );

            IWebElement rangeValue = driver.FindElement(
                By.XPath("//td[normalize-space(.)='RANGE']/following-sibling::td[1]")
            );

            IWebElement taxYearCell = driver.FindElement(
                By.XPath("//td[.//b[contains(normalize-space(.), 'Tax Year')]]")
            );

            IReadOnlyCollection<IWebElement> taxYearDetails =
                taxYearCell.FindElements(By.TagName("b"));

            if (taxYearDetails.Count < 2)
            {
                throw new Exception(
                    $"Tax Year or Records Last Updated was not found for PPIN {lien.AdvNum}."
                );
            }

            string taxYear = taxYearDetails.ElementAt(0).Text
                .Replace("Tax Year", string.Empty)
                .Trim();

            string recordsLastUpdated = taxYearDetails.ElementAt(1).Text.Trim();

            return new LienResult
            {
                Lien = lien,
                PARCEL = parcelValue.Text.Trim(),
                ADDRESS = addressValue.Text.Trim(),
                OWNER = ownerValue.Text.Trim(),
                ACRES = acresValue.Text.Trim(),
                LAND_VALUE = landValue.Text.Trim(),
                IMPROVEMENTS = improvementsValue.Text.Trim(),
                TOTAL_VALUE = totalValue.Text.Trim(),
                ASSESSED = assessedValue.Text.Trim(),
                PPIN = ppinValue.Text.Trim(),
                TOWNSHIP = townshipValue.Text.Trim(),
                LEGAL = legalValue.Text.Trim(),
                TAX_DISTRICT = taxDistrictValue.Text.Trim(),
                SECTION = sectionValue.Text.Trim(),
                RANGE = rangeValue.Text.Trim(),
                TAX_YEAR = taxYear,
                RECORDS_LAST_UPDATED = recordsLastUpdated
            };
        }

          
       

        private static List<Lien> LoadLiens()
        {
            string connectionString =
                "Server=DataServer;Database=ContentGrabber;Trusted_Connection=True;TrustServerCertificate=True;";

            const string query = @"
SELECT top 3
    TaxLienID,
    TaxLienAPN,
    AdvertisementNumber,
    ParcelID,
    AuctionYear,
    DelinquentYear
FROM staging.dbo.TaxLiens
WHERE TaxLienStatus = 'OPEN'
  AND AuctionYear = YEAR(GETDATE())
  AND FIPCode = '28001'";

            List<Lien> liens = new List<Lien>();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Lien lien = new Lien
                            {
                                TaxLienID = Convert.ToString(reader["TaxLienID"]) ?? string.Empty,
                                APN = Convert.ToString(reader["TaxLienAPN"]) ?? string.Empty,
                                AdvNum = Convert.ToString(reader["AdvertisementNumber"]) ?? string.Empty,
                                ParcelID = Convert.ToString(reader["ParcelID"]) ?? string.Empty,
                                AuctionYear = Convert.ToString(reader["AuctionYear"]) ?? string.Empty,
                                TaxYear = Convert.ToString(reader["DelinquentYear"]) ?? string.Empty
                            };

                            liens.Add(lien);
                        }
                    }
                }
            }

            return liens;
        }

        private static void SendEmailNotification(string subject, string body)
        {
            MailMessage message = new MailMessage();
            SmtpClient smtp = new SmtpClient();

            message.From = new MailAddress("abshir.saleem@lumentumllc.com");
            message.To.Add(new MailAddress("abshir.saleem@lumentumllc.com"));
            message.To.Add(new MailAddress("alejandro.henriquez@lumentumllc.com"));

            message.Subject = subject;
            message.IsBodyHtml = true;
            message.Body = body;

            smtp.Port = 587;
            smtp.Host = "smtp-mail.outlook.com";
            smtp.EnableSsl = true;
            smtp.UseDefaultCredentials = false;
            smtp.Credentials = new NetworkCredential(
                "abshir.saleem@lumentumllc.com",
                Environment.GetEnvironmentVariable("SMTP_PASSWORD")
                    ?? throw new InvalidOperationException("SMTP_PASSWORD environment variable is not set.")
            );
            smtp.DeliveryMethod = SmtpDeliveryMethod.Network;

            smtp.Send(message);
        }
    }
}
