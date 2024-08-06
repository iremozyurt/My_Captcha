using CaptchaDemo.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace CaptchaDemo.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private const int CaptchaExpirationSeconds = 30;
        private readonly ApplicationDbContext _dbContext;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext dbContext)
        {
            _logger = logger;
            _dbContext = dbContext;
        }

        public IActionResult Index()
        {
            ViewBag.CaptchaImage = GetCaptchaImage();
            return View();
        }

        public IActionResult Register()
        {
            ViewBag.CaptchaImage = GetCaptchaImage();
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        public string GenerateRandomText(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        public Bitmap GenerateCaptchaImage(string captchaText)
        {
            Bitmap bitmap = new Bitmap(200, 60);
            Random random = new Random();

            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.White);
                using (Font font = new Font("Arial", 24, FontStyle.Italic))
                {
                    g.DrawString(captchaText, font, Brushes.Black, new PointF(10, 10));
                }
                 //Add random pixels
                for (int i = 0; i < 100; i++)
                {
                    int x = random.Next(0, bitmap.Width);
                    int y = random.Next(0, bitmap.Height);
                    bitmap.SetPixel(x, y, Color.Gray);
                }
                  // Adds random lines
                for (int i = 0; i < 10; i++)
                {
                    int x1 = random.Next(0, bitmap.Width);
                    int y1 = random.Next(0, bitmap.Height);
                    int x2 = random.Next(0, bitmap.Width);
                    int y2 = random.Next(0, bitmap.Height);
                    g.DrawLine(Pens.Black, x1, y1, x2, y2);
                }
            }
            return bitmap;
        }
        // Returns the CAPTCHA image as base64
        private string GetCaptchaImage()
        {
            string captchaText = GenerateRandomText(6);// Generates random CAPTCHA text
            string hashedCaptcha = ComputeSha256Hash(captchaText);// Hashes the text with SHA256
            HttpContext.Session.SetString("CaptchaHash", hashedCaptcha);// Stores the hashed text in the session

            //Stores the CAPTCHA creation time
            HttpContext.Session.SetString("CaptchaTimestamp", DateTime.UtcNow.ToString("o"));

            using (var ms = new MemoryStream())
            {
                using (Bitmap bitmap = GenerateCaptchaImage(captchaText))
                {
                    bitmap.Save(ms, ImageFormat.Png);
                    var base64String = Convert.ToBase64String(ms.ToArray());
                    return $"data:image/png;base64,{base64String}";
                }
            }
        }

        private string ComputeSha256Hash(string rawData)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        private bool IsCaptchaExpired()
        {
            string timestampStr = HttpContext.Session.GetString("CaptchaTimestamp");
            if (DateTime.TryParse(timestampStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime timestamp))
            {
                return (DateTime.UtcNow - timestamp).TotalSeconds > CaptchaExpirationSeconds;
            }
            return true;
        }

        [HttpPost]
        public async Task<IActionResult> VerifyCaptcha(string userInput, string action, string username, string password, string fullName, string phone)
        {
            if (IsCaptchaExpired())
            {
                ViewBag.Message = "CAPTCHA süresi dolmuş. Lütfen yeni bir CAPTCHA alın.";
                ViewBag.MessageColor = "red";
                ViewBag.CaptchaImage = GetCaptchaImage();
                return View(action == "Register" ? "Register" : "Index");
            }

            string storedCaptchaHash = HttpContext.Session.GetString("CaptchaHash");
            string userInputHash = ComputeSha256Hash(userInput);

            if (userInputHash == storedCaptchaHash)
            {
                if (action == "Register")
                {
                    var existingUser = _dbContext.Users.FirstOrDefault(u => u.Username == username);
                    if (existingUser != null)
                    {
                        ViewBag.Message = "Bu kullanıcı adı zaten alınmış.";
                        ViewBag.MessageColor = "red";
                        ViewBag.CaptchaImage = GetCaptchaImage();
                        return View("Register");
                    }

                    var hashedPassword = ComputeSha256Hash(password);
                    var user = new User
                    {
                        Username = username,
                        Password = hashedPassword,
                        FullName = fullName,
                        PhoneNumber = phone 
                    };
                    _dbContext.Users.Add(user);
                    await _dbContext.SaveChangesAsync();

                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    var hashedPassword = ComputeSha256Hash(password);
                    var user = _dbContext.Users.FirstOrDefault(u => u.Username == username && u.Password == hashedPassword);

                    if (user != null)
                    {
                        return RedirectToAction("Welcome");
                    }
                    else
                    {
                        ViewBag.Message = "Kullanıcı adı veya şifre yanlış.";
                        ViewBag.MessageColor = "red";
                        ViewBag.CaptchaImage = GetCaptchaImage();
                        return View("Index");
                    }
                }
            }
            else
            {
                ViewBag.Message = "CAPTCHA yanlış!";
                ViewBag.MessageColor = "red";
                ViewBag.CaptchaImage = GetCaptchaImage();
                return View(action == "Register" ? "Register" : "Index");
            }
        }


        public IActionResult Welcome()
        {
            return View();
        }

        // Yeni CAPTCHA oluşturma için AJAX çağrısı
        [HttpGet]
        public IActionResult GenerateNewCaptcha()
        {
            string captchaImageUrl = GetCaptchaImage();
            return Json(new { captchaImageUrl });
        }
    }
}
