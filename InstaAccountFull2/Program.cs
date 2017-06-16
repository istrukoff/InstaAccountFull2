using App = AppiumStartLib;
using NLog;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Remote;
using OpenQA.Selenium.Support.UI;
using OSGTools;
using OSGTools.FB;
using OSGTools.Insta;
using OSGTools.InstaFB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace InstaFBAccountFull
{
    class Program
    {
        #region **** variables ****
        // объект с настройками программы
        public static OSGTools.InstaFB.Settings settings;

        // файл с портами для ProxyDroid
        private static string proxyport;
        private static StreamReader readerProxyPorts;
        private static List<string> listProxyPorts = new List<string>();
        private static StreamWriter writerProxyPorts;

        // объект Instagram для хранения данных
        private static InstagramData insta;

        // полученный номер телефона для регистрации
        private static string telephone = "";

        // сформированный идентификатор телефона
        private static string android_id = "";

        // итоговый результат работы
        private static string status = "";

        // файл для записи зарегистрированных аккаунтов
        private static StreamWriter outputInsta;
        #endregion

        // загрузить порт прокси из файла
        private static string getProxyPort(string path)
        {
            string t = "";

            using (readerProxyPorts = new StreamReader(path))
            {
                while (true)
                {
                    t = readerProxyPorts.ReadLine();
                    if (t != null)
                        listProxyPorts.Add(t);
                    else
                        break;
                }
            }

            if (listProxyPorts.Count > 0)
                return listProxyPorts[0].ToString();
            else
                return "";
        }

        // удалить выбранный порт прокси из файла
        private static void removeProxyPort(string path, string port)
        {
            using (writerProxyPorts = new StreamWriter(path, false))
            {
                foreach (string t in listProxyPorts)
                    if (t != port)
                        writerProxyPorts.WriteLine(t);
            }
        }

        // вернуть выбранный порт прокси в файл
        private static void writeProxyPort(string port)
        {
            using (writerProxyPorts = new StreamWriter(settings.pathProxyPorts, true))
                writerProxyPorts.WriteLine(port);
        }

        private static StreamReader f_input_firstname;
        private static StreamReader f_input_lastname;

        public static List<string> readWomanNames(string path)
        {
            List<string> result = new List<string>();
            string t = "";

            using (f_input_firstname = new StreamReader(path))
            {
                while (true)
                {
                    t = f_input_firstname.ReadLine();
                    if (t != null)
                        result.Add(t);
                    else
                        break;
                }
            }

            return result;
        }

        public static List<string> readLastNames(string path)
        {
            List<string> result = new List<string>();
            string t = "";

            using (f_input_lastname = new StreamReader(path))
            {
                while (true)
                {
                    t = f_input_lastname.ReadLine();
                    if (t != null)
                        result.Add(t);
                    else
                        break;
                }
            }

            return result;
        }

        static void Main(string[] args)
        {
            Logger log = LogManager.GetCurrentClassLogger();

            // загрузка настроек программы из xml-файла
            log.Info("Загрузка настроек программы из xml-файла.");
            #region **** xml settings load ****
            settings = InstaFBSettings.LoadSettingsFromXML(string.Format(@"{0}\{1}", Environment.CurrentDirectory, "instafull2.xml"));

            log.Info(string.Format("Указанный USSD-запрос: {0}.", settings.ussd));
            log.Info(string.Format("Указанный IP прокси-сервера: {0}.", settings.proxyIP));
            log.Info(string.Format("Файл с портами для ProxyDroid: {0}.", settings.pathProxyPorts));
            log.Info(string.Format("Файл с именами: {0}.", settings.pathFirstName));
            log.Info(string.Format("Файл с фамилиями: {0}.", settings.pathLastName));
            log.Info(string.Format("Указанный пол: {0}.", settings.sex));
            log.Info(string.Format("Указанная дата рождения: {0}.", settings.birthday));
            log.Info(string.Format("Папка с аватарками: {0}.", settings.pathAvatars));
            log.Info(string.Format("Файл с e-mail: {0}.", settings.pathEmails));
            log.Info(string.Format("Файл с адресами сайтов: {0}.", settings.pathWebSites));
            log.Info(string.Format("Файл с описанием для аккаунта: {0}.", settings.pathInstaInfo));
            log.Info(string.Format("Файл с именами для аккаунта: {0}.", settings.pathNames));
            log.Info(string.Format("Папка с картинками для постов: {0}.", settings.pathPictures));
            log.Info(string.Format("Файл с текстом для постов: {0}.", settings.pathPostText));
            log.Info(string.Format("Файл записи аккаунтов: {0}.", settings.pathOutputInsta));
            #endregion

            // выборка порта для прокси-сервера
            log.Info("Выбираем порт прокси из файла.");
            proxyport = getProxyPort(settings.pathProxyPorts);

            if (proxyport == "")
            {
                log.Error("Файл с портами прокси пустой.");
                return;
            }
            else
            {
                log.Info(string.Format("Выбрали порт прокси: {0}", proxyport));
                log.Info("Удаляем выбранный порт из файла.");
                removeProxyPort(settings.pathProxyPorts, proxyport);
            }

            // формируем имя и фамилию
            log.Info("Выбираем имя и фамилию.");
            Random rnd_fn = new Random();
            Random rnd_ln = new Random();
            List<string> firstnames = readWomanNames(settings.pathFirstName);
            List<string> lastnames = readLastNames(settings.pathLastName);

            settings.FirstName = firstnames[rnd_fn.Next(0, firstnames.Count)].ToString();
            settings.LastName = lastnames[rnd_ln.Next(0, lastnames.Count)].ToString();
            log.Info(string.Format("Выбрали имя и фамилию: {0} {1}. Пол: {2}.", settings.FirstName, settings.LastName, settings.sex));

            if (settings.FirstName == "" || settings.LastName == "")
            {
                log.Error("Имя или фамилия пустые.");
                log.Info("Возвращаем порт прокси в файл.");
                writeProxyPort(proxyport);
                return;
            }

            string machine = Environment.MachineName;
            // запуск Appium
            App.Appium app = new App.Appium(machine, "instafull2");
            int app_port = app.appium_port;

            if (app.device_id == -1)
            {
                log.Error("Нет свободных устройств.");
                log.Info("Возвращаем порт прокси в файл.");
                writeProxyPort(proxyport);
                log.Info("**** **** **** ****");
                System.Environment.Exit(0);
            }
            else
            {
                log.Info("**** **** **** ****");
                if (app.AppiumStart())
                {
                    NLog.Targets.FileTarget tar = (NLog.Targets.FileTarget)LogManager.Configuration.FindTargetByName("instafull2_log");
                    tar.FileName = "${basedir}/logs/instafull2_${shortdate}_" + app.device + ".log"; // создаём отдельный файл для логов
                    tar.DeleteOldFileOnStartup = false; // не удалять старые логи
                    LogManager.ReconfigExistingLoggers();

                    log.Info("Создание объекта Appium");
                    log.Info("Используется устройство: {0}", app.device);

                    int pause = 30;
                    log.Info(string.Format("Пауза {0} секунд.", pause));
                    Thread.Sleep(pause * 1000);

                    DesiredCapabilities cap = new DesiredCapabilities();
                    cap.SetCapability("deviceName", "beeline");
                    cap.SetCapability("platformVersion", "4.0.3");
                    cap.SetCapability("platformName", "Android");
                    cap.SetCapability("appPackage", "ru.osg.projects.android.osgutility");
                    cap.SetCapability("appActivity", ".MainActivity");
                    cap.SetCapability("unicodeKeyboard", "true");

                    try
                    {
                        int port = app.appium_port;
                        log.Info(string.Format("Создание объекта AndroidDriver на порту {0}", port));
                        AndroidDriver<IWebElement> driver = new AndroidDriver<IWebElement>(new Uri(string.Format("http://127.0.0.1:{0}/wd/hub", port)), cap);

                        WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(30));  // таймаут ожидания элемента = 30 секунд

                        // **** запуск ProxyDroid **** //
                        if (proxyport != "")
                        {
                            wait.Timeout = new TimeSpan(0, 0, 10);
                            log.Info(string.Format("Запуск ProxyDroid для указания порта {0} прокси-сервера {1}.", proxyport, settings.proxyIP));
                            if (ProxyDroidStart.runProxyDroid(driver, wait, proxyport))
                            {
                                log.Info("Порт прокси указан.");
                            }
                            else
                            {
                                log.Error("Ошибка указания порта прокси.");
                                log.Info("Возвращаем порт прокси в файл.");
                                writeProxyPort(proxyport);
                                return;
                            }
                        }
                        else
                        {
                            log.Info("Пропускаем запуск ProxyDroid, так как порт прокси-сервера не задан.");
                        }

                        // **** запуск IDChanger для изменения идентификатора телефона **** //
                        log.Info(string.Format("Запуск IDChanger для изменения идентификатора телефона."));
                        android_id = DeviceIDChangerStart.runIDChanger(driver, wait);
                        log.Info(string.Format("Изменённый идентификатор телефона: {0}.", android_id));
                        settings.android_id = android_id;

                        // **** запуск получения номера телефона **** //
                        log.Info(string.Format("Запуск получения номера телефона."));
                        telephone = Telephone.getNumber(driver, wait, settings.ussd);
                        log.Info(string.Format("Номер телефона: {0}.", telephone));
                        if (telephone == "" || telephone == null)
                        {
                            status = "Ошибка получения номера телефона.";
                            log.Error(status);
                            log.Info("Возвращаем порт прокси в файл.");
                            writeProxyPort(proxyport);
                            if (app.AppiumStop(status))
                                log.Info("Работа Appium завершена.");
                            else
                                log.Error("Ошибка завершения Appium");
                            log.Info("Выполнение задачи завершено.");
                            return;
                        }
                        else
                        {
                            settings.telephone = telephone;
                        }

                        // **** запуск очистки галереи **** //
                        wait.Timeout = new TimeSpan(0, 0, 10);
                        log.Info("Запуск очистки галереи на телефоне.");
                        if (AndroidGallery.clearGallery(driver, wait))
                        {
                            log.Info("Галерея очищена.");
                        }
                        else
                        {
                            log.Error("Ошибка очистки галереи.");
                        }
                        Thread.Sleep(1000);

                        // **** запуск OSGUtility для проверки доступа в интернет **** //
                        wait.Timeout = new TimeSpan(0, 0, 30);
                        status = InternetCheckStart.checkInternet(driver, wait);

                        if (status == InternetCheckStart.noInetConnAlert)
                        {
                            status = string.Format("На телефоне {0} нет интернета.", app.device);
                            log.Error(status);
                            log.Info("Возвращаем порт прокси в файл.");
                            writeProxyPort(proxyport);
                            if (app.AppiumStop(status))
                                log.Info("Работа Appium завершена.");
                            else
                                log.Error("Ошибка завершения Appium");
                        }
                        else
                        {
                            wait.Timeout = new TimeSpan(0, 0, 30);
                            // **** запуск Instagram для выхода из аккаунта **** //
                            #region **** instagram logout ****
                            log.Info(string.Format("Запуск Instagram для выхода из аккаунта."));
                            if (InstaLogin.LogOut(driver, wait))
                            {
                                log.Info("Успешный выход из аккаунта.");
                            }
                            else { }
                            #endregion

                            driver.PressKeyCode(AndroidKeyCode.Home);

                            wait.Timeout = new TimeSpan(0, 0, 30);
                            // **** запуск Instagram для регистрации нового аккаунта **** //
                            #region **** instagram reg ****
                            log.Info("Запуск Instagram для регистрации нового аккаунта.");
                            insta = InstaActions.Registration(driver, wait, settings);
                            if (insta == null)
                            {
                                status = "Ошибка регистрации.";
                                log.Error(status);
                                log.Info("Закрываем процесс Appium: {0}", app.pid);
                                if (app.AppiumStop(status))
                                    log.Info(status);
                                else
                                    log.Error("Ошибка завершения Appium");
                                return;
                            }
                            else
                            {
                                status = "Успешная регистрация нового аккаунта.";
                                log.Info(status);
                                log.Info(insta.Login);
                            }
                            #endregion

                            // **** запись аккаунта в БД **** //
                            #region **** insta db insert ****
                            if (insta != null)
                            {
                                log.Info(string.Format("Пишем в БД аккаунт Instagram: {0} {1} {2} {3}", insta.Login, insta.Password, settings.telephone, settings.android_id));
                                if (InstaAccountsBase.InstagramAdd(insta.Login, 
                                    insta.Password, 
                                    settings.telephone, 
                                    settings.android_id, 
                                    "", 
                                    "", 
                                    settings.proxyIP, 
                                    int.Parse(proxyport), 
                                    "0"))
                                {
                                    status = "Успешная запись в БД.";
                                    log.Info(status);
                                }
                                else
                                {
                                    status = "Ошибка записи в БД.";
                                    log.Error(status);
                                }
                            }

                            using (StreamWriter account_file = new StreamWriter(string.Format("accounts.txt", insta.Login), true))
                                account_file.WriteLine(insta.Login);
                            #endregion

                            wait.Timeout = new TimeSpan(0, 0, 30);
                            // **** запуск Instagram для заполнения зарегистрированного аккаунта **** //
                            #region **** instagram fill ****
                            log.Info(string.Format("Запуск Instagram для заполнения зарегистрированного аккаунта {0}.", insta.Login));
                            if (InstaActions.Fill(driver, wait, insta, settings))
                            {
                                status = "Успешное заполнение аккаунта.";
                                log.Info(status);
                            }
                            else
                            {
                                status = "Ошибка заполнения аккаунта. Смотрите логи.";
                                log.Error(status);
                            }

                            wait.Timeout = new TimeSpan(0, 0, 10);
                            // **** запуск очистки галереи **** //
                            log.Info("Запуск очистки галереи на телефоне.");
                            if (AndroidGallery.clearGallery(driver, wait))
                            {
                                log.Info("Галерея очищена.");
                            }
                            else
                            {
                                log.Error("Ошибка очистки галереи.");
                            }
                            Thread.Sleep(1000);
                            #endregion

                            wait.Timeout = new TimeSpan(0, 0, 20);
                            // **** запуск Instagram для эмуляции жизни **** //
                            log.Info(string.Format("Запуск Instagram для эмуляции жизни {0}.", insta.Login));
                            try
                            {
                                InstaActions.SearchPageView(driver, wait, 10, 500, 2000);
                                log.Info("Успешное завершение эмуляции жизни.");
                            }
                            catch
                            {
                                log.Error("Ошибка выполнения эмуляции жизни.");
                            }

                            wait.Timeout = new TimeSpan(0, 0, 30);
                            // **** запуск Instagram для размещения постов в зарегистрированном аккаунте **** //
                            #region **** instagram post ****
                            log.Info(string.Format("Запуск Instagram для размещения постов в зарегистрированном аккаунте {0}.", insta.Login));
                            if (InstaActions.Post2(driver, wait, insta, settings))
                            {
                                status = "Успешное размещение постов.";
                                log.Info(status);
                            }
                            else
                            {
                                status = "Ошибка размещения постов. Смотрите логи.";
                                log.Error(status);
                            }
                            #endregion

                            wait.Timeout = new TimeSpan(0, 0, 30);
                            // **** запуск Instagram для выхода из аккаунта **** //
                            #region **** instagram logout ****
                            // перед выходом из аккаунта включаем режим полёта
                            log.Info("Включаем режим самолёта.");
                            // открываем системное меню
                            driver.Swipe(360, 5, 330, 550, 300);
                            Thread.Sleep(1000);
                            driver.Swipe(360, 5, 330, 700, 300);
                            // включаем режим самолёта
                            try
                            {
                                wait.Until(ExpectedConditions.PresenceOfAllElementsLocatedBy(By.XPath("//android.view.View[contains(@content-desc, 'Режим полета отключен.')]")));
                                driver.FindElementByXPath("//android.view.View[contains(@content-desc, 'Режим полета отключен.')]").Click();
                                wait.Until(ExpectedConditions.PresenceOfAllElementsLocatedBy(By.XPath("//android.view.View[contains(@content-desc, 'Режим полета включен.')]")));
                            }
                            catch
                            {
                                log.Error("Ошибка при включении режима самолёта. Возможно, он уже включен.");
                            }
                            driver.PressKeyCode(AndroidKeyCode.Home);
                            
                            log.Info(string.Format("Запуск Instagram для выхода из аккаунта."));
                            if (InstaLogin.LogOut(driver, wait))
                            {
                                log.Info("Успешный выход из аккаунта.");
                            }
                            else { }

                            // выключаем режим полёта
                            log.Info("Отключаем режим самолёта.");
                            // открываем системное меню
                            driver.Swipe(360, 5, 330, 550, 300);
                            Thread.Sleep(1000);
                            driver.Swipe(360, 5, 330, 700, 300);
                            // выключаем режим самолёта
                            try
                            {
                                wait.Until(ExpectedConditions.PresenceOfAllElementsLocatedBy(By.XPath("//android.view.View[contains(@content-desc, 'Режим полета включен.')]")));
                                driver.FindElementByXPath("//android.view.View[contains(@content-desc, 'Режим полета включен.')]").Click();
                                log.Info("Пауза 5 секунд.");
                                Thread.Sleep(5000);
                            }
                            catch
                            {
                                log.Error("Ошибка при выключении режима самолёта.");
                            }
                            #endregion

                            driver.PressKeyCode(AndroidKeyCode.Home);

                            // запись результата в файл зарегистрированных аккаунтов
                            #region **** output file ****
                            log.Info("Запись результата в файл зарегистрированных аккаунтов.");

                            string strOutputInsta = string.Format("{0}:{1}:{2}:{3}",
                                insta.Login,
                                insta.Password,
                                settings.proxyIP,
                                proxyport);

                            using (outputInsta = new StreamWriter(settings.pathOutputInsta, true))
                                outputInsta.WriteLine(strOutputInsta);
                            log.Info("Файл записан.");
                            #endregion

                            // завершение Appium
                            if (app.AppiumStop(status))
                                log.Info(status);
                            else
                                log.Error("Ошибка завершения Appium");
                        }
                    }
                    catch (WebDriverException e)
                    {
                        log.Error("Закончился таймаут на подключение к Appium. Подробности смотрите ниже.");
                        log.Error(e.ToString());
                        log.Info("Закрываем процесс Appium: {0}", app.pid);
                        if (app.AppiumStop(status))
                            log.Info(status);
                        else
                            log.Error("Ошибка завершения Appium");
                    }
                }
                else
                {
                    log.Error("Ошибка запуска Appium");
                }
            }

            log.Info("**** **** **** ****");
        }
    }
}