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

            string? GetValue(string label)
            {
                IReadOnlyCollection<IWebElement> elements =
                    driver.FindElements(
                        By.XPath($"//td[normalize-space(.)='{label}']/following-sibling::td[1]")
                    );

                if (elements.Count == 0)
                {
                    return null;
                }

                string value = elements.First().Text.Trim();

                return string.IsNullOrWhiteSpace(value)
                    ? null
                    : value;
            }

            string? taxYear = null;
            string? recordsLastUpdated = null;

            IReadOnlyCollection<IWebElement> taxYearCells =
                driver.FindElements(
                    By.XPath("//td[.//b[contains(normalize-space(.), 'Tax Year')]]")
                );

            if (taxYearCells.Count > 0)
            {
                IReadOnlyCollection<IWebElement> taxYearDetails =
                    taxYearCells.First().FindElements(By.TagName("b"));

                if (taxYearDetails.Count > 0)
                {
                    string value = taxYearDetails.ElementAt(0).Text
                        .Replace("Tax Year", string.Empty)
                        .Trim();

                    taxYear = string.IsNullOrWhiteSpace(value)
                        ? null
                        : value;
                }

                if (taxYearDetails.Count > 1)
                {
                    string value =
                        taxYearDetails.ElementAt(1).Text.Trim();

                    recordsLastUpdated =
                        string.IsNullOrWhiteSpace(value)
                            ? null
                            : value;
                }
            }

            return new LienResult
            {
                Lien = lien,
                PARCEL = GetValue("PARCEL"),
                ADDRESS = GetValue("ADDRESS"),
                OWNER = GetValue("OWNER"),
                ACRES = GetValue("ACRES"),
                LAND_VALUE = GetValue("LAND VALUE"),
                IMPROVEMENTS = GetValue("IMPROVEMENTS"),
                TOTAL_VALUE = GetValue("TOTAL VALUE"),
                ASSESSED = GetValue("ASSESSED"),
                PPIN = GetValue("PPIN"),
                TOWNSHIP = GetValue("TOWNSHIP"),
                LEGAL = GetValue("LEGAL"),
                TAX_DISTRICT = GetValue("TAX DISTRICT"),
                SECTION = GetValue("SECTION"),
                RANGE = GetValue("RANGE"),
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
                "@B10tx2025"
                   

            );
            smtp.DeliveryMethod = SmtpDeliveryMethod.Network;

            smtp.Send(message);
        }
    }
}
