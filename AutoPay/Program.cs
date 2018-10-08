using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace AutoPay
{
    class Program
    {
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_HIDE = 0;
        const int SW_SHOW = 5;

        static double creditLimit = 1500;
        static bool headless = true;
        static string API_KEY = "";
        static IWebDriver _driver = null;
        static ClickatellWrapperClient _clickatellWrapperClient;

        static void Main(string[] args)
        {
            hideConsoleWindow();
            initDriver();
            initClickatellWrapperClient();
            signIn();
            securityQuestions();
            clickCreditCardAccount();

            while (true)
            {
                double outstandingCredit = calculateOutstandingFees();

                //If the outstanding fees are more than $1.00 pay them
                if (outstandingCredit > 1)
                {
                    payOutstandingFees(outstandingCredit.ToString());
                }

                //Wait, refresh and check every 10 minutes
                Thread.Sleep(300000);
                _driver.Navigate().Refresh();
                Thread.Sleep(300000);
            }
        }

        //Hides the console window
        static void hideConsoleWindow()
        {
            var handle = GetConsoleWindow();
            ShowWindow(handle, SW_HIDE);
        }

        //Initialize chrome driver based off chrome installation directory
        static void initDriver()
        {
            var chromeOptions = new ChromeOptions();

            if (headless)
            {
                chromeOptions.AddArguments("headless");
            }

            _driver = new ChromeDriver(@"C:\Users\Jeeves\VisualStudioProjects\AutoPay\chrome_driver", chromeOptions);
        }

        //Initialize Clickatell Wrapper for sending text messages
        static void initClickatellWrapperClient()
        {
            _clickatellWrapperClient = new ClickatellWrapperClient(API_KEY);
        }

        //Sign the user into ScotiaBank Online
        static void signIn()
        {
            _driver.Navigate().GoToUrl("https://www.scotiaonline.scotiabank.com");

            _driver.Manage().Window.Maximize();

            IWebElement cardNumberBox = _driver.FindElement(By.Id("signon_form:userName"));
            cardNumberBox.SendKeys("");

            Thread.Sleep(5000);

            IWebElement passwordBox = _driver.FindElement(By.Id("signon_form:password_0"));
            passwordBox.SendKeys("");

            Thread.Sleep(5000);

            IWebElement signInBox = _driver.FindElement(By.Id("signon_form:enter_sol"));
            signInBox.Click();
        }

        //Process the security questions
        static void securityQuestions()
        {
            IWebElement securityQuestionBox = _driver.FindElement(By.XPath("//*[@id='mfaAuth_form']/div/table/tbody/tr[1]/td"));
            string securityQuestionText = securityQuestionBox.Text;

            IWebElement securityQuestionAnswer = _driver.FindElement(By.Id("mfaAuth_form:answer_0"));

            if (securityQuestionText.Equals(""))
            {
                securityQuestionAnswer.SendKeys("");
            }
            else if (securityQuestionText.Equals(""))
            {
                securityQuestionAnswer.SendKeys("");
            }
            else if (securityQuestionText.Equals(""))
            {
                securityQuestionAnswer.SendKeys("");
            }

            //Don't register the device, as chrome starts up a new instance everytime anyway
            IWebElement registerDevice = _driver.FindElement(By.Id("mfaAuth_form:register:1"));
            registerDevice.Click();

            IWebElement continueButton = _driver.FindElement(By.XPath("//*[@id='mfaAuth_form']/div/div[3]/input[1]"));
            continueButton.Click();
        }

        //Click on the SCENE VISA credit card account
        static void clickCreditCardAccount()
        {
            IWebElement creditCardAccount = _driver.FindElement(By.XPath("//a[contains(@href, '/online/views/accounts/accountDetails/visaAcctDetails.bns?acctId=')]"));
            creditCardAccount.Click();
        }

        static double calculateOutstandingFees()
        {
            IWebElement availableCreditBox = _driver.FindElement(By.XPath("//div[@class='vizbox-lgd availablecredit']/div"));
            double availableCredit = double.Parse(availableCreditBox.Text.Replace("$", ""));

            //Round the amount to the nearest cent
            double outstandingCredit = Math.Round(creditLimit - availableCredit, 2);
            return outstandingCredit;
        }

        //Calculate how much is outstanding and make the payment
        static void payOutstandingFees(string outstandingCredit)
        {
            IWebElement makePaymentDiv = _driver.FindElement(By.XPath("//div[@id='ft_form:ftVisaMakepayment']/input"));
            makePaymentDiv.Click();

            Thread.Sleep(5000);

            IWebElement amountBeingPaidBox = _driver.FindElement(By.XPath("//input[@id='modal_dlg_form:ft-amount-value']"));
            amountBeingPaidBox.Clear();
            amountBeingPaidBox.SendKeys(outstandingCredit);

            IWebElement initialPaymentButton = _driver.FindElement(By.Id("modal_dlg_form:right_btn"));
            initialPaymentButton.Click();

            Thread.Sleep(5000);

            //They use the same button again to confirm the payment
            IWebElement confirmPaymentButton = _driver.FindElement(By.Id("modal_dlg_form:right_btn"));
            confirmPaymentButton.Click();

            //Send text confirmation
            sendTextConfirmation(outstandingCredit);

            //Record the transaction
            File.AppendAllText(@"C:\Users\Jeeves\VisualStudioProjects\AutoPay\transactions\transactions.txt", Environment.NewLine + $"A transfer of {outstandingCredit} was paid on {DateTime.Now}" + Environment.NewLine);

            Thread.Sleep(5000);

            IWebElement closeDialogBox = _driver.FindElement(By.Id("modal_dlg_form:dlg_close_btn_div"));
            closeDialogBox.Click();
        }

        static async void sendTextConfirmation(string outstandingCredit)
        {
            await _clickatellWrapperClient.SendSmsAsync("AutoPay", "", $"A transaction of ${outstandingCredit} has been paid by AutoPay.");
        }
    }
}