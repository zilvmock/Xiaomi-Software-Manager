using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using xsm.Logic.Scraper.Parsing;

namespace xsm.Logic.Scraper.Selenium
{
	internal class WebPageInteraction
	{
		private static readonly By[] CookieAcceptSelectors =
		{
			By.Id("onetrust-accept-btn-handler"),
			By.CssSelector("button#onetrust-accept-btn-handler"),
			By.XPath("//*[contains(translate(@id,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'cookie') or contains(translate(@class,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'cookie') or contains(translate(@id,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'consent') or contains(translate(@class,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'consent')]//button[contains(translate(normalize-space(.),'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'accept') or contains(translate(normalize-space(.),'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'allow') or contains(translate(normalize-space(.),'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'agree') or contains(translate(normalize-space(.),'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'consent')]")
		};

		private static readonly By CookieAcceptFallbackSelector = By.XPath(
			"//button[contains(translate(normalize-space(.),'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'accept all') or contains(translate(normalize-space(.),'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'accept') or contains(translate(normalize-space(.),'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'allow all') or contains(translate(normalize-space(.),'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'allow')]" +
			" | //a[contains(translate(normalize-space(.),'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'accept all') or contains(translate(normalize-space(.),'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'accept') or contains(translate(normalize-space(.),'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'allow all') or contains(translate(normalize-space(.),'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'allow')]" +
			" | //div[@role='button' and (contains(translate(normalize-space(.),'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'accept') or contains(translate(normalize-space(.),'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'allow'))]");

		private readonly IWebDriver _driver;
		private readonly WebDriverWait _wait;

		public WebPageInteraction(IWebDriver driver)
		{
			_driver = driver;
			_wait = new WebDriverWait(driver, ScraperConfig.ElementWaitTimeout);
		}

		public void NavigateToPage(string url)
		{
			_driver.Manage().Timeouts().PageLoad = ScraperConfig.PageLoadTimeout;
			_driver.Navigate().GoToUrl(url);

			// Wait for page to be truly ready
			_wait.Until(d => ((IJavaScriptExecutor)d)
				.ExecuteScript("return document.readyState")
				.Equals("complete"));

			// Give a small delay for any post-load JavaScript
			Thread.Sleep(2000);

			// Check if any dynamic content is still loading
			var hasLoader = (bool)((IJavaScriptExecutor)_driver)
				.ExecuteScript(@"return !!document.querySelector('.loading, .loader, [class*=""loading""]')");

			if (hasLoader)
			{
				_wait.Until(d => !(bool)((IJavaScriptExecutor)d)
					.ExecuteScript(@"return !!document.querySelector('.loading, .loader, [class*=""loading""]')"));
			}
		}

		public bool TryAcceptCookies(TimeSpan? timeout = null)
		{
			var wait = new WebDriverWait(_driver, timeout ?? TimeSpan.FromSeconds(3));

			foreach (var selector in CookieAcceptSelectors)
			{
				if (TryClickCookie(wait, selector))
				{
					return true;
				}
			}

			return TryClickCookie(wait, CookieAcceptFallbackSelector);
		}

		public void ClickFastbootUpdate()
		{
			void ItemDivClick()
			{
				var itemDiv = _wait.Until(ExpectedConditions.ElementToBeClickable(
					By.XPath("//div[@class='pc-miuidownload-sidetab_item' and text()='Fastboot Update']")));
				itemDiv.Click();
			}
			try
			{
				ItemDivClick();
			}
			catch // A popup might be blocking the element. Click at the edge of the page to close it.
			{
				ClickAtEdgeOfPage(0, 0);
				ItemDivClick();
			}
		}

		public List<ScrapeLink> ExtractFastbootLinks()
		{
			var fastbootList = _wait.Until(ExpectedConditions.ElementExists(By.ClassName("fastboot-list")));
			if (fastbootList == null) { throw new WebDriverException("fastboot-list element not found"); }

			// Wait for at least one link to be present
			_wait.Until(d => fastbootList.FindElements(By.TagName("a")).Count > 0);

			// Get all links
			var aElements = fastbootList.FindElements(By.TagName("a"));

			return aElements
				.Select(element => new ScrapeLink(
					element.Text,
					element.GetDomAttribute("href"),
					element.GetAttribute("outerHTML")))
				.ToList();
		}

		private bool TryClickCookie(WebDriverWait wait, By selector)
		{
			try
			{
				var element = wait.Until(driver =>
				{
					var matches = driver.FindElements(selector);
					return matches.FirstOrDefault(match => match.Displayed && match.Enabled);
				});

				if (element == null)
				{
					return false;
				}

				try
				{
					element.Click();
				}
				catch
				{
					((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", element);
				}

				return true;
			}
			catch (WebDriverTimeoutException)
			{
				return false;
			}
			catch
			{
				return false;
			}
		}

		private void ClickAtEdgeOfPage(int xOffset, int yOffset)
		{
			((IJavaScriptExecutor)_driver).ExecuteScript(
				$"document.elementFromPoint({xOffset}, {yOffset}).click();");
		}

		public void TakeErrorScreenshot(string logDirectory)
		{
			var screenshot = ((ITakesScreenshot)_driver).GetScreenshot();
			screenshot.SaveAsFile(Path.Combine(logDirectory,
				$"driver_error_screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png"));
		}
	}
}
